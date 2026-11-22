using System.Text.Json;
using Events.Application.Caching;
using Events.Application.DTOs.Events;
using Events.Application.DTOs.Events.Results;
using Events.Application.Repositories.Events;
using Events.Application.Services.Events;
using Events.Domain.Exceptions;
using Events.Domain.Models;
using Events.Infrastructure.Caching;
using Events.Infrastructure.DataAccess;
using Events.Infrastructure.Repositories.Events;
using Events.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Test;

public class EventsCacheAsideTests
{
    private readonly Mock<IEventRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<ICacheService> cache = new();

    private static Event CreateEvent(int total = 100, int sold = 0)
    {
        var item = Event.Create(Guid.NewGuid(), "Test event", DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2), total);
        if (sold > 0)
            Assert.True(item.TryReserveSeats(sold));
        return item;
    }

    [Fact]
    public async Task GetById_CacheHitDoesNotReadDatabase()
    {
        var dto = CreateEvent().AsDto();
        cache.Setup(x => x.GetAsync($"event:{dto.Id}")).ReturnsAsync(JsonSerializer.Serialize(dto));

        Assert.Equal(dto, await new EventsService(repository.Object, cache.Object).GetEventById(dto.Id));
        repository.VerifyNoOtherCalls();
        cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid json")]
    public async Task GetById_CacheMissReadsDatabaseAndStoresDtoWithTtl(string? payload)
    {
        var item = CreateEvent();
        var key = $"event:{item.Id}";
        cache.Setup(x => x.GetAsync(key)).ReturnsAsync(payload);
        repository.Setup(x => x.GetByIdAsync(item.Id, default)).ReturnsAsync(item);
        var service = new EventsService(repository.Object, cache.Object);

        Assert.Equal(item.AsDto(), await service.GetEventById(item.Id));
        cache.Verify(x => x.SetAsync(key, JsonSerializer.Serialize(item.AsDto()), TimeSpan.FromMinutes(5)), Times.Once);
    }

    [Fact]
    public async Task GetById_NotFoundIsNotCached()
    {
        var id = Guid.NewGuid();
        repository.Setup(x => x.GetByIdAsync(id, default)).ReturnsAsync((Event?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new EventsService(repository.Object, cache.Object).GetEventById(id));
        cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task Top_CacheHitDoesNotReadDatabase()
    {
        var expected = new List<EventDto> { CreateEvent().AsDto() };
        cache.Setup(x => x.GetAsync("events:top10")).ReturnsAsync(JsonSerializer.Serialize(expected));

        var result = await new EventsService(repository.Object, cache.Object).GetTopEvents();

        Assert.Equal(expected, result);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Top_CacheMissStoresResultIncludingEmptyList(bool empty)
    {
        var items = empty ? new List<Event>() : new List<Event> { CreateEvent() };
        repository.Setup(x => x.GetTopEventsAsync(default)).ReturnsAsync(items);
        var service = new EventsService(repository.Object, cache.Object);

        var result = await service.GetTopEvents();

        Assert.Equal(items.Select(x => x.AsDto()), result);
        cache.Verify(x => x.SetAsync("events:top10", JsonSerializer.Serialize(result), TimeSpan.FromMinutes(5)), Times.Once);
        var controller = new EventsController(service);
        Assert.IsType<List<EventDto>>(Assert.IsType<OkObjectResult>(await controller.GetTopEvents()).Value);
    }

    [Fact]
    public async Task RedisUnavailable_BothReadsReturnDatabaseResults()
    {
        var item = CreateEvent();
        repository.Setup(x => x.GetByIdAsync(item.Id, default)).ReturnsAsync(item);
        repository.Setup(x => x.GetTopEventsAsync(default)).ReturnsAsync([item]);
        var unavailableCache = new RedisCacheService(
            () => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                CommandFlags.None, "Offline", null, CommandStatus.Unknown),
            NullLogger<RedisCacheService>.Instance);
        var service = new EventsService(repository.Object, unavailableCache);

        Assert.Equal(item.AsDto(), await service.GetEventById(item.Id));
        Assert.Equal(item.AsDto(), Assert.Single(await service.GetTopEvents()));
    }

    [Fact]
    public async Task TopRepository_OrdersByFractionRatherThanSoldCountAndLimitsToTen()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new EventsDbContext(options);
        var highest = CreateEvent(10, 9);
        var next = CreateEvent(1000, 800);
        var heldOnly = CreateEvent(10);
        Assert.True(heldOnly.TryHoldSeats(10));
        context.Events.AddRange(highest, next, heldOnly);
        context.Events.AddRange(Enumerable.Range(1, 10).Select(i => CreateEvent(100, i)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new EventRepository(context).GetTopEventsAsync();

        Assert.Equal(10, result.Count);
        Assert.Equal(highest.Id, result[0].Id);
        Assert.Equal(next.Id, result[1].Id);
        Assert.DoesNotContain(result, x => x.Id == heldOnly.Id);
        Assert.Equal(Enumerable.Range(3, 8).Reverse(), result.Skip(2).Select(x => x.TotalSeats - x.AvailableSeats));
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
