using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ToDoApp.Entities;
using ToDoApp.Models;

namespace ToDoApp.Services;

public sealed class HttpTaskService(HttpClient httpClient, ILogger<HttpTaskService> logger) : ITaskService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<HttpTaskService> _logger = logger;

    public async Task<IReadOnlyList<TaskItem>> GetUpcomingAsync(string userId, CancellationToken cancellationToken = default)
    {
        var tasks = await _httpClient.GetFromJsonAsync<List<TaskItem>>("/api/tasks", cancellationToken);
        return tasks ?? new List<TaskItem>();
    }

    public async Task<TaskItem?> GetByIdAsync(int taskId, string userId, bool asTracking = false, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TaskItem>($"/api/tasks/{taskId}", cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to load task {TaskId}", taskId);
            return null;
        }
    }

    public async Task<TaskItem> CreateAsync(TaskInputModel input, string userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/tasks", input, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(int taskId, TaskInputModel input, string userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{taskId}", input, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int taskId, string userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/tasks/{taskId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MarkCompletedAsync(int taskId, string userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"/api/tasks/{taskId}/complete", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
