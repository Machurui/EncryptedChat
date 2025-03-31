namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [Required]
    [MaxLength(100)]
    public string? LastName { get; set; }

    [Required]
    public string? Email { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Password { get; set; }

    [Required]
    public int Level { get; set; }

    public ICollection<Team> TeamsAsAdmin { get; set; } = new List<Team>();
    public ICollection<Team> TeamsAsMember { get; set; } = new List<Team>();

    [Required]
    public string? Secret { get; set; }
}

// Affichage vers le client
public class UserDTOPublic
{
    public int Id { get; set; }
    public string? FirstName { get; set; }

    [Required]
    [MaxLength(100)]
    public string? LastName { get; set; }

    [Required]
    public string? Email { get; set; }

    [Required]
    public int Level { get; set; }
}

// Utilisé lors de création et MAJ
public class UserDTO
{
    [Required]
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [Required]
    [MaxLength(100)]
    public string? LastName { get; set; }

    [Required]
    public string? Email { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Password { get; set; }
}

public class UserDTOSecret
{
    [Required]
    public string? Email { get; set; }

    [Required]
    public string? Secret { get; set; }
}

