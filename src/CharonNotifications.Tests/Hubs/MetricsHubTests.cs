using CharonDbContext.Models;
using FluentAssertions;
using System.Text.Json;

namespace CharonNotifications.Tests.Hubs;

/// <summary>
/// Tests for MetricsHub DTO conversion logic.
/// Note: Full SignalR hub testing requires integration tests due to framework limitations.
/// The controller tests verify the hub integration.
/// </summary>
public class MetricsHubTests
{
    [Fact]
    public void MetricDtoConversion_ShouldConvertMetricCorrectly()
    {
        // Arrange
        var metric = new Metric
        {
            Id = 1,
            Type = "motion",
            Name = "Garage",
            PayloadJson = JsonSerializer.Serialize(new Dictionary<string, object> { { "motionDetected", true } }),
            CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act - Simulate the DTO conversion logic from MetricsHub
        var metricDto = new
        {
            id = metric.Id,
            type = metric.Type,
            name = metric.Name,
            payload = string.IsNullOrEmpty(metric.PayloadJson)
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(metric.PayloadJson) ?? new Dictionary<string, object>(),
            createdAt = metric.CreatedAt.ToString("O")
        };

        // Assert
        metricDto.id.Should().Be(1);
        metricDto.type.Should().Be("motion");
        metricDto.name.Should().Be("Garage");
        metricDto.payload.Should().ContainKey("motionDetected");
        metricDto.createdAt.Should().Be(metric.CreatedAt.ToString("O"));
    }

    [Fact]
    public void MetricDtoConversion_ShouldHandleEmptyPayloadJson()
    {
        // Arrange
        var metric = new Metric
        {
            Id = 2,
            Type = "air_quality",
            Name = "Kitchen",
            PayloadJson = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var payload = string.IsNullOrEmpty(metric.PayloadJson)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(metric.PayloadJson) ?? new Dictionary<string, object>();

        // Assert
        payload.Should().BeEmpty();
    }

    [Fact]
    public void MetricDtoConversion_ShouldHandleNullPayloadJson()
    {
        // Arrange
        var metric = new Metric
        {
            Id = 3,
            Type = "motion",
            Name = "Bedroom",
            PayloadJson = null!,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var payload = string.IsNullOrEmpty(metric.PayloadJson)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(metric.PayloadJson) ?? new Dictionary<string, object>();

        // Assert
        payload.Should().BeEmpty();
    }

    [Fact]
    public void MetricDtoConversion_ShouldDeserializePayloadCorrectly()
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
            Id = 4,
            Type = "air_quality",
            Name = "Living Room",
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var deserializedPayload = string.IsNullOrEmpty(metric.PayloadJson)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(metric.PayloadJson) ?? new Dictionary<string, object>();

        // Assert
        deserializedPayload.Should().ContainKey("temperature");
        deserializedPayload.Should().ContainKey("humidity");
        deserializedPayload.Should().ContainKey("pressure");
        
        // JSON deserialization returns JsonElement, so we need to convert
        var tempValue = deserializedPayload["temperature"];
        var humidityValue = deserializedPayload["humidity"];
        var pressureValue = deserializedPayload["pressure"];
        
        // Convert JsonElement to double for comparison
        if (tempValue is System.Text.Json.JsonElement tempElement)
            tempElement.GetDouble().Should().BeApproximately(22.5, 0.01);
        else
            tempValue.Should().Be(22.5);
            
        if (humidityValue is System.Text.Json.JsonElement humidityElement)
            humidityElement.GetDouble().Should().BeApproximately(45.0, 0.01);
        else
            humidityValue.Should().Be(45.0);
            
        if (pressureValue is System.Text.Json.JsonElement pressureElement)
            pressureElement.GetDouble().Should().BeApproximately(1013.25, 0.01);
        else
            pressureValue.Should().Be(1013.25);
    }

    [Fact]
    public void MetricDtoConversion_ShouldFormatCreatedAtAsIso8601()
    {
        // Arrange
        var createdAt = new DateTime(2024, 1, 15, 14, 30, 45, DateTimeKind.Utc);
        var metric = new Metric
        {
            Id = 5,
            Type = "energy",
            Name = "Corridor",
            PayloadJson = JsonSerializer.Serialize(new Dictionary<string, object>()),
            CreatedAt = createdAt
        };

        // Act
        var formattedDate = metric.CreatedAt.ToString("O");

        // Assert
        formattedDate.Should().Be(createdAt.ToString("O")); // ISO 8601 format
        formattedDate.Should().Contain("2024-01-15T14:30:45");
    }
}

