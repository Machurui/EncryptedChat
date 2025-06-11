namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Team
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ICollection<User>? Admins { get; set; } = [];

    public ICollection<User>? Members { get; set; } = [];

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Password { get; set; } = string.Empty;
}
