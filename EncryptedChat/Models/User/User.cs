namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
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