using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class UserService
{
    private readonly EncryptedChatContext _context;

    public UserService(EncryptedChatContext context)
    {
        _context = context;
    }

    public IEnumerable<UserDTOPublic> GetAll()
    {
        // Return a list of users
        return _context.Users
        .Select(user => ItemToDTO(user))
        .ToList();
    }
    public UserDTOPublic? GetById(string id)
    {
        // Return a user by id
        return _context.Users
        .AsNoTracking()
        .Where(user => user.Id == id)
        .Select(user => ItemToDTO(user))
        .SingleOrDefault();
    }

    public UserDTOPublic? Update(string id, UserDTO user)
    {
        // Update a user
        var existingUser = _context.Users.Find(id);
        if (existingUser == null)
            return null;

        existingUser.FirstName = user.FirstName;
        existingUser.LastName = user.LastName;
        existingUser.Email = user.Email;

        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UserExists(id))
            {
                return null;
            }
            else
            {
                throw;
            }
        }

        return ItemToDTO(existingUser);
    }

    public User? Delete(string id)
    {
        // Delete a user
        var userToDelete = _context.Users.Find(id);
        if (userToDelete == null)
            return null;
        

        _context.Users.Remove(userToDelete);
        _context.SaveChanges();

        return userToDelete;
    }

    private bool UserExists(string id)
    {
        return _context.Users.Any(e => e.Id == id);
    }

    private static UserDTOPublic ItemToDTO(User user) =>
       new UserDTOPublic
       {
           Id = user.Id,
           FirstName = user.FirstName,
           LastName = user.LastName,
           Email = user.Email,
           Level = user.Level
       };
}