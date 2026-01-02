using Microsoft.AspNetCore.SignalR;
using CharonDbContext.Models;
using System.Text.Json;

namespace CharonNotifications.Hubs;

public class MetricsHub : Hub
{
    public async Task SendMetric(Metric metric)
    {
        // Convert Metric to the format expected by the frontend
        var metricDto = new
        {
            id = metric.Id,
            type = metric.Type,
            name = metric.Name,
            payload = string.IsNullOrEmpty(metric.PayloadJson)
                ? new Dictionary<string, object>()
                : JsonSerializer.Deserialize<Dictionary<string, object>>(metric.PayloadJson) ?? new Dictionary<string, object>(),
            createdAt = metric.CreatedAt.ToString("O") // ISO 8601 format
        };

        // Send actual metric data for instant Latest Values update
        await Clients.All.SendAsync("MetricReceived", metricDto);
        
        // Also send DataUpdated notification for chart/aggregation refresh via GraphQL
        await Clients.All.SendAsync("DataUpdated");
    }
}


