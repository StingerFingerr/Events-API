using Contracts;
using Events.Application.Caching;
using Events.Application.DTOs.Events.Incoming;
using Events.Application.Messaging;
using Events.Application.Repositories.Events;
using Events.Application.Services.Events;
using Events.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Test;

public class EventCacheInvalidationTests
{
    [Theory]
    [InlineData("put")]
    [InlineData("patch")]
    [InlineData("delete")]
    public async Task Mutation_WaitsForDatabaseCommitBeforeInvalidatingCache(string operation)
    {
        var item = CreateEvent();
        var repository = new Mock<IEventRepository>();
        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        var saveCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Setup(x => x.GetByIdAsync(item.Id, default)).ReturnsAsync(item);
        repository.Setup(x => x.SaveChangesAsync(default)).Returns(saveCompleted.Task);
        repository.Setup(x => x.RemoveAsync(item, default)).Returns(saveCompleted.Task);
        cache.Setup(x => x.RemoveAsync($"event:{item.Id}")).Returns(Task.CompletedTask);
        var service = new EventsService(repository.Object, cache.Object, new CacheOptions());

        Task mutation = operation switch
        {
            "put" => service.UpdateEvent(item.Id, new CreateEventDto
            {
                Title = "Updated concert",
                StartAt = item.StartAt,
                EndAt = item.EndAt,
                TotalSeats = item.TotalSeats
            }),
            "patch" => service.UpdateEvent(item.Id, "Updated concert"),
            _ => service.DeleteEvent(item.Id)
        };

        Assert.False(mutation.IsCompleted);
        cache.VerifyNoOtherCalls();
        saveCompleted.SetResult();
        await mutation.WaitAsync(TimeSpan.FromSeconds(5));

        cache.Verify(x => x.RemoveAsync($"event:{item.Id}"), Times.Once);
        cache.VerifyNoOtherCalls();
    }

    private static Event CreateEvent() => Event.Create(Guid.NewGuid(), "Concert",
        DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);

    [Theory]
    [InlineData("put", false)]
    [InlineData("patch", false)]
    [InlineData("delete", false)]
    [InlineData("put", true)]
    [InlineData("patch", true)]
    [InlineData("delete", true)]
    public async Task ApiMutation_InvalidatesOnlyAfterSuccessfulSave(string operation, bool failSave)
    {
        var item = CreateEvent();
        var repository = new Mock<IEventRepository>();
        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        var saved = false;
        repository.Setup(x => x.GetByIdAsync(item.Id, default)).ReturnsAsync(item);
        Task Save()
        {
            if (failSave) throw new InvalidOperationException("Database failure");
            saved = true;
            return Task.CompletedTask;
        }
        repository.Setup(x => x.SaveChangesAsync(default)).Returns(Save);
        repository.Setup(x => x.RemoveAsync(item, default)).Returns(Save);
        cache.Setup(x => x.RemoveAsync($"event:{item.Id}"))
            .Callback(() => Assert.True(saved)).Returns(Task.CompletedTask);
        var service = new EventsService(repository.Object, cache.Object, new Events.Application.Caching.CacheOptions());
        Task Act() => operation switch
        {
            "put" => service.UpdateEvent(item.Id, new CreateEventDto
            {
                Title = "Updated concert",
                Description = item.Description,
                StartAt = item.StartAt,
                EndAt = item.EndAt,
                TotalSeats = item.TotalSeats
            }),
            "patch" => service.UpdateEvent(item.Id, "Updated concert"),
            _ => service.DeleteEvent(item.Id)
        };

        if (failSave) await Assert.ThrowsAsync<InvalidOperationException>(Act);
        else await Act();

        cache.Verify(x => x.RemoveAsync($"event:{item.Id}"), failSave ? Times.Never() : Times.Once());
        cache.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("reserve", false)]
    [InlineData("confirm", false)]
    [InlineData("cancel-held", false)]
    [InlineData("cancel-confirmed", false)]
    [InlineData("reserve", true)]
    [InlineData("confirm", true)]
    [InlineData("cancel-held", true)]
    [InlineData("cancel-confirmed", true)]
    public async Task KafkaMutation_InvalidatesOnlyAfterSuccessfulSave(string operation, bool failSave)
    {
        var item = CreateEvent();
        var bookingId = Guid.NewGuid();
        var repository = new Mock<IReservationRepository>();
        var cache = new Mock<ICacheService>(MockBehavior.Strict);
        var saved = false;
        repository.Setup(x => x.GetEvent(item.Id, default)).ReturnsAsync(item);
        if (operation != "reserve")
        {
            var confirmed = operation == "cancel-confirmed";
            if (confirmed) item.TryReserveSeats(2);
            else item.TryHoldSeats(2);
            repository.Setup(x => x.GetReceipt(bookingId, default)).ReturnsAsync(new SeatReservation
            {
                Id = bookingId,
                EventId = item.Id,
                SeatCount = 2,
                State = confirmed ? ReservationState.Confirmed : ReservationState.Held
            });
        }
        repository.Setup(x => x.Save(default)).Returns(() =>
        {
            if (failSave) throw new InvalidOperationException("Database failure");
            saved = true;
            return Task.CompletedTask;
        });
        cache.Setup(x => x.RemoveAsync($"event:{item.Id}"))
            .Callback(() => Assert.True(saved)).Returns(Task.CompletedTask);
        var subscriber = new ReservationSubscriber(repository.Object,
            NullLogger<ReservationSubscriber>.Instance, cache.Object);
        Task Act() => operation switch
        {
            "reserve" => subscriber.Reserve(new ReserveSeats(bookingId, item.Id, 2), default),
            "confirm" => subscriber.Confirm(new BookingConfirmed(bookingId, item.Id,
                Guid.NewGuid(), 2, DateTimeOffset.UtcNow), default),
            _ => subscriber.Cancel(new BookingCancelled(bookingId), default)
        };

        if (failSave) await Assert.ThrowsAsync<InvalidOperationException>(Act);
        else await Act();

        cache.Verify(x => x.RemoveAsync($"event:{item.Id}"), failSave ? Times.Never() : Times.Once());
        cache.VerifyNoOtherCalls();
    }
}
