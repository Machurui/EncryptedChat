namespace EncryptedChat.Models;

public class AttachmentDTOPublic
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool SignatureVerified { get; set; }
}
