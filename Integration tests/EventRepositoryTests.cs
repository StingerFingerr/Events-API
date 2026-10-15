using Events_API.DTOs.Events.Incoming;
using Events_API.Repositories.Events;
using Microsoft.EntityFrameworkCore;

namespace Integration_tests;

[Collection(PostgreSqlCollection.Name)]
public sealed class EventRepositoryTests(PostgreSqlFixture database) : RepositoryTestBase(database)
{
    [Fact]
    public async Task AddAsync_PersistsAllFields()
    {
        // Arrange
        var eventData = CreateEvent();
        eventData.TryReserveSeats(2);

        // Act
        await using (var context = Database.CreateContext())
            await new EventRepository(context).AddAsync(eventData);

        // Assert
        await using var verification = Database.CreateContext();
        var saved = await verification.Events.SingleAsync();
        Assert.Equal(eventData.Id, saved.Id);
        Assert.Equal(eventData.Title, saved.Title);
        Assert.Equal(eventData.Description, saved.Description);
        Assert.Equal(eventData.StartAt, saved.StartAt);
        Assert.Equal(eventData.EndAt, saved.EndAt);
        Assert.Equal(10, saved.TotalSeats);
        Assert.Equal(8, saved.AvailableSeats);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTrackedEventOrNull()
    {
        // Arrange
        var eventData = CreateEvent();
        await using (var seed = Database.CreateContext())
            await new EventRepository(seed).AddAsync(eventData);

        await using var context = Database.CreateContext();
        var repository = new EventRepository(context);

        // Act
        var found = await repository.GetByIdAsync(eventData.Id);
        var foundAgain = await repository.GetByIdAsync(eventData.Id);
        var missing = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.NotNull(found);
        Assert.Equal(eventData.Title, found.Title);
        Assert.Equal(EntityState.Unchanged, context.Entry(found).State);
        Assert.Same(found, foundAgain);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetByIdWithReloadAsync_RefreshesTrackedEvent()
    {
        // Arrange
        await using var context = Database.CreateContext();
        var repository = new EventRepository(context);
        var eventData = CreateEvent();
        await repository.AddAsync(eventData);

        await using (var otherContext = Database.CreateContext())
        {
            var otherRepository = new EventRepository(otherContext);
            var otherEvent = (await otherRepository.GetByIdAsync(eventData.Id))!;
            otherEvent.Title = "Updated festival";
            otherEvent.TryReserveSeats(3);
            await otherRepository.SaveChangesAsync();
        }

        // Act
        var reloaded = await repository.GetByIdWithReloadAsync(eventData.Id);

        // Assert
        Assert.Same(eventData, reloaded);
        Assert.Equal("Updated festival", reloaded!.Title);
        Assert.Equal(7, reloaded.AvailableSeats);
    }

    [Fact]
    public async Task GetByIdWithReloadAsync_ReturnsNullForDeletedOrMissingEvent()
    {
        // Arrange
        await using var context = Database.CreateContext();
        var repository = new EventRepository(context);
        var eventData = CreateEvent();
        await repository.AddAsync(eventData);
        await using (var otherContext = Database.CreateContext())
            await new EventRepository(otherContext).RemoveAsync((await otherContext.Events.SingleAsync()));

        // Act
        var deleted = await repository.GetByIdWithReloadAsync(eventData.Id);
        var missing = await repository.GetByIdWithReloadAsync(Guid.NewGuid());

        // Assert
        Assert.Null(deleted);
        Assert.Equal(EntityState.Detached, context.Entry(eventData).State);
        Assert.Null(missing);
    }

    [Theory]
    [InlineData("Rock Festival", true)]
    [InlineData("rOcK fEsTiVaL", true)]
    [InlineData("Festival", false)]
    [InlineData("Missing", false)]
    [InlineData("", false)]
    public async Task ExistsByTitleAsync_MatchesFullTitleIgnoringCase(string title, bool expected)
    {
        // Arrange
        await using var context = Database.CreateContext();
        var repository = new EventRepository(context);
        await repository.AddAsync(CreateEvent());

        // Act
        var exists = await repository.ExistsByTitleAsync(title);

        // Assert
        Assert.Equal(expected, exists);
    }

    public static TheoryData<string?, int?, int?, int[]> FilterCases => new()
    {
        // No filters and each individual filter.
        { null, null, null, new[] { 0, 1, 2, 3, 4 } },
        { "fEsTiVaL", null, null, new[] { 0, 2, 4 } },
        { null, 1, null, new[] { 1, 2, 3, 4 } },
        { null, null, 3, new[] { 0, 1, 2, 3 } },

        // Every pair and all three filters together.
        { "festival", 1, null, new[] { 2, 4 } },
        { "festival", null, 3, new[] { 0, 2 } },
        { null, 1, 3, new[] { 1, 2, 3 } },
        { "festival", 1, 3, new[] { 2 } },

        // Empty input, missing matches, inclusive boundaries and reversed range.
        { "", null, null, new[] { 0, 1, 2, 3, 4 } },
        { "missing", null, null, Array.Empty<int>() },
        { null, 2, 2, new[] { 2 } },
        { null, 3, 1, Array.Empty<int>() },
        { null, 5, null, Array.Empty<int>() },
        { null, null, -1, Array.Empty<int>() }
    };

    [Theory]
    [InlineData("100% Festival", "100% festival", true)]
    [InlineData("1000 Festival", "100% festival", false)]
    [InlineData("Rock_Festival", "rock_festival", true)]
    [InlineData("Rock Festival", "rock_festival", false)]
    [InlineData(@"Rock\Festival", @"rock\festival", true)]
    [InlineData("RockFestival", @"rock\festival", false)]
    public async Task ExistsByTitleAsync_TreatsPatternCharactersLiterally(string title, string search, bool expected)
    {
        // Arrange
        await using var context = Database.CreateContext();
        var repository = new EventRepository(context);
        await repository.AddAsync(CreateEvent(title));

        // Act
        var exists = await repository.ExistsByTitleAsync(search);

        // Assert
        Assert.Equal(expected, exists);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData(@"\")]
    public async Task GetByFiltersAsync_TreatsPatternCharactersLiterally(string search)
    {
        // Arrange
        await using (var seed = Database.CreateContext())
        {
            seed.Events.AddRange(CreateEvent("100% Festival"), CreateEvent("Rock_Festival"),
                CreateEvent(@"Rock\Festival"), CreateEvent("Rock Festival"));
            await seed.SaveChangesAsync();
        }
        await using var context = Database.CreateContext();

        // Act
        var result = await new EventRepository(context).GetByFiltersAsync(new GetEventsByFiltersDto { Title = search });

        // Assert
        var found = Assert.Single(result.Items);
        Assert.Contains(search, found.Title);
        Assert.Equal(1, result.TotalItems);
    }

    [Theory]
    [MemberData(nameof(FilterCases))]
    public async Task GetByFiltersAsync_AppliesEveryFilterCombination(
        string? title, int? fromDay, int? toDay, int[] expectedDays)
    {
        // Arrange
        var events = await SeedFilterEventsAsync();
        await using var context = Database.CreateContext();

        // Act
        var result = await new EventRepository(context).GetByFiltersAsync(new GetEventsByFiltersDto
        {
            Title = title,
            From = fromDay.HasValue ? StartAt.AddDays(fromDay.Value) : null,
            To = toDay.HasValue ? StartAt.AddDays(toDay.Value) : null
        });

        // Assert
        Assert.Equal(expectedDays.Select(day => events[day].Id), result.Items.Select(e => e.Id));
        Assert.Equal(expectedDays.Length, result.TotalItems);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(1, 2, 0, 2)]
    [InlineData(2, 2, 2, 2)]
    [InlineData(3, 2, 4, 1)]
    [InlineData(4, 2, 5, 0)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(5, 1, 4, 1)]
    [InlineData(1, 10, 0, 5)]
    public async Task GetByFiltersAsync_PaginatesInDateOrderAndCountsAllMatches(int page, int pageSize, int skip, int count)
    {
        // Arrange
        var events = await SeedFilterEventsAsync();
        await using var context = Database.CreateContext();

        // Act
        var result = await new EventRepository(context).GetByFiltersAsync(new GetEventsByFiltersDto { Page = page, PageSize = pageSize });

        // Assert
        Assert.Equal(events.Skip(skip).Take(count).Select(e => e.Id), result.Items.Select(e => e.Id));
        Assert.Equal(5, result.TotalItems);
    }

    [Fact]
    public async Task GetByFiltersAsync_PaginatesFilteredMatches()
    {
        // Arrange
        var events = await SeedFilterEventsAsync();
        await using var context = Database.CreateContext();

        // Act
        var result = await new EventRepository(context).GetByFiltersAsync(new GetEventsByFiltersDto
        {
            Title = "festival",
            From = StartAt,
            To = StartAt.AddDays(4),
            Page = 2,
            PageSize = 2
        });

        // Assert
        Assert.Equal(events[4].Id, Assert.Single(result.Items).Id);
        Assert.Equal(3, result.TotalItems);
    }

    [Fact]
    public async Task GetByFiltersAsync_EmptyDatabaseReturnsEmptyPage()
    {
        // Arrange
        await using var context = Database.CreateContext();

        // Act
        var result = await new EventRepository(context).GetByFiltersAsync(new GetEventsByFiltersDto());

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChangesToTrackedEvent()
    {
        // Arrange
        var eventData = CreateEvent();
        await using (var context = Database.CreateContext())
        {
            var repository = new EventRepository(context);
            await repository.AddAsync(eventData);
            var tracked = (await repository.GetByIdAsync(eventData.Id))!;

            // Act
            tracked.Title = "Updated event";
            tracked.Description = null;
            tracked.StartAt = StartAt.AddDays(1);
            tracked.EndAt = StartAt.AddDays(2);
            tracked.TryReserveSeats(2);
            tracked.TryUpdateCapacity(20);
            await repository.SaveChangesAsync();
        }

        // Assert
        await using var verification = Database.CreateContext();
        var saved = await verification.Events.SingleAsync();
        Assert.Equal("Updated event", saved.Title);
        Assert.Null(saved.Description);
        Assert.Equal(StartAt.AddDays(1), saved.StartAt);
        Assert.Equal(StartAt.AddDays(2), saved.EndAt);
        Assert.Equal(20, saved.TotalSeats);
        Assert.Equal(18, saved.AvailableSeats);
    }

    [Fact]
    public async Task RemoveAsync_DeletesEventAndCascadesBookings()
    {
        // Arrange
        await using (var seed = Database.CreateContext())
        {
            var eventData = CreateEvent();
            seed.Bookings.Add(CreateBooking(eventData));
            await seed.SaveChangesAsync();
        }

        // Act
        await using (var context = Database.CreateContext())
            await new EventRepository(context).RemoveAsync(await context.Events.SingleAsync());

        // Assert
        await using var verification = Database.CreateContext();
        Assert.Empty(await verification.Events.ToListAsync());
        Assert.Empty(await verification.Bookings.ToListAsync());
    }

    private async Task<Events_API.Models.Event[]> SeedFilterEventsAsync()
    {
        var events = new[]
        {
            CreateEvent("Rock Festival", 0), CreateEvent("Concert", 1), CreateEvent("Food FESTIVAL", 2),
            CreateEvent("Conference", 3), CreateEvent("Film festival", 4)
        };
        await using var context = Database.CreateContext();
        // Insert in reverse date order to verify that the repository orders the results.
        context.Events.AddRange(events.Reverse());
        await context.SaveChangesAsync();
        return events;
    }
}
