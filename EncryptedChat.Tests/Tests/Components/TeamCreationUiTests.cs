namespace EncryptedChat.Tests.Tests.Components;

using System.Reflection;
using EncryptedChat.Client.Pages.Components;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

public class TeamCreationUiTests
{
    [Fact]
    public async Task ModalStaysOpenAndShowsErrorWhenCreationFails()
    {
        int closeCount = 0;
        NewTeamModal modal = CreateModal(
            _ => Task.FromResult(NewTeamModal.CreateResult.Fail("Creation failed.")),
            () => closeCount++);

        SetField(modal, "teamName", "Secure team");
        await InvokeCreateTeamAsync(modal);

        closeCount.Should().Be(0);
        GetField<string?>(modal, "createError").Should().Be("Creation failed.");
        GetField<bool>(modal, "isCreating").Should().BeFalse();
    }

    [Fact]
    public async Task ModalClosesAndResetsWhenCreationSucceeds()
    {
        int closeCount = 0;
        NewTeamModal modal = CreateModal(
            _ => Task.FromResult(NewTeamModal.CreateResult.Ok()),
            () => closeCount++);

        SetField(modal, "teamName", "Secure team");
        await InvokeCreateTeamAsync(modal);

        closeCount.Should().Be(1);
        GetField<string>(modal, "teamName").Should().BeEmpty();
        GetField<string?>(modal, "createError").Should().BeNull();
        GetField<bool>(modal, "isCreating").Should().BeFalse();
    }

    [Theory]
    [InlineData("TeamCreated")]
    [InlineData("TeamMemberAdded")]
    [InlineData("DirectMessageCreated")]
    public void SignalRMembershipHandlersDoNotAwaitJoinOnTheirOwnConnection(string eventName)
    {
        string chat = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "Pages", "Chat.razor"));
        string handler = ExtractSignalRHandler(chat, eventName);

        handler.Should().Contain("_ = JoinTeamGroupAsync(");
        handler.Should().NotContain("await hubConnection.InvokeAsync(\"JoinTeam\"");
    }

    [Fact]
    public void CreationCallbackDoesNotWaitForTeamReloadOrSelection()
    {
        string chat = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "Pages", "Chat.razor"));
        string callback = ExtractBetween(
            chat,
            "private async Task<NewTeamModal.CreateResult> HandleCreateTeam",
            "private async Task CompleteTeamCreationAsync");

        callback.Should().Contain("_ = CompleteTeamCreationAsync(result.Value.Id);");
        callback.Should().NotContain("await LoadTeams()");
        callback.Should().NotContain("await SelectTeam(");
    }

    [Fact]
    public void TeamSelectionConvertsUnhandledLoadingStatesToErrors()
    {
        string chat = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "Pages", "Chat.razor"));
        string selection = ExtractBetween(
            chat,
            "private async Task SelectTeam(Guid teamId)",
            "private async Task LoadTeamDetails()");

        selection.Should().Contain("ClientTelemetry.CaptureError(ex, \"chat.select-team\")");
        selection.Should().Contain("if (membersState == LoadState.Loading)");
        selection.Should().Contain("membersState = LoadState.Error;");
        selection.Should().Contain("if (messagesState == LoadState.Loading)");
        selection.Should().Contain("messagesState = LoadState.Error;");
    }

    private static NewTeamModal CreateModal(
        Func<NewTeamModal.CreateRequest, Task<NewTeamModal.CreateResult>> onCreate,
        Action onClose)
    {
        NewTeamModal modal = new();
        SetProperty(modal, nameof(NewTeamModal.OnCreate), onCreate);
        SetProperty(modal, nameof(NewTeamModal.OnClose), EventCallback.Factory.Create(new object(), onClose));
        return modal;
    }

    private static async Task InvokeCreateTeamAsync(NewTeamModal modal)
    {
        MethodInfo method = typeof(NewTeamModal).GetMethod(
            "CreateTeam",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(NewTeamModal).FullName, "CreateTeam");

        Task invocation = (Task)(method.Invoke(modal, null)
            ?? throw new InvalidOperationException("CreateTeam did not return a task."));
        await invocation;
    }

    private static void SetField<T>(NewTeamModal modal, string fieldName, T value)
    {
        FieldInfo field = typeof(NewTeamModal).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(NewTeamModal).FullName, fieldName);

        field.SetValue(modal, value);
    }

    private static void SetProperty<T>(NewTeamModal modal, string propertyName, T value)
    {
        PropertyInfo property = typeof(NewTeamModal).GetProperty(propertyName)
            ?? throw new MissingMemberException(typeof(NewTeamModal).FullName, propertyName);

        property.SetValue(modal, value);
    }

    private static T GetField<T>(NewTeamModal modal, string fieldName)
    {
        FieldInfo field = typeof(NewTeamModal).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(NewTeamModal).FullName, fieldName);

        return (T)field.GetValue(modal)!;
    }

    private static string ExtractSignalRHandler(string source, string eventName)
    {
        int eventIndex = source.IndexOf($"(\"{eventName}\"", StringComparison.Ordinal);
        eventIndex.Should().BeGreaterThanOrEqualTo(0);

        int start = source.LastIndexOf("hubConnection.On<", eventIndex, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        int next = source.IndexOf("\n            hubConnection.On<", eventIndex, StringComparison.Ordinal);
        next.Should().BeGreaterThan(start);
        return source[start..next];
    }

    private static string ExtractBetween(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);
        return source[start..end];
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
