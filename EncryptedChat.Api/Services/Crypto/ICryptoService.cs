namespace EncryptedChat.Services;

public interface ICryptoService
{
    (string EncryptedText, string Iv) Encrypt(string plaintext, string teamSecret);
    string Decrypt(string encryptedText, string iv, string teamSecret);
    string Sign(string plaintext, string userSecret);
    bool Verify(string plaintext, string signature, string userSecret);
}
