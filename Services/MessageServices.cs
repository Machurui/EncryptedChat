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

    public IEnumerable<Message> GetAllByTeam()
    {
        throw new NotImplementedException();
        // Return a list of messages
    }

    public Message? GetAllBySender(int id)
    {
        throw new NotImplementedException();
        // Return a user by id
    }

    public MessageDTOPrivate? Create(MessageDTO message)
    {
        // Create a new user
        // var team = _context.Teams.Find(message?.Team?.Id);
        // if (team == null)
        //     return null;

        // var sender = _context.Users.Find(message?.Sender?.Id);
        // if (sender == null)
        //     return null;

        // var newMessage = new Message
        // {
        //     Text = message?.Text,
        //     Sender = sender,
        //     Team = team,
        //     Date = message.Date
        // };

        // _context.Messages.Add(newMessage);
        // _context.SaveChanges();

        // return newMessage;

        throw new NotImplementedException();
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

    // private static MessageDTOPrivate ItemToDTO(Message message) =>
    //    new MessageDTOPrivate
    //    {
    //        Id = message.Id,
    //        Text = message.Text,
    //        Sender = ItemToDTO(message.Sender),
    //        Team = ItemToDTO(message.Team),
    //        Date = message.Date
    //    };
}