using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ToDoApp.Entities;

namespace ToDoApp.Data;

public class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [MaxLength(256)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(40)]
    public string ThemePreference { get; set; } = "sprout";

    public TimeSpan? DailyDigestTime { get; set; }

    public bool ReceiveReminders { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
