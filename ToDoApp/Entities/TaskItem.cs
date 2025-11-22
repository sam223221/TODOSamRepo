using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ToDoApp.Data;

namespace ToDoApp.Entities;

public class TaskItem
{
    public int Id { get; set; }

    [MaxLength(80)]
    [Required]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsSchedulable { get; set; } = true;

    public DateTime? Schedule { get; set; } = DateTime.UtcNow;

    public bool IsRepeatable { get; set; }

    public RepeatFrequency Frequency { get; set; } = RepeatFrequency.Weekly;

    [Range(1, 99)]
    public int RepeatInterval { get; set; } = 1;

    public Priority TaskPriority { get; set; } = Priority.Medium;

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(Owner))]
    [StringLength(64)]
    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser? Owner { get; set; }

    public enum Priority
    {
        Low,
        Medium,
        High
    }

    public enum RepeatFrequency
    {
        Daily,
        Weekly,
        Monthly,
        Yearly
    }
}
