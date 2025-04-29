namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Team
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ICollection<User>? Admins { get; set; }

    public ICollection<User>? Members { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Password { get; set; }
}

// Affichage vers le client
public class TeamDTOPublic
{
    public int Id { get; set; }

    [Required]
    public ICollection<UserDTOPublic>? Admins { get; set; }

    public ICollection<UserDTOPublic>? Members { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; }
}

// Utilisé lors de création et MAJ
public class TeamDTO
{
    [Required] 
    public ICollection<string>? AdminIds { get; set; }

    public ICollection<string>? MemberIds { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Password { get; set; }
}
