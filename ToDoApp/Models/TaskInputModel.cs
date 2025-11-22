using System;
using System.ComponentModel.DataAnnotations;
using ToDoApp.Entities;

namespace ToDoApp.Models;

public class TaskInputModel
{
    [Required]
    [MaxLength(80)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsSchedulable { get; set; } = true;

    public DateTime? Schedule { get; set; } = DateTime.Now;

    public bool IsRepeatable { get; set; }

    public TaskItem.RepeatFrequency Frequency { get; set; } = TaskItem.RepeatFrequency.Weekly;

    [Required]
    [Range(1, 99, ErrorMessage = "Repeat interval must be between 1 and 99.")]
    public int? RepeatInterval { get; set; } = 1;

    [Required]
    [Range(1, 100, ErrorMessage = "Recurring count must be between 1 and 100.")]
    public int? RecurringCount { get; set; } = 5;

    public TaskItem.Priority TaskPriority { get; set; } = TaskItem.Priority.Medium;

    public static TaskInputModel FromEntity(TaskItem entity) => new()
    {
        Title = entity.Title,
        Description = entity.Description,
        IsSchedulable = entity.IsSchedulable,
        Schedule = entity.Schedule,
        IsRepeatable = entity.IsRepeatable,
        Frequency = entity.Frequency,
        RepeatInterval = entity.RepeatInterval,
        TaskPriority = entity.TaskPriority
    };

    public void ApplyTo(TaskItem entity)
    {
        entity.Title = Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        entity.IsSchedulable = IsSchedulable;
        entity.Schedule = IsSchedulable ? (Schedule ?? DateTime.Now) : null;
        entity.IsRepeatable = IsRepeatable;
        entity.Frequency = Frequency;
        entity.RepeatInterval = RepeatInterval ?? 1;
        entity.TaskPriority = TaskPriority;
        if (!entity.IsSchedulable)
        {
            entity.Schedule = null;
        }
    }
}
