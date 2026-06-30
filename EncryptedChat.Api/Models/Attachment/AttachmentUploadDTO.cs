namespace EncryptedChat.Models;

public class AttachmentUploadDTO
{
    public byte[] EncryptedContent { get; set; } = [];
    public string EncryptedFileName { get; set; } = string.Empty;
    public string FileNameIv { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string FileIv { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int KeyGeneration { get; set; }
}