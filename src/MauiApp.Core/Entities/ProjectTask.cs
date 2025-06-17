using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class ProjectTask : IHasId
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.ToDo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int EstimatedHours { get; set; } = 0;
    public int ActualHours { get; set; } = 0;
    
    // Navigation properties
    public virtual Project Project { get; set; } = null!;
    public virtual ApplicationUser? Assignee { get; set; }
    public virtual ApplicationUser CreatedBy { get; set; } = null!;
    public virtual ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public virtual ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public virtual ICollection<TaskDependency> Dependencies { get; set; } = new List<TaskDependency>();
    public virtual ICollection<TaskDependency> DependentTasks { get; set; } = new List<TaskDependency>();
}

public enum TaskStatus
{
    ToDo = 1,
    InProgress = 2,
    Review = 3,
    Done = 4,
    Cancelled = 5
}

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}