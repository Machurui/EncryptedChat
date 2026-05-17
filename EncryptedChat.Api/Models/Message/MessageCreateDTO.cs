using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class MessageCreateDTO
{
    [MaxLength(4000)]
    public string Text { get; set; } = string.Empty;

    [Required]
    public Guid Team { get; set; }
}
