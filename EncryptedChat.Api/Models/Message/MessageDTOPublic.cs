using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class MessageDTOPublic
{
    public int Id { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    [Required]
    public MessageSenderDTO? Sender { get; set; }

    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public bool SignatureVerified { get; set; } = true;
}

public class MessageSenderDTO
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}