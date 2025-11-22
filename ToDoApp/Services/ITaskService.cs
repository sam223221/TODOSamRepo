using ToDoApp.Entities;
using ToDoApp.Models;

namespace ToDoApp.Services;

public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> GetUpcomingAsync(string userId, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetByIdAsync(int taskId, string userId, bool asTracking = false, CancellationToken cancellationToken = default);
    Task<TaskItem> CreateAsync(TaskInputModel input, string userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int taskId, TaskInputModel input, string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int taskId, string userId, CancellationToken cancellationToken = default);
    Task<bool> MarkCompletedAsync(int taskId, string userId, CancellationToken cancellationToken = default);
}
