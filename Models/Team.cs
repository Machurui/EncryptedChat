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

public class TeamDTOPrivate
{
    public int Id { get; set; }

    [Required]
    public ICollection<UserDTOPrivate>? Admins { get; set; }

    public ICollection<UserDTOPrivate>? Members { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; }
}

public class TeamDTO
{
    [Required]
    public ICollection<UserDTOAdd>? Admins { get; set; }

    public ICollection<UserDTOAdd>? Members { get; set; }

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Password { get; set; }
}
