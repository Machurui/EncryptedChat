namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

public class User : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [Required]
    [MaxLength(100)]
    public string? LastName { get; set; }

    [Required]
    public int Level { get; set; }

    [Required]
    public string? Secret { get; set; }

    public ICollection<Team> TeamsAsAdmin { get; set; } = new List<Team>();
    public ICollection<Team> TeamsAsMember { get; set; } = new List<Team>();

}

// Affichage vers le client
public class UserDTOPublic
{
    public string? Id { get; set; }
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

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

