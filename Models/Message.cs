using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class Message
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    [Required]
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser? Sender { get; set; }
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
