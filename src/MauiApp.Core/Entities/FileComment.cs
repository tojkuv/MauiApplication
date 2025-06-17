namespace MauiApp.Core.Entities;

public class FileComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FileId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
    public bool IsEdited { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ProjectFile File { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
    public FileComment? ParentComment { get; set; }
    public List<FileComment> Replies { get; set; } = new();
}