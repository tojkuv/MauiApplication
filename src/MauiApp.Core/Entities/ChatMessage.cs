using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class ChatMessage : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text"; // text, file, image, system
    public Guid? ReplyToMessageId { get; set; }
    public bool IsEdited { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
    public ChatMessage? ReplyToMessage { get; set; }
    public List<ChatMessage> Replies { get; set; } = new();
    public List<MessageReaction> Reactions { get; set; } = new();
    public List<MessageReadStatus> ReadStatuses { get; set; } = new();
}