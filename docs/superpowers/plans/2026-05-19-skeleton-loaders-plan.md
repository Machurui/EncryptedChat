# Skeleton Loaders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace boolean loading flags with a unified `LoadingState` wrapper that switches between skeleton, error (with retry), empty, and success templates across four async-loading zones in `Chat.razor`.

**Architecture:** A `LoadState` enum drives a generic `LoadingState.razor` component that switches between four `RenderFragment` templates. Skeleton primitives (`SkeletonBox`, `SkeletonText`) use a CSS-only shimmer animation; composed skeletons (`SkeletonTeamRow`, `SkeletonMessage`, `SkeletonFriendRow`, `SkeletonMemberRow`) mirror the real component layout to avoid layout shift. `Chat.razor` is refactored to replace `isLoadingMessages` with four `LoadState` variables (one per zone) plus their error messages.

**Tech Stack:** Blazor WebAssembly (.NET 8), Razor components, vanilla CSS animations.

**Reference spec:** `docs/superpowers/specs/2026-05-19-skeleton-loaders-design.md`

---

### Task 1: Create LoadState enum

**Files:**
- Create: `EncryptedChat.Client/Models/LoadState.cs`

- [ ] **Step 1: Create the enum file**

```csharp
namespace EncryptedChat.Client.Models;

public enum LoadState
{
    Loading,
    Error,
    Empty,
    Success
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Models/LoadState.cs
git commit -m "feat(client): add LoadState enum for async UI states"
```

---

### Task 2: Add `Models` namespace to `_Imports.razor`

**Files:**
- Modify: `EncryptedChat.Client/_Imports.razor`

- [ ] **Step 1: Add the using directive**

Append to `EncryptedChat.Client/_Imports.razor` (after the last `@using` line):

```razor
@using EncryptedChat.Client.Models
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/_Imports.razor
git commit -m "feat(client): import Models namespace globally for Razor"
```

---

### Task 3: Add skeleton CSS variables and base classes

**Files:**
- Modify: `EncryptedChat.Client/wwwroot/css/app.css`

- [ ] **Step 1: Add skeleton CSS variables**

In `EncryptedChat.Client/wwwroot/css/app.css`, find the `:root` selector at the top of the file (it should contain `--accent`, etc.). Add at the end of that block (just before the closing `}`):

```css
    --skeleton-bg: rgba(255, 255, 255, 0.06);
    --skeleton-shimmer: rgba(255, 255, 255, 0.10);
```

- [ ] **Step 2: Append base skeleton classes at end of file**

Append at the end of `EncryptedChat.Client/wwwroot/css/app.css`:

```css
/* ─────────────── Skeleton Loaders ─────────────── */

.skeleton {
    position: relative;
    overflow: hidden;
    background: var(--skeleton-bg);
    border-radius: 6px;
}

.skeleton::after {
    content: "";
    position: absolute;
    inset: 0;
    background: linear-gradient(
        90deg,
        transparent 0%,
        var(--skeleton-shimmer) 50%,
        transparent 100%
    );
    transform: translateX(-100%);
    animation: skeleton-shimmer 1.4s ease-in-out infinite;
}

@keyframes skeleton-shimmer {
    100% { transform: translateX(100%); }
}

.skeleton-circle { border-radius: 50%; }
.skeleton-text   { height: 12px; border-radius: 4px; }

.skeleton-text-group > .skeleton-text + .skeleton-text {
    margin-top: 6px;
}

.skeleton-mt-3 { margin-top: 3px; }
.skeleton-mt-4 { margin-top: 4px; }
.skeleton-mb-4 { margin-bottom: 4px; }

.skeleton-team-row,
.skeleton-friend-row,
.skeleton-member-row {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 10px;
}

.skeleton-team-row-content,
.skeleton-friend-row-content,
.skeleton-member-row-content {
    flex: 1;
    min-width: 0;
}

.skeleton-message {
    display: flex;
    gap: 10px;
    padding: 6px 12px;
    margin-bottom: 8px;
}

.skeleton-message.own {
    justify-content: flex-end;
}

.skeleton-message-content {
    display: flex;
    flex-direction: column;
}

/* ─────────────── LoadingState error template ─────────────── */

.loading-state-error {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 10px;
    padding: 30px 20px;
    text-align: center;
    color: rgba(255, 255, 255, 0.65);
}

.loading-state-error-icon {
    width: 32px;
    height: 32px;
    color: rgba(239, 68, 68, 0.85);
}

.loading-state-error-message {
    font-size: 13px;
}

.loading-state-retry-btn {
    padding: 6px 14px;
    border-radius: 8px;
    border: 0.5px solid rgba(255, 255, 255, 0.18);
    background: rgba(255, 255, 255, 0.08);
    color: white;
    font-size: 12px;
    cursor: pointer;
    transition: background 0.12s;
}

.loading-state-retry-btn:hover {
    background: rgba(255, 255, 255, 0.14);
}

@media (prefers-reduced-motion: reduce) {
    .skeleton::after {
        animation: none;
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add EncryptedChat.Client/wwwroot/css/app.css
git commit -m "feat(client): add skeleton shimmer CSS + LoadingState error styles"
```

---

### Task 4: Create `SkeletonBox` primitive

**Files:**
- Create: `EncryptedChat.Client/Pages/Components/Skeleton/SkeletonBox.razor`

- [ ] **Step 1: Create the component**

```razor
<div class="skeleton @CssClass"
     style="width: @Width; height: @Height; border-radius: @BorderRadius;"></div>

@code {
    [Parameter] public string Width { get; set; } = "100%";
    [Parameter] public string Height { get; set; } = "16px";
    [Parameter] public string BorderRadius { get; set; } = "6px";
    [Parameter] public string CssClass { get; set; } = "";
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Components/Skeleton/SkeletonBox.razor
git commit -m "feat(client): add SkeletonBox primitive component"
```

---

### Task 5: Create `SkeletonText` primitive

**Files:**
- Create: `EncryptedChat.Client/Pages/Components/Skeleton/SkeletonText.razor`

- [ ] **Step 1: Create the component**

```razor
<div class="skeleton-text-group">
    @for (int i = 0; i < Lines; i++)
    {
        var isLast = i == Lines - 1;
        <div class="skeleton skeleton-text"
             style="width: @(isLast && Lines > 1 ? LastLineWidth : "100%");"></div>
    }
</div>

@code {
    [Parameter] public int Lines { get; set; } = 1;
    [Parameter] public string LastLineWidth { get; set; } = "65%";
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Components/Skeleton/SkeletonText.razor
git commit -m "feat(client): add SkeletonText primitive for multi-line text"
```

---

### Task 6: Create `LoadingState` wrapper component

**Files:**
- Create: `EncryptedChat.Client/Pages/Components/Skeleton/LoadingState.razor`

- [ ] **Step 1: Create the component**

```razor
@switch (Status)
{
    case LoadState.Loading:
        <div aria-busy="true">
            @SkeletonTemplate
        </div>
        break;

    case LoadState.Error:
        @if (ErrorTemplate != null)
        {
            @ErrorTemplate
        }
        else
        {
            <div class="loading-state-error">
                <svg class="loading-state-error-icon" viewBox="0 0 24 24" fill="none"
                     stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10"/>
                    <line x1="12" y1="8" x2="12" y2="12"/>
                    <line x1="12" y1="16" x2="12.01" y2="16"/>
                </svg>
                <div class="loading-state-error-message">
                    @(string.IsNullOrWhiteSpace(ErrorMessage) ? "Something went wrong." : ErrorMessage)
                </div>
                @if (OnRetry.HasDelegate)
                {
                    <button class="loading-state-retry-btn" @onclick="OnRetry">Retry</button>
                }
            </div>
        }
        break;

    case LoadState.Empty:
        @EmptyTemplate
        break;

    case LoadState.Success:
        @SuccessTemplate
        break;
}

@code {
    [Parameter, EditorRequired] public LoadState Status { get; set; } = LoadState.Loading;
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public EventCallback OnRetry { get; set; }

    [Parameter, EditorRequired] public RenderFragment SkeletonTemplate { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment SuccessTemplate { get; set; } = default!;
    [Parameter, EditorRequired] public RenderFragment EmptyTemplate { get; set; } = default!;
    [Parameter] public RenderFragment? ErrorTemplate { get; set; }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Components/Skeleton/LoadingState.razor
git commit -m "feat(client): add LoadingState wrapper with 4-state templates"
```

---

### Task 7: Create `SkeletonTeamRow` composition

**Files:**
- Create: `EncryptedChat.Client/Pages/Components/Skeleton/SkeletonTeamRow.razor`

- [ ] **Step 1: Create the component**

```razor
<div class="skeleton-team-row">
    <SkeletonBox Width="32px" Height="32px" BorderRadius="10px" />
    <div class="skeleton-team-row-content">
        <SkeletonBox Width="70%" Height="12px" />
        <SkeletonBox Width="40%" Height="10px" CssClass="skeleton-mt-4" />
    </div>
</div>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Components/Skeleton/SkeletonTeamRow.razor
git commit -m "feat(client): add SkeletonTeamRow composition"
```

---

### Task 8: Create `SkeletonFriendRow` composition

**Files:**
- Create: `EncryptedChat.Client/Pages/Components/Skeleton/SkeletonFriendRow.razor`

- [ ] **Step 1: Create the component**

```razor
<div class="skeleton-friend-row">
    <SkeletonBox Width="34px" Height="34px" BorderRadius="50%" />
    <div class="skeleton-friend-row-content">
        <SkeletonBox Width="60%" Height="12px" />
        <SkeletonBox Width="35%" Height="10px" CssClass="skeleton-mt-4" />
    </div>
</div>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Components/Skeleton/SkeletonFriendRow.razor
git commit -m "feat(client): add SkeletonFriendRow composition"
```

---

### Task 9: Create `SkeletonMessage` composition

**Files:**
- Create: `EncryptedChat.Client/Pages/Components/Skeleton/SkeletonMessage.razor`

- [ ] **Step 1: Create the component**

```razor
<div class="skeleton-message @(IsOwn ? "own" : "")">
    @if (!IsOwn)
    {
        <SkeletonBox Width="30px" Height="30px" BorderRadius="50%" />
    }
    <div class="skeleton-message-content">
        @if (!IsOwn)
        {
            <SkeletonBox Width="80px" Height="11px" CssClass="skeleton-mb-4" />
        }
        <SkeletonBox Width="@BubbleWidth" Height="36px" BorderRadius="14px" />
    </div>
</div>

@code {
    [Parameter] public bool IsOwn { get; set; }
    [Parameter] public string BubbleWidth { get; set; } = "220px";
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Components/Skeleton/SkeletonMessage.razor
git commit -m "feat(client): add SkeletonMessage composition (own/other variants)"
```

---

### Task 10: Create `SkeletonMemberRow` composition

**Files:**
- Create: `EncryptedChat.Client/Pages/Components/Skeleton/SkeletonMemberRow.razor`

- [ ] **Step 1: Create the component**

```razor
<div class="skeleton-member-row">
    <SkeletonBox Width="30px" Height="30px" BorderRadius="50%" />
    <div class="skeleton-member-row-content">
        <SkeletonBox Width="75%" Height="11px" />
        <SkeletonBox Width="45%" Height="9px" CssClass="skeleton-mt-3" />
    </div>
</div>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Components/Skeleton/SkeletonMemberRow.razor
git commit -m "feat(client): add SkeletonMemberRow composition"
```

---

### Task 11: Add `LoadState` fields and refactor `LoadTeams`

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (state declaration block + `LoadTeams` method around line 2994)

- [ ] **Step 1: Add state fields**

Find the `@code {` block in `Chat.razor`. After the existing private field declaration `private bool isLoadingMessages;` (around line 2768), add the following block. **Do not remove `isLoadingMessages` yet** — Task 13 removes it after refactoring `LoadMessages`:

```csharp
    // Skeleton loader states
    private LoadState teamsState = LoadState.Loading;
    private LoadState friendsState = LoadState.Loading;
    private LoadState messagesState = LoadState.Loading;
    private LoadState membersState = LoadState.Loading;
    private string? teamsError;
    private string? friendsError;
    private string? messagesError;
    private string? membersError;
```

- [ ] **Step 2: Replace the `LoadTeams` method**

Replace the entire `LoadTeams` method (around line 2994) with:

```csharp
    private async Task LoadTeams()
    {
        if (string.IsNullOrEmpty(_userId))
        {
            teamsState = LoadState.Error;
            teamsError = "User session not loaded.";
            return;
        }

        if (!teams.Any()) teamsState = LoadState.Loading;
        teamsError = null;

        try
        {
            var result = await TeamClient.GetTeamsByUserAsync(_userId);
            if (!result.Success)
            {
                teamsState = LoadState.Error;
                teamsError = result.ErrorMessage ?? "Failed to load teams.";
                return;
            }
            teams = result.Value ?? [];
            teamsState = teams.Any() ? LoadState.Success : LoadState.Empty;
        }
        catch (Exception ex)
        {
            teamsState = LoadState.Error;
            teamsError = ex.Message;
        }
    }
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): add LoadState fields and refactor LoadTeams"
```

---

### Task 12: Refactor `LoadFriends`

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (`LoadFriends` method around line 3033)

- [ ] **Step 1: Replace the `LoadFriends` method**

Replace the entire `LoadFriends` method with:

```csharp
    private async Task LoadFriends()
    {
        if (!friends.Any()) friendsState = LoadState.Loading;
        friendsError = null;

        try
        {
            var friendsResult = await FriendClient.GetFriendsAsync();
            if (!friendsResult.Success)
            {
                friendsState = LoadState.Error;
                friendsError = friendsResult.ErrorMessage ?? "Failed to load friends.";
                return;
            }
            friends = friendsResult.Value ?? [];

            var requestsResult = await FriendClient.GetPendingRequestsAsync();
            if (requestsResult.Success)
            {
                pendingRequests = requestsResult.Value ?? [];
            }

            friendsState = friends.Any() ? LoadState.Success : LoadState.Empty;
        }
        catch (Exception ex)
        {
            friendsState = LoadState.Error;
            friendsError = ex.Message;
        }
    }
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): refactor LoadFriends with LoadState"
```

---

### Task 13: Refactor `LoadMessages` to use `LoadState`

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (`LoadMessages` method around line 3339)

> **Note:** This task does NOT remove the `isLoadingMessages` field. The field is still referenced by markup at line 534. Both the field declaration and that markup reference will be removed in Task 17.

- [ ] **Step 1: Replace the `LoadMessages` method**

Replace the entire `LoadMessages` method (around line 3339) with:

```csharp
    private async Task LoadMessages(bool scrollToBottom = false)
    {
        if (!selectedTeamId.HasValue) return;

        messagesState = LoadState.Loading;
        messagesError = null;

        try
        {
            var result = await ChatClient.GetMessagesByTeamAsync(selectedTeamId.Value);
            if (!result.Success)
            {
                messagesState = LoadState.Error;
                messagesError = result.ErrorMessage ?? "Failed to load messages.";
                return;
            }

            messages = (result.Value ?? []).OrderBy(m => m.Date).ToList();
            teamMessages[selectedTeamId.Value] = messages;
            messagesState = messages.Any() ? LoadState.Success : LoadState.Empty;

            if (scrollToBottom)
            {
                Console.WriteLine("[Scroll] LoadMessages: scrolling to bottom");
                _shouldScrollToBottom = true;
                _isFirstLoad = false;
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            messagesState = LoadState.Error;
            messagesError = ex.Message;
        }
    }
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors. The `isLoadingMessages` field is still declared and referenced by markup; that markup will be replaced in Task 17.

> **Transient behavior between Task 13 and Task 17:** `isLoadingMessages` is no longer set, so the `@if (isLoadingMessages)` branch at line 534 never fires. Between these two commits, the UI temporarily shows "No messages yet" (instead of "Loading messages...") during a message load. This is resolved in Task 17.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): refactor LoadMessages to use messagesState"
```

---

### Task 14: Refactor `LoadTeamDetails` for members state

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (`LoadTeamDetails` method around line 3327)

- [ ] **Step 1: Replace the `LoadTeamDetails` method**

Replace the entire `LoadTeamDetails` method with:

```csharp
    private async Task LoadTeamDetails()
    {
        if (!selectedTeamId.HasValue) return;

        membersState = LoadState.Loading;
        membersError = null;

        try
        {
            var result = await TeamClient.GetTeamDetailsAsync(selectedTeamId.Value);
            if (!result.Success)
            {
                membersState = LoadState.Error;
                membersError = result.ErrorMessage ?? "Failed to load team details.";
                return;
            }

            if (result.Value == null)
            {
                membersState = LoadState.Empty;
                return;
            }

            teamDetails = result.Value;
            isAdmin = teamDetails.Members?.Any(m => m.User?.Id == _userId && m.Role == "Admin") ?? false;
            membersState = (teamDetails.Members != null && teamDetails.Members.Any())
                ? LoadState.Success
                : LoadState.Empty;
        }
        catch (Exception ex)
        {
            membersState = LoadState.Error;
            membersError = ex.Message;
        }
    }
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): refactor LoadTeamDetails with members LoadState"
```

---

### Task 15: Wrap teams sidebar list with `LoadingState`

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (markup around line 133, the `<div class="team-list">` after the Teams section header)

- [ ] **Step 1: Replace the team-list markup**

Find the `@* Team list *@` block (around line 132-155). It currently looks like:

```razor
            @* Team list *@
            <div class="team-list">
                @foreach (var team in FilteredTeams)
                {
                    var unreadCount = GetUnreadCount(team.Id);
                    var hasUnread = unreadCount > 0;
                    <button class="team-row @(team.Id == selectedTeamId ? "active" : "") @(hasUnread ? "unread" : "")" @onclick="() => SelectTeam(team.Id)">
                        <div class="team-glyph" style="background: linear-gradient(135deg, @team.Color, @GetSecondaryColor(team.Color));">
                            @team.Glyph
                        </div>
                        <div class="team-info">
                            <div class="team-row-header">
                                <span class="team-name @(hasUnread ? "bold" : "")">@team.Name</span>
                                <span class="team-time">@GetLastActiveTime(team.Id)</span>
                            </div>
                            <div class="team-preview @(hasUnread ? "unread" : "")">@GetLastMessagePreview(team.Id)</div>
                        </div>
                        @if (hasUnread)
                        {
                            <span class="unread-badge">@unreadCount</span>
                        }
                    </button>
                }
            </div>
```

Replace it with:

```razor
            @* Team list *@
            <div class="team-list">
                <LoadingState Status="@teamsState" ErrorMessage="@teamsError" OnRetry="LoadTeams">
                    <SkeletonTemplate>
                        @for (int i = 0; i < 5; i++)
                        {
                            <SkeletonTeamRow />
                        }
                    </SkeletonTemplate>
                    <EmptyTemplate>
                        <div class="empty-section">
                            <div class="empty-text">No teams yet</div>
                        </div>
                    </EmptyTemplate>
                    <SuccessTemplate>
                        @foreach (var team in FilteredTeams)
                        {
                            var unreadCount = GetUnreadCount(team.Id);
                            var hasUnread = unreadCount > 0;
                            <button class="team-row @(team.Id == selectedTeamId ? "active" : "") @(hasUnread ? "unread" : "")" @onclick="() => SelectTeam(team.Id)">
                                <div class="team-glyph" style="background: linear-gradient(135deg, @team.Color, @GetSecondaryColor(team.Color));">
                                    @team.Glyph
                                </div>
                                <div class="team-info">
                                    <div class="team-row-header">
                                        <span class="team-name @(hasUnread ? "bold" : "")">@team.Name</span>
                                        <span class="team-time">@GetLastActiveTime(team.Id)</span>
                                    </div>
                                    <div class="team-preview @(hasUnread ? "unread" : "")">@GetLastMessagePreview(team.Id)</div>
                                </div>
                                @if (hasUnread)
                                {
                                    <span class="unread-badge">@unreadCount</span>
                                }
                            </button>
                        }
                    </SuccessTemplate>
                </LoadingState>
            </div>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): wrap teams sidebar list with LoadingState"
```

---

### Task 16: Wrap friends list with `LoadingState`

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (markup around line 252, the `else` branch inside `<div class="friends-list-container">` that shows All/Online friends)

- [ ] **Step 1: Replace the friends `else` branch**

Find the section starting at `else` (around line 251, inside `friends-list-container` and below `if (friendsTab == "requests")`). It currently looks like:

```razor
                else
                {
                    @* All / Online friends list *@
                    @foreach (var friend in GetFilteredFriendsByTab())
                    {
                        <div class="friend-row">
                            ...
                        </div>
                    }
                }
```

Replace the entire `else { ... }` block with:

```razor
                else
                {
                    @* All / Online friends list *@
                    <LoadingState Status="@friendsState" ErrorMessage="@friendsError" OnRetry="LoadFriends">
                        <SkeletonTemplate>
                            @for (int i = 0; i < 4; i++)
                            {
                                <SkeletonFriendRow />
                            }
                        </SkeletonTemplate>
                        <EmptyTemplate>
                            <div class="empty-section">
                                <div class="empty-text">No friends yet</div>
                            </div>
                        </EmptyTemplate>
                        <SuccessTemplate>
                            @foreach (var friend in GetFilteredFriendsByTab())
                            {
                                <div class="friend-row">
                                    <div class="friend-avatar-wrapper">
                                        <Avatar Name="@friend.Name" ImageUrl="@GetFullImageUrl(friend.ProfileImageUrl)" Size="32" />
                                        <span class="status-dot-small @GetFriendDisplayStatus(friend.Status)"></span>
                                    </div>
                                    <div class="friend-row-content">
                                        <div class="friend-row-name" style="color: @friend.NameColor;">@friend.Name</div>
                                        <div class="friend-row-meta">
                                            <span class="friend-status-indicator @GetFriendDisplayStatus(friend.Status)"></span>
                                            @if (IsStatusOnline(friend.Status))
                                            {
                                                @if (!string.IsNullOrEmpty(friend.StatusMessage))
                                                {
                                                    <span>@friend.StatusMessage</span>
                                                }
                                                else
                                                {
                                                    <span>@(friend.Status == "online" ? "Online" : friend.Status == "away" ? "Away" : "Do not disturb")</span>
                                                }
                                            }
                                            else
                                            {
                                                <span>@(friend.LastSeenAt.HasValue ? GetRelativeTimeFromDate(friend.LastSeenAt.Value) : "Offline")</span>
                                            }
                                        </div>
                                    </div>
                                    <button class="icon-btn" title="Message" @onclick="() => StartDirectMessage(friend.UserId)">
                                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                            <line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/>
                                        </svg>
                                    </button>
                                    <div class="friend-more-menu">
                                        <button class="icon-btn" title="More" @onclick="() => ToggleFriendMenu(friend.UserId)" @onclick:stopPropagation="true">
                                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                                <circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/><circle cx="5" cy="12" r="1"/>
                                            </svg>
                                        </button>
                                        @if (openFriendMenuId == friend.UserId)
                                        {
                                            <div class="friend-dropdown">
                                                <button class="friend-dropdown-item danger" @onclick="() => RemoveFriend(friend.UserId)" @onclick:stopPropagation="true">
                                                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                                        <path d="M16 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="8.5" cy="7" r="4"/><line x1="18" y1="8" x2="23" y2="13"/><line x1="23" y1="8" x2="18" y2="13"/>
                                                    </svg>
                                                    Remove friend
                                                </button>
                                            </div>
                                        }
                                    </div>
                                </div>
                            }
                        </SuccessTemplate>
                    </LoadingState>
                }
```

> **Note:** This task replaces only the `else` branch (All / Online tabs). The `if (friendsTab == "requests")` branch is NOT modified — requests already have their own empty handling.

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): wrap friends list (All/Online) with LoadingState"
```

---

### Task 17: Wrap messages list with `LoadingState` and remove `isLoadingMessages`

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (markup around line 534 AND field declaration around line 2768)

- [ ] **Step 1: Remove the `isLoadingMessages` field declaration**

Find and delete the line in the `@code` block (around line 2768):

```csharp
    private bool isLoadingMessages;
```

- [ ] **Step 2: Replace the messages loading/empty/foreach block**

Find the block that currently starts at `@if (isLoadingMessages)` (around line 534) through the end of the closing brace of the `else { @foreach (...) ... }` block. It currently looks like:

```razor
                    @if (isLoadingMessages)
                    {
                        <div class="loading-messages">Loading messages...</div>
                    }
                    else if (!messages.Any())
                    {
                        <div class="no-messages">
                            <div>No messages yet</div>
                            <div class="no-messages-sub">Start the conversation!</div>
                        </div>
                    }
                    else
                    {
                        @foreach (var (msg, index) in FilteredMessages.Select((m, i) => (m, i)))
                        {
                            ...all the message rendering markup...
                        }
                    }
```

Replace it with the following — preserving the full inner `@foreach` body (do not modify the message rendering itself):

```razor
                    <LoadingState Status="@messagesState" ErrorMessage="@messagesError" OnRetry="() => LoadMessages(true)">
                        <SkeletonTemplate>
                            <SkeletonMessage IsOwn="false" BubbleWidth="240px" />
                            <SkeletonMessage IsOwn="true"  BubbleWidth="180px" />
                            <SkeletonMessage IsOwn="false" BubbleWidth="280px" />
                            <SkeletonMessage IsOwn="false" BubbleWidth="160px" />
                            <SkeletonMessage IsOwn="true"  BubbleWidth="220px" />
                            <SkeletonMessage IsOwn="false" BubbleWidth="200px" />
                        </SkeletonTemplate>
                        <EmptyTemplate>
                            <div class="no-messages">
                                <div>No messages yet</div>
                                <div class="no-messages-sub">Start the conversation!</div>
                            </div>
                        </EmptyTemplate>
                        <SuccessTemplate>
                            @foreach (var (msg, index) in FilteredMessages.Select((m, i) => (m, i)))
                            {
                                @* PRESERVE the existing message rendering here — do not modify the inner body. *@
                            }
                        </SuccessTemplate>
                    </LoadingState>
```

**IMPORTANT:** In your actual edit, do NOT replace the `@foreach` body with a comment. Keep the existing per-message rendering code from the original `else { @foreach (...) { ... } }` block exactly as it is, just move it inside `<SuccessTemplate>`. The comment above is only there to indicate the boundary in this plan.

To make this edit safely, use the following procedure with your Edit tool:
1. Find the `@if (isLoadingMessages) {` line and capture text up to the matching outer `else { @foreach ... }`.
2. Wrap that whole region with the new `<LoadingState>` structure, moving the existing `@foreach (...) { ... }` body verbatim into `<SuccessTemplate>`.
3. The `@if (isLoadingMessages)` branch and the `else if (!messages.Any())` branch are dropped (replaced by `SkeletonTemplate` and `EmptyTemplate`).

- [ ] **Step 3: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors. The `isLoadingMessages` field is now fully removed (declaration in Step 1, last usage replaced in Step 2).

- [ ] **Step 4: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): wrap messages list with LoadingState, remove isLoadingMessages"
```

---

### Task 18: Wrap members rail with `LoadingState`

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor` (markup around line 814, `<div class="members-list">`)

- [ ] **Step 1: Replace the members-list markup**

Find the current `<div class="members-list">` block (around line 814):

```razor
            <div class="members-list">
                @if (teamDetails?.Members != null)
                {
                    @foreach (var member in teamDetails.Members.OrderByDescending(m => m.Role == "Admin"))
                    {
                        <div class="member-row">
                            <Avatar Name="@member.User?.Name" ImageUrl="@GetFullImageUrl(member.User?.ProfileImageUrl)" Size="30" />
                            <div class="member-info">
                                <div class="member-name" style="color: @(member.User?.NameColor ?? "#FFFFFF");">@member.User?.Name</div>
                                <div class="member-status @(member.Role == "Admin" ? "admin" : "")">
                                    @(member.Role == "Admin" ? "Admin" : "Member")
                                </div>
                            </div>
                        </div>
                    }
                }
            </div>
```

Replace it with:

```razor
            <div class="members-list">
                <LoadingState Status="@membersState" ErrorMessage="@membersError" OnRetry="LoadTeamDetails">
                    <SkeletonTemplate>
                        @for (int i = 0; i < 4; i++)
                        {
                            <SkeletonMemberRow />
                        }
                    </SkeletonTemplate>
                    <EmptyTemplate>
                        <div class="empty-section">
                            <div class="empty-text">No members</div>
                        </div>
                    </EmptyTemplate>
                    <SuccessTemplate>
                        @if (teamDetails?.Members != null)
                        {
                            @foreach (var member in teamDetails.Members.OrderByDescending(m => m.Role == "Admin"))
                            {
                                <div class="member-row">
                                    <Avatar Name="@member.User?.Name" ImageUrl="@GetFullImageUrl(member.User?.ProfileImageUrl)" Size="30" />
                                    <div class="member-info">
                                        <div class="member-name" style="color: @(member.User?.NameColor ?? "#FFFFFF");">@member.User?.Name</div>
                                        <div class="member-status @(member.Role == "Admin" ? "admin" : "")">
                                            @(member.Role == "Admin" ? "Admin" : "Member")
                                        </div>
                                    </div>
                                </div>
                            }
                        }
                    </SuccessTemplate>
                </LoadingState>
            </div>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build EncryptedChat.Client`
Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "feat(client): wrap members rail with LoadingState"
```

---

### Task 19: Final build, tests, and manual verification

**Files:**
- All previously modified files.

- [ ] **Step 1: Full solution build**

Run: `dotnet build EncryptedChat.sln`
Expected: Build succeeded with 0 errors.

- [ ] **Step 2: Run all tests**

Run: `dotnet test EncryptedChat.Tests`
Expected: All existing tests pass (no test count regression).

- [ ] **Step 3: Manual visual verification**

Start both servers in separate terminals:

```bash
# Terminal 1
cd EncryptedChat.Api && dotnet run --launch-profile https

# Terminal 2
cd EncryptedChat.Client && dotnet run --launch-profile https
```

Verify each item:

1. **Initial load** — On hard refresh of `/chat`, the teams sidebar shows 5 shimmering team-row skeletons, then transitions to real teams.
2. **Friends tab skeleton** — Click the "Friends" tab. The friends list shows 4 shimmering friend-row skeletons, then transitions to real friends.
3. **Messages skeleton on team change** — Click a different team in the sidebar. The messages area shows 6 alternating own/other message skeletons, then transitions to real messages.
4. **Members rail skeleton** — When opening a team, the right-side members rail shows 4 shimmering member-row skeletons, then transitions to real members.
5. **Empty state** — In an empty team (or by clearing messages temporarily), confirm the "No messages yet" empty template appears (not the skeleton).
6. **Error + Retry** — In browser DevTools → Network tab, set throttling to "Offline". Refresh `/chat`. Confirm:
   - Each affected zone shows the error icon + message + Retry button.
   - Click Retry while still offline → error persists.
   - Set network back to "Online" → click Retry → success state appears.
7. **No layout shift** — During the skeleton → success transition, confirm the layout does not jump (skeletons should roughly match real component dimensions).
8. **Reduced motion** — In DevTools → Rendering panel, enable `prefers-reduced-motion: reduce`. Refresh `/chat`. Confirm the shimmer animation is paused (skeletons still show, but without the sliding gradient).
9. **Accessibility** — Inspect a Loading zone in DevTools and verify the outer `<div>` has `aria-busy="true"`.

- [ ] **Step 4: Commit any final integration fixes (if needed)**

If you discover issues during manual verification, fix them and commit:

```bash
git add -A
git commit -m "fix(client): post-integration adjustments for skeleton loaders"
```

If no fixes were needed, skip this step.
