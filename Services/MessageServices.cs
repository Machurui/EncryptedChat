using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class MessageService
{
    private readonly EncryptedChatContext _context;

    public MessageService(EncryptedChatContext context)
    {
        _context = context;
    }

    public IEnumerable<Message> GetAll()
    {
        throw new NotImplementedException();
        // Return a list of users
    }

    public Message? GetById(int id)
    {
        throw new NotImplementedException();
        // Return a user by id
    }

    public Message? Create(Message user)
    {
        throw new NotImplementedException();
        // Create a new user
    }

    public Message? Update(int id, Message user)
    {
        throw new NotImplementedException();
        // Update a user
    }

    public Message? Delete(int id)
    {
        throw new NotImplementedException();
        // Delete a user
    }   
}