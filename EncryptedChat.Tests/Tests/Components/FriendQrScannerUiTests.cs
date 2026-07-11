using FluentAssertions;

namespace EncryptedChat.Tests.Tests.Components;

public class FriendQrScannerUiTests
{
    [Fact]
    public void IndexLoadsZxingAndScannerBeforeBlazorStarts()
    {
        string index = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "wwwroot", "index.html"));

        int zxingIndex = index.IndexOf("vendor/zxing-browser.min.js", StringComparison.Ordinal);
        int scannerIndex = index.IndexOf("js/qr-scanner.js", StringComparison.Ordinal);
        int blazorIndex = index.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal);

        zxingIndex.Should().BeGreaterThan(0);
        scannerIndex.Should().BeGreaterThan(zxingIndex);
        scannerIndex.Should().BeLessThan(blazorIndex);
    }

    [Fact]
    public void ScannerStopsDecoderAndEveryCameraTrack()
    {
        string scanner = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "wwwroot", "js", "qr-scanner.js"));

        scanner.Should().Contain("session.controls?.stop()");
        scanner.Should().Contain("stream.getTracks().forEach");
        scanner.Should().Contain("track.stop()");
        scanner.Should().Contain("callbackControls.stop()");
        scanner.Should().Contain("facingMode");
        scanner.Should().Contain("decodeFromImageUrl");
    }

    [Fact]
    public void ModalConfirmsScannedProfileBeforeSendingRequest()
    {
        string modal = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "Pages", "Components", "AddFriendModal.razor"));

        modal.Should().Contain("<video @ref=\"qrVideo\"");
        modal.Should().Contain("Choose image");
        modal.Should().Contain("FriendQrPayloadCodec.TryDecode");
        modal.Should().Contain("scannedUser != null");
        modal.Should().Contain("SendRequestToUser(scannedUser.Id)");
        modal.Should().Contain("SvgQRCode.SizingMode.ViewBoxAttribute");
        modal.Should().NotContain("qr-pattern");
    }

    [Fact]
    public void VendoredScannerHasPinnedPackageAndLicense()
    {
        string package = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "package.json"));

        package.Should().Contain("\"@zxing/browser\": \"0.1.5\"");
        File.Exists(FindRepoFile(
            "EncryptedChat.Client", "wwwroot", "vendor", "zxing-browser.min.js")).Should().BeTrue();
        File.Exists(FindRepoFile(
            "EncryptedChat.Client", "wwwroot", "vendor", "zxing-browser.LICENSE.txt")).Should().BeTrue();
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        foreach (string candidateRoot in CandidateRoots())
        {
            string path = Path.Combine([candidateRoot, .. pathParts]);
            if (File.Exists(path)) return path;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(pathParts)} from test context.");
    }

    private static IEnumerable<string> CandidateRoots()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
            current = Directory.GetParent(current)?.FullName;
        }

        current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
            current = Directory.GetParent(current)?.FullName;
        }
    }
}
