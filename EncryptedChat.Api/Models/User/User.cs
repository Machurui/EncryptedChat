namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

public class User : IdentityUser
{
    [Required]
    [MaxLength(100)]
    public string? FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string? LastName { get; set; } = string.Empty;

    [Required]
    public int Level { get; set; } = 0;

    [Required]
    public string? Secret { get; set; } = string.Empty;

    public ICollection<Team> TeamsAsAdmin { get; set; } = [];
    public ICollection<Team> TeamsAsMember { get; set; } = [];

}