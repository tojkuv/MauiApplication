namespace MauiApp.Core.Entities;

public class TaskDependency
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid DependsOnTaskId { get; set; }
    public DependencyType Type { get; set; } = DependencyType.FinishToStart;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ProjectTask Task { get; set; } = null!;
    public virtual ProjectTask DependsOnTask { get; set; } = null!;
}

public enum DependencyType
{
    FinishToStart = 1,
    StartToStart = 2,
    FinishToFinish = 3,
    StartToFinish = 4
}