using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToDoApp.Entities;

namespace ToDoApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<TaskItem>(entity =>
        {
            entity.Property(t => t.Title)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(t => t.Description)
                .HasMaxLength(1000);

            entity.Property(t => t.RepeatInterval)
                .HasDefaultValue(1);

            entity.Property(t => t.TaskPriority)
                .HasConversion<int>();

            entity.Property(t => t.Frequency)
                .HasConversion<int>();

            entity.Property(t => t.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.Property(t => t.OwnerId)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasOne(t => t.Owner)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Id)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.Property(u => u.ThemePreference)
                .HasMaxLength(40)
                .HasDefaultValue("sprout");

            entity.Property(u => u.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.HasIndex(u => u.Email)
                .IsUnique();
        });
    }
}
