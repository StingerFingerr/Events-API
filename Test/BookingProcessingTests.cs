using Application.Repositories.Bookings;
using Application.Services.Bookings.Processing;
using Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Test;

public class BookingProcessingTests
{
    [Fact]
    public async Task ProcessBatchAsync_ConfirmsPendingAndSkipsFinalOrMissingBookingsDespiteFailure()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var pending = new Booking { Id = ids[0], Status = BookingStatus.Pending };
        var confirmed = new Booking { Id = ids[1], Status = BookingStatus.Confirmed };
        var rejected = new Booking { Id = ids[2], Status = BookingStatus.Rejected };
        var repositories = new List<Mock<IBookingRepository>>();
        var services = new ServiceCollection();
        services.AddScoped<IBookingRepository>(_ =>
        {
            var repository = new Mock<IBookingRepository>();
            repository.Setup(x => x.GetPendingIdsAsync(100, CancellationToken.None)).ReturnsAsync(ids);
            repository.Setup(x => x.GetByIdAsync(ids[0], CancellationToken.None)).ReturnsAsync(pending);
            repository.Setup(x => x.GetByIdAsync(ids[1], CancellationToken.None)).ReturnsAsync(confirmed);
            repository.Setup(x => x.GetByIdAsync(ids[2], CancellationToken.None)).ReturnsAsync(rejected);
            repository.Setup(x => x.GetByIdAsync(ids[4], CancellationToken.None)).ThrowsAsync(new InvalidOperationException());
            lock (repositories)
                repositories.Add(repository);
            return repository.Object;
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var processor = new PendingBookingsProcessor(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingBookingsProcessor>.Instance);

        Assert.Equal(5, await processor.ProcessBatchAsync(CancellationToken.None));

        Assert.Equal(BookingStatus.Confirmed, pending.Status);
        Assert.NotNull(pending.ProcessedAt);
        Assert.Null(confirmed.ProcessedAt);
        Assert.Equal(BookingStatus.Rejected, rejected.Status);
        Assert.Equal(6, repositories.Count);
        Assert.Equal(1, repositories.Sum(repository => repository.Invocations.Count(
            invocation => invocation.Method.Name == nameof(IBookingRepository.SaveChangesAsync))));
    }

    [Fact]
    public async Task ProcessBatchAsync_PropagatesShutdownCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new Mock<IBookingRepository>();
        repository.Setup(x => x.GetPendingIdsAsync(100, cancellation.Token)).Returns(() =>
        {
            cancellation.Cancel();
            return Task.FromResult(new[] { Guid.NewGuid() });
        });
        var services = new ServiceCollection();
        services.AddScoped<IBookingRepository>(_ => repository.Object);
        using var provider = services.BuildServiceProvider();
        var processor = new PendingBookingsProcessor(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PendingBookingsProcessor>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessBatchAsync(cancellation.Token));
    }
}
