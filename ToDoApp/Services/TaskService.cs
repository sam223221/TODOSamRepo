using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToDoApp.Data;
using ToDoApp.Entities;
using ToDoApp.Models;

namespace ToDoApp.Services;

public class TaskService(ApplicationDbContext dbContext, ILogger<TaskService> logger) : ITaskService
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly ILogger<TaskService> _logger = logger;
    private const int RecurringOccurrences = 30;

    public async Task<IReadOnlyList<TaskItem>> GetUpcomingAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tasks
            .AsNoTracking()
            .Where(t => t.OwnerId == userId)
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.Schedule ?? DateTime.MaxValue)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskItem?> GetByIdAsync(int taskId, string userId, bool asTracking = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tasks.Where(t => t.OwnerId == userId && t.Id == taskId);
        return asTracking ? await query.FirstOrDefaultAsync(cancellationToken) : await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TaskItem> CreateAsync(TaskInputModel input, string userId, CancellationToken cancellationToken = default)
    {
        var template = new TaskItem
        {
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow
        };

        input.ApplyTo(template);

        var toCreate = template.IsRepeatable && template.IsSchedulable && template.Schedule.HasValue
            ? BuildRecurringTasks(template, input.RecurringCount ?? 5)
            : new List<TaskItem> { template };

        await _dbContext.Tasks.AddRangeAsync(toCreate, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return toCreate[0];
    }

    public async Task<bool> UpdateAsync(int taskId, TaskInputModel input, string userId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OwnerId == userId, cancellationToken);
        if (entity is null)
        {
            _logger.LogWarning("Attempted to update missing task {TaskId} for user {UserId}", taskId, userId);
            return false;
        }

        input.ApplyTo(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (entity.IsRepeatable && entity.IsSchedulable && entity.Schedule.HasValue)
        {
            var recurring = BuildRecurringTasks(entity, input.RecurringCount ?? 5).Skip(1).ToList();
            if (recurring.Count > 0)
            {
                var existingSchedules = await _dbContext.Tasks.AsNoTracking()
                    .Where(t => t.OwnerId == userId && t.Title == entity.Title && t.Schedule.HasValue)
                    .Select(t => t.Schedule!.Value)
                    .ToListAsync(cancellationToken);

                var existingSet = existingSchedules.ToHashSet();
                var newOnes = recurring.Where(r => r.Schedule.HasValue && !existingSet.Contains(r.Schedule.Value)).ToList();
                if (newOnes.Count > 0)
                {
                    await _dbContext.Tasks.AddRangeAsync(newOnes, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }

        return true;
    }

    public async Task<bool> DeleteAsync(int taskId, string userId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OwnerId == userId, cancellationToken);
        if (entity is null)
        {
            _logger.LogWarning("Attempted to delete missing task {TaskId} for user {UserId}", taskId, userId);
            return false;
        }

        _dbContext.Tasks.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkCompletedAsync(int taskId, string userId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OwnerId == userId, cancellationToken);
        if (entity is null)
        {
            _logger.LogWarning("Attempted to complete missing task {TaskId} for user {UserId}", taskId, userId);
            return false;
        }

        entity.IsCompleted = true;
        entity.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static List<TaskItem> BuildRecurringTasks(TaskItem template, int count)
    {
        var schedule = template.Schedule ?? DateTime.Now;
        var items = new List<TaskItem>();

        for (var i = 0; i < count; i++)
        {
            var occurrenceDate = AddInterval(schedule, template.Frequency, template.RepeatInterval, i);
            var clone = new TaskItem
            {
                OwnerId = template.OwnerId,
                Title = template.Title,
                Description = template.Description,
                IsSchedulable = template.IsSchedulable,
                Schedule = occurrenceDate,
                IsRepeatable = template.IsRepeatable,
                Frequency = template.Frequency,
                RepeatInterval = template.RepeatInterval,
                TaskPriority = template.TaskPriority,
                CreatedAt = DateTime.UtcNow
            };
            items.Add(clone);
        }

        return items;
    }

    private static DateTime AddInterval(DateTime start, TaskItem.RepeatFrequency frequency, int interval, int occurrencesAhead)
    {
        var steps = Math.Max(1, interval);
        var multiplier = occurrencesAhead * steps;
        return frequency switch
        {
            TaskItem.RepeatFrequency.Daily => start.AddDays(multiplier),
            TaskItem.RepeatFrequency.Weekly => start.AddDays(7 * multiplier),
            TaskItem.RepeatFrequency.Monthly => start.AddMonths(multiplier),
            TaskItem.RepeatFrequency.Yearly => start.AddYears(multiplier),
            _ => start
        };
    }
}
