namespace EncryptedChat.Models;

public record PinnedMessageDTO(
    Guid Id,
    Guid MessageId,
    MessageDTOPublic Message,
    string PinnedById,
    string PinnedByName,
    DateTime PinnedAt
);
