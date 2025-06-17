using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class TaskComment : IHasId
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid TaskId { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsEdited { get; set; } = false;
    
    // Navigation properties
    public virtual ProjectTask Task { get; set; } = null!;
    public virtual ApplicationUser Author { get; set; } = null!;
    public virtual TaskComment? ParentComment { get; set; }
    public virtual ICollection<TaskComment> Replies { get; set; } = new List<TaskComment>();
}