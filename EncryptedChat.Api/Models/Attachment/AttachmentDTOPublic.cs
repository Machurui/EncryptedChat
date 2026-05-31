namespace EncryptedChat.Models;

// Wire shape returned to clients. EncryptedFileName / FileNameIv / FileIv /
// Signature are opaque blobs produced by the client. Clients must decrypt
// the filename and verify the signature locally before showing the
// attachment to the user.
public class AttachmentDTOPublic
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string EncryptedFileName { get; set; } = string.Empty;
    public string FileNameIv { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string FileIv { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int KeyGeneration { get; set; }
    public DateTime CreatedAt { get; set; }
}
