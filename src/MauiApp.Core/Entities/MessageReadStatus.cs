using MauiApp.Core.Data;

namespace MauiApp.Core.Entities;

public class MessageReadStatus : IHasId
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ChatMessage Message { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}