namespace EncryptedChat.Models;

public record SessionDTO(
    Guid Id,
    string DeviceInfo,
    string DeviceKind,
    string? Location,
    string? IpAddress,
    DateTime CreatedAt,
    DateTime LastActiveAt,
    bool IsCurrent
);

public record SessionListDTO(
    int TotalCount,
    IReadOnlyList<SessionDTO> Sessions
);
