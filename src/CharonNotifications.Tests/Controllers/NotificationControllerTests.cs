using CharonNotifications.Controllers;
using CharonNotifications.Hubs;
using CharonDbContext.Data;
using CharonDbContext.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace CharonNotifications.Tests.Controllers;

public class NotificationControllerTests
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IHubContext<MetricsHub>> _hubContextMock;
    private readonly Mock<ILogger<NotificationController>> _loggerMock;
    private readonly NotificationController _controller;

    public NotificationControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _hubContextMock = new Mock<IHubContext<MetricsHub>>();
        _loggerMock = new Mock<ILogger<NotificationController>>();
        _controller = new NotificationController(
            _hubContextMock.Object,
            _dbContext,
            _loggerMock.Object);
    }

    [Fact]
    public async Task NotifyMetric_ShouldReturnNotFound_WhenMetricDoesNotExist()
    {
        // Arrange
        var nonExistentId = 999;

        // Act
        var result = await _controller.NotifyMetric(nonExistentId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task NotifyMetric_ShouldReturnOk_WhenMetricExists()
    {
        // Arrange
        var metric = new Metric
        {
            Id = 1,
            Type = "motion",
            Name = "Garage",
            PayloadJson = JsonSerializer.Serialize(new Dictionary<string, object> { { "motionDetected", true } }),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Metrics.Add(metric);
        await _dbContext.SaveChangesAsync();

        var clientsMock = new Mock<IClientProxy>();
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock.Setup(x => x.All).Returns(clientsMock.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

        // Act
        var result = await _controller.NotifyMetric(metric.Id);

        // Assert
        result.Should().BeOfType<OkResult>();
        // Verify both MetricReceived and DataUpdated are sent
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "MetricReceived",
                It.Is<object[]>(args => args.Length == 1),
                default(CancellationToken)),
            Times.Once);
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "DataUpdated",
                It.Is<object[]>(args => args.Length == 0),
                default(CancellationToken)),
            Times.Once);
    }

    [Fact]
    public async Task NotifyMetric_ShouldSendMetricAndDataUpdated_WhenMetricExists()
    {
        // Arrange
        var metric = new Metric
        {
            Id = 2,
            Type = "energy",
            Name = "Office",
            PayloadJson = JsonSerializer.Serialize(new Dictionary<string, object> { { "power", 150.5 } }),
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        _dbContext.Metrics.Add(metric);
        await _dbContext.SaveChangesAsync();

        var clientsMock = new Mock<IClientProxy>();
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock.Setup(x => x.All).Returns(clientsMock.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

        object[]? capturedMetricArgs = null;
        clientsMock.Setup(x => x.SendCoreAsync(
                "MetricReceived",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, ct) => capturedMetricArgs = args);

        // Act
        var result = await _controller.NotifyMetric(metric.Id);

        // Assert
        result.Should().BeOfType<OkResult>();
        
        // Verify MetricReceived was sent with correct data
        capturedMetricArgs.Should().NotBeNull();
        capturedMetricArgs!.Length.Should().Be(1);
        var metricDto = capturedMetricArgs[0];
        var dtoType = metricDto.GetType();
        var idProperty = dtoType.GetProperty("id");
        var typeProperty = dtoType.GetProperty("type");
        var nameProperty = dtoType.GetProperty("name");
        
        idProperty!.GetValue(metricDto).Should().Be(2);
        typeProperty!.GetValue(metricDto).Should().Be("energy");
        nameProperty!.GetValue(metricDto).Should().Be("Office");
        
        // Verify both events were sent
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "MetricReceived",
                It.Is<object[]>(args => args.Length == 1),
                default(CancellationToken)),
            Times.Once);
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "DataUpdated",
                It.Is<object[]>(args => args.Length == 0),
                default(CancellationToken)),
            Times.Once);
    }

    [Fact]
    public async Task NotifyMetric_ShouldHandleEmptyPayloadJson()
    {
        // Arrange
        var metric = new Metric
        {
            Id = 3,
            Type = "air_quality",
            Name = "Kitchen",
            PayloadJson = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Metrics.Add(metric);
        await _dbContext.SaveChangesAsync();

        var clientsMock = new Mock<IClientProxy>();
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock.Setup(x => x.All).Returns(clientsMock.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

        // Act
        var result = await _controller.NotifyMetric(metric.Id);

        // Assert
        result.Should().BeOfType<OkResult>();
        // Verify both events were sent
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "MetricReceived",
                It.Is<object[]>(args => args.Length == 1),
                default(CancellationToken)),
            Times.Once);
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "DataUpdated",
                It.Is<object[]>(args => args.Length == 0),
                default(CancellationToken)),
            Times.Once);
    }

    [Fact]
    public async Task NotifyMetric_ShouldReturnInternalServerError_WhenExceptionOccurs()
    {
        // Arrange
        var metric = new Metric
        {
            Id = 4,
            Type = "motion",
            Name = "Bedroom",
            PayloadJson = "invalid json",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Metrics.Add(metric);
        await _dbContext.SaveChangesAsync();

        // Simulate an exception by disposing the context
        await _dbContext.DisposeAsync();

        var newController = new NotificationController(
            _hubContextMock.Object,
            _dbContext,
            _loggerMock.Object);

        // Act
        var result = await newController.NotifyMetric(metric.Id);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("Error sending notification");
    }

    [Fact]
    public async Task NotifyMetric_ShouldDeserializePayloadCorrectly()
    {
        // Arrange
        var payload = new Dictionary<string, object>
        {
            { "temperature", 22.5 },
            { "humidity", 45.0 },
            { "pressure", 1013.25 }
        };

        var metric = new Metric
        {
            Id = 5,
            Type = "air_quality",
            Name = "Living Room",
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Metrics.Add(metric);
        await _dbContext.SaveChangesAsync();

        var clientsMock = new Mock<IClientProxy>();
        var hubClientsMock = new Mock<IHubClients>();
        hubClientsMock.Setup(x => x.All).Returns(clientsMock.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(hubClientsMock.Object);

        object[]? capturedMetricArgs = null;
        clientsMock.Setup(x => x.SendCoreAsync(
                "MetricReceived",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((method, args, ct) => capturedMetricArgs = args);

        // Act
        var result = await _controller.NotifyMetric(metric.Id);

        // Assert
        result.Should().BeOfType<OkResult>();
        capturedMetricArgs.Should().NotBeNull();
        var metricDto = capturedMetricArgs![0];
        var dtoType = metricDto.GetType();
        var payloadProperty = dtoType.GetProperty("payload");
        payloadProperty.Should().NotBeNull();

        var payloadValue = payloadProperty!.GetValue(metricDto);
        payloadValue.Should().BeOfType<Dictionary<string, object>>();
        var payloadDict = payloadValue as Dictionary<string, object>;
        payloadDict!.Should().ContainKey("temperature");
        payloadDict.Should().ContainKey("humidity");
        payloadDict.Should().ContainKey("pressure");
        
        // Verify both events were sent
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "MetricReceived",
                It.Is<object[]>(args => args.Length == 1),
                default(CancellationToken)),
            Times.Once);
        clientsMock.Verify(
            x => x.SendCoreAsync(
                "DataUpdated",
                It.Is<object[]>(args => args.Length == 0),
                default(CancellationToken)),
            Times.Once);
    }
}

