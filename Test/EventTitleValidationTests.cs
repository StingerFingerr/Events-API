using System.ComponentModel.DataAnnotations;
using Application.Services.Events;
using Domain.Consts;
using Application.DTOs.Events.Incoming;
using Domain.Models;
using Application.Repositories.Events;
using Moq;

namespace Test;

public class EventTitleValidationTests
{
    public static TheoryData<string?> InvalidTitles => new() { null, "", " ", "     ", "abcd" };

    [Theory]
    [MemberData(nameof(InvalidTitles))]
    public async Task UpdateTitle_InvalidTitleIsRejectedBeforeAccessingRepository(string? title)
    {
        // Arrange
        var repository = new Mock<IEventRepository>(MockBehavior.Strict);
        var service = new EventsService(repository.Object);

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateEvent(Guid.NewGuid(), title!));

        // Assert
        Assert.Equal(ErrorsMessages.EventTitleIsShort, exception.Message);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidTitles))]
    public async Task CreateAndUpdate_InvalidTitleIsRejectedBeforeAccessingRepository(string? title)
    {
        // Arrange
        var repository = new Mock<IEventRepository>(MockBehavior.Strict);
        var service = new EventsService(repository.Object);
        var dto = new CreateEventDto
        {
            Title = title!,
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 10
        };

        // Act
        var createException = await Assert.ThrowsAsync<ValidationException>(() => service.CreateEvent(dto));
        var updateException = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateEvent(Guid.NewGuid(), dto));

        // Assert
        Assert.Equal(ErrorsMessages.EventTitleIsShort, createException.Message);
        Assert.Equal(ErrorsMessages.EventTitleIsShort, updateException.Message);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidTitles))]
    public void UpdateTitleDto_InvalidTitleFailsValidation(string? title)
    {
        // Arrange
        var dto = new UpdateTitleDto { Title = title! };
        var results = new List<ValidationResult>();

        // Act
        var valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        // Assert
        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(UpdateTitleDto.Title)));
    }

    [Fact]
    public async Task UpdateTitle_FiveCharactersAreAcceptedAndSaved()
    {
        // Arrange
        var eventData = Event.Create(Guid.NewGuid(), "Original", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);
        var repository = new Mock<IEventRepository>(MockBehavior.Strict);
        repository.Setup(r => r.ExistsByTitleAsync("abcde", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(r => r.GetByIdAsync(eventData.Id, It.IsAny<CancellationToken>())).ReturnsAsync(eventData);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var service = new EventsService(repository.Object);

        // Act
        var result = await service.UpdateEvent(eventData.Id, "abcde");

        // Assert
        Assert.Equal("abcde", result.Title);
        Assert.Equal("abcde", eventData.Title);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
