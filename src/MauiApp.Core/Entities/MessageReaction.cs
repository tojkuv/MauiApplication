namespace MauiApp.Core.Entities;

public class MessageReaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public string Reaction { get; set; } = string.Empty; // emoji like "👍", "❤️", "😂", etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ChatMessage Message { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}