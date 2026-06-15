namespace EncryptedChat.Models;

public record TeamInviteDTO(string Token, DateTime ExpiresAt);
public record TeamInviteListItemDTO(Guid Id, string Token, DateTime CreatedAt, DateTime ExpiresAt);
public record InvitePreviewDTO(Guid TeamId, string TeamName);
