using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EncryptedChat.Tests;

public class FileStorageServiceTests : IDisposable
{
    private readonly string _testBasePath;
    private readonly FileStorageService _service;

    public FileStorageServiceTests()
    {
        _testBasePath = Path.Combine(Path.GetTempPath(), $"filestorage_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testBasePath);

        var options = Options.Create(new FileStorageOptions
        {
            BasePath = _testBasePath,
            MaxFileSizeBytes = 1024 * 1024
        });

        _service = new FileStorageService(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testBasePath))
            Directory.Delete(_testBasePath, recursive: true);
        GC.SuppressFinalize(this);
    }

    #region SaveAsync

    [Fact]
    public async Task SaveAsync_SavesFileAndReturnsPath()
    {
        byte[] content = [1, 2, 3, 4, 5];
        Guid teamId = Guid.NewGuid();

        string path = await _service.SaveAsync(content, teamId);

        path.Should().NotBeNullOrEmpty();
        path.Should().Contain(teamId.ToString());
        path.Should().EndWith(".enc");
    }

    [Fact]
    public async Task SaveAsync_CreatesTeamDirectory()
    {
        byte[] content = [1, 2, 3];
        Guid teamId = Guid.NewGuid();

        await _service.SaveAsync(content, teamId);

        string teamDir = Path.Combine(_testBasePath, teamId.ToString());
        Directory.Exists(teamDir).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_FileContainsCorrectContent()
    {
        byte[] content = [10, 20, 30, 40, 50];
        Guid teamId = Guid.NewGuid();

        string path = await _service.SaveAsync(content, teamId);

        string fullPath = Path.Combine(_testBasePath, path);
        byte[] savedContent = await File.ReadAllBytesAsync(fullPath);
        savedContent.Should().BeEquivalentTo(content);
    }

    #endregion

    #region LoadAsync

    [Fact]
    public async Task LoadAsync_ReturnsFileContent()
    {
        byte[] content = [100, 200, 150];
        Guid teamId = Guid.NewGuid();
        string path = await _service.SaveAsync(content, teamId);

        byte[] loaded = await _service.LoadAsync(path);

        loaded.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task LoadAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        Func<Task> act = async () => await _service.LoadAsync($"{Guid.NewGuid()}/nonexistent.enc");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadAsync_ThrowsUnauthorizedAccessException_WhenPathTraversal()
    {
        Func<Task> act = async () => await _service.LoadAsync("../../../etc/passwd");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_DeletesFile()
    {
        byte[] content = [1, 2, 3];
        Guid teamId = Guid.NewGuid();
        string path = await _service.SaveAsync(content, teamId);

        await _service.DeleteAsync(path);

        string fullPath = Path.Combine(_testBasePath, path);
        File.Exists(fullPath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow_WhenFileDoesNotExist()
    {
        Func<Task> act = async () => await _service.DeleteAsync($"{Guid.NewGuid()}/nonexistent.enc");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_ThrowsUnauthorizedAccessException_WhenPathTraversal()
    {
        Func<Task> act = async () => await _service.DeleteAsync("../../../etc/passwd");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region Path Traversal Protection

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("team/../../../etc/passwd")]
    [InlineData("/etc/passwd")]
    public async Task LoadAsync_RejectsPathTraversalAttempts(string maliciousPath)
    {
        Func<Task> act = async () => await _service.LoadAsync(maliciousPath);

        await act.Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("team/../../../etc/passwd")]
    public async Task DeleteAsync_RejectsPathTraversalAttempts(string maliciousPath)
    {
        Func<Task> act = async () => await _service.DeleteAsync(maliciousPath);

        await act.Should().ThrowAsync<Exception>();
    }

    #endregion
}
