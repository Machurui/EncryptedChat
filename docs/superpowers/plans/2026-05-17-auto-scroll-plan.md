# Auto-Scroll Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix auto-scroll to reliably show the last message using Blazor's OnAfterRenderAsync lifecycle

**Architecture:** Use a `_shouldScrollToBottom` flag that handlers set, and OnAfterRenderAsync checks after DOM update to perform the actual scroll

**Tech Stack:** Blazor WebAssembly, JavaScript interop

---

## File Structure

| File | Responsibility |
|------|----------------|
| `wwwroot/index.html` | Contains `scrollToBottom` JS function |
| `Pages/Chat.razor` | Blazor component with scroll logic |

---

### Task 1: Simplify scrollToBottom JavaScript

**Files:**
- Modify: `EncryptedChat.Client/wwwroot/index.html:29-67`

- [ ] **Step 1: Replace scrollToBottom function with simplified version**

Replace the existing `scrollToBottom` function and remove `initAutoScroll`:

```javascript
function scrollToBottom(elementId) {
    const container = document.getElementById(elementId);
    if (container) {
        container.scrollTop = container.scrollHeight;
    }
}
```

Remove lines 41-67 entirely (the `initAutoScroll` function and MutationObserver).

- [ ] **Step 2: Verify the change**

Open `index.html` and confirm:
- `scrollToBottom` is simple (4 lines)
- `initAutoScroll` and `messagesObserver` are gone

- [ ] **Step 3: Commit**

```bash
git add EncryptedChat.Client/wwwroot/index.html
git commit -m "Simplify scrollToBottom JS, remove MutationObserver"
```

---

### Task 2: Remove Task.Delay scroll calls in LoadMessages

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor:3099-3102`

- [ ] **Step 1: Locate LoadMessages method**

Find the `LoadMessages()` method around line 3080.

- [ ] **Step 2: Remove Task.Delay and direct scroll call**

In `LoadMessages()`, change:

```csharp
messages = (result.Value ?? []).OrderBy(m => m.Date).ToList();
teamMessages[selectedTeamId.Value] = messages;
_shouldScrollToBottom = true;
await InvokeAsync(StateHasChanged);
await Task.Delay(200);
await JS.InvokeVoidAsync("scrollToBottom", "messages-container");
```

To:

```csharp
messages = (result.Value ?? []).OrderBy(m => m.Date).ToList();
teamMessages[selectedTeamId.Value] = messages;
_shouldScrollToBottom = true;
StateHasChanged();
```

The flag + StateHasChanged triggers re-render, then OnAfterRenderAsync handles scroll.

- [ ] **Step 3: Verify the change**

Confirm the method no longer has `Task.Delay` or direct `scrollToBottom` call.

- [ ] **Step 4: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "Remove Task.Delay scroll in LoadMessages, use flag"
```

---

### Task 3: Remove Task.Delay scroll in ReceiveMessage handler

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor:3151-3198`

- [ ] **Step 1: Locate ReceiveMessage handler**

Find the `hubConnection.On<ChatClient.MessageDTOPublic>("ReceiveMessage", ...)` handler around line 3135.

- [ ] **Step 2: Replace scroll logic with flag**

Change the section that adds message to current team view:

From:
```csharp
if (message.TeamId == selectedTeamId)
{
    Console.WriteLine("[SignalR] ReceiveMessage: Message is for current team, adding to view");
    if (!messages.Any(m => m.Id == message.Id))
    {
        messages.Add(message);
        await InvokeAsync(StateHasChanged);
    }
}
```

To:
```csharp
if (message.TeamId == selectedTeamId)
{
    Console.WriteLine("[SignalR] ReceiveMessage: Message is for current team, adding to view");
    if (!messages.Any(m => m.Id == message.Id))
    {
        messages.Add(message);
        _shouldScrollToBottom = true;
        await InvokeAsync(StateHasChanged);
    }
}
```

- [ ] **Step 3: Remove the separate scroll block at end of handler**

Remove lines around 3194-3198:
```csharp
if (message.TeamId == selectedTeamId)
{
    await Task.Delay(100);
    await JS.InvokeVoidAsync("scrollToBottom", "messages-container");
}
```

This block is no longer needed since we set the flag above.

- [ ] **Step 4: Verify the change**

Confirm:
- `_shouldScrollToBottom = true` is set when adding message
- No `Task.Delay` or direct `scrollToBottom` call in handler

- [ ] **Step 5: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "Remove Task.Delay scroll in ReceiveMessage, use flag"
```

---

### Task 4: Remove Task.Delay scroll in AttachmentAdded handler

**Files:**
- Modify: `EncryptedChat.Client/Pages/Chat.razor:3201-3234`

- [ ] **Step 1: Locate AttachmentAdded handler**

Find the `hubConnection.On<AttachmentAddedEvent>("AttachmentAdded", ...)` handler around line 3201.

- [ ] **Step 2: Replace scroll logic with flag**

Change:
```csharp
if (message != null)
{
    message.Attachments ??= new List<AttachmentClient.AttachmentDTOPublic>();
    if (!message.Attachments.Any(a => a.Id == evt.Attachment.Id))
    {
        message.Attachments.Add(evt.Attachment);
        await InvokeAsync(StateHasChanged);

        // Wait for image to render then scroll
        await Task.Delay(150);
        await JS.InvokeVoidAsync("scrollToBottom", "messages-container");
    }
}
```

To:
```csharp
if (message != null)
{
    message.Attachments ??= new List<AttachmentClient.AttachmentDTOPublic>();
    if (!message.Attachments.Any(a => a.Id == evt.Attachment.Id))
    {
        message.Attachments.Add(evt.Attachment);
        _shouldScrollToBottom = true;
        await InvokeAsync(StateHasChanged);
    }
}
```

- [ ] **Step 3: Verify the change**

Confirm:
- `_shouldScrollToBottom = true` is set when adding attachment
- No `Task.Delay` or direct `scrollToBottom` call

- [ ] **Step 4: Commit**

```bash
git add EncryptedChat.Client/Pages/Chat.razor
git commit -m "Remove Task.Delay scroll in AttachmentAdded, use flag"
```

---

### Task 5: Build and test

**Files:**
- Test: `EncryptedChat.Client/`

- [ ] **Step 1: Build the client**

```bash
dotnet build EncryptedChat.Client
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 2: Manual test checklist**

Run the client and API, then test:

1. **Page load/reload (F5)** — Messages should scroll to bottom
2. **Login** — After login, chat should show last message
3. **Change team** — Switching teams should scroll to bottom
4. **Send message** — Sent message appears at bottom, scrolled into view
5. **Receive message (SignalR)** — New message from another user scrolls into view
6. **Send/receive image** — Images scroll into view

- [ ] **Step 3: Commit final state if needed**

If any adjustments were made during testing:
```bash
git add -A
git commit -m "Fix auto-scroll edge cases"
```
