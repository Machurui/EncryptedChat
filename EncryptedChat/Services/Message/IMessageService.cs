using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Services
{
    public interface IMessageService
    {
        Task<IEnumerable<MessageDTOPublic>?> GetAllAsync();

        Task<IEnumerable<MessageDTOPublic?>?> GetAllByTeamAsync(int id);

        Task<MessageDTOPublic?> GetByIdAsync(int id);

        Task<MessageDTOPublic?> CreateAsync(MessageDTO message);

        Task<MessageDTOPublic?> UpdateAsync(int id, MessageDTO message);

        Task<MessageDTOPublic?> DeleteAsync(int id);
    }
}