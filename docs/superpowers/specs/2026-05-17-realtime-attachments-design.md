# Design: Broadcast temps réel pour messages avec attachments

**Date:** 2026-05-17  
**Statut:** Approuvé  
**Priorités:** Architecture propre, Performance temps réel

## Problème

Quand un utilisateur envoie un fichier via REST API, les autres utilisateurs ne reçoivent pas de notification SignalR en temps réel. Le flow actuel :

- `MessageController.PostMessage` : crée le message, retourne l'ID, pas de broadcast
- `AttachmentController.Upload` : attache le fichier, pas de broadcast
- Seul l'expéditeur voit son message (ajouté localement)
- Les autres doivent attendre un refresh

## Solution

Créer un `RealtimeService` dédié qui encapsule toute la logique de broadcast SignalR. Les controllers appellent ce service après les opérations de persistance.

### Comportement attendu

1. Le message est broadcasté dès sa création → tous les membres le voient immédiatement
2. L'attachment est broadcasté quand l'upload est terminé → le fichier apparaît sur le message existant
3. Deux broadcasts distincts pour une UX réactive

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENT                                   │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       │
│  │ SendMessage  │    │ ReceiveMsg   │    │ AttachmentAdd│       │
│  │ (SignalR/REST)│    │ (SignalR)    │    │ (SignalR)    │       │
│  └──────┬───────┘    └──────▲───────┘    └──────▲───────┘       │
└─────────┼───────────────────┼───────────────────┼───────────────┘
          │                   │                   │
          ▼                   │                   │
┌─────────────────────────────┼───────────────────┼───────────────┐
│                         SERVER                  │               │
│  ┌──────────────┐           │                   │               │
│  │ ChatHub      │───────────┤                   │               │
│  │ (SignalR)    │           │                   │               │
│  └──────┬───────┘           │                   │               │
│         │                   │                   │               │
│  ┌──────▼───────┐    ┌──────┴───────┐          │               │
│  │ Message      │    │ Realtime     │◄─────────┘               │
│  │ Controller   │───►│ Service      │                          │
│  └──────┬───────┘    └──────────────┘                          │
│         │                   ▲                                   │
│  ┌──────▼───────┐           │                                   │
│  │ Attachment   │───────────┘                                   │
│  │ Controller   │                                               │
│  └──────┬───────┘                                               │
│         │                                                       │
│  ┌──────▼───────┐                                               │
│  │ Services     │  (MessageService, AttachmentService - purs)   │
│  └──────────────┘                                               │
└─────────────────────────────────────────────────────────────────┘
```

## Interface IRealtimeService

```csharp
public interface IRealtimeService
{
    Task BroadcastMessageAsync(Guid teamId, MessageDTOPublic message);
    Task BroadcastAttachmentAddedAsync(Guid teamId, Guid messageId, AttachmentDTOPublic attachment);
}
```

## Implémentation RealtimeService

```csharp
public class RealtimeService(IHubContext<ChatHub> hubContext, ILogger<RealtimeService> logger) : IRealtimeService
{
    public async Task BroadcastMessageAsync(Guid teamId, MessageDTOPublic message)
    {
        try
        {
            await hubContext.Clients.Group($"team-{teamId}")
                .SendAsync("ReceiveMessage", message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast message {MessageId} to team {TeamId}", 
                message.Id, teamId);
        }
    }

    public async Task BroadcastAttachmentAddedAsync(Guid teamId, Guid messageId, AttachmentDTOPublic attachment)
    {
        try
        {
            await hubContext.Clients.Group($"team-{teamId}")
                .SendAsync("AttachmentAdded", new { MessageId = messageId, Attachment = attachment });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast attachment for message {MessageId}", messageId);
        }
    }
}
```

## Fichiers à modifier

| Fichier | Action |
|---------|--------|
| `Services/Realtime/IRealtimeService.cs` | Créer interface |
| `Services/Realtime/RealtimeService.cs` | Créer implémentation |
| `Program.cs` | Enregistrer le service DI |
| `Controllers/MessageController.cs` | Injecter et appeler après création |
| `Controllers/AttachmentController.cs` | Injecter et appeler après upload |
| `Hubs/ChatHub.cs` | Refactorer pour utiliser RealtimeService |
| `Pages/Chat.razor` (client) | Écouter `AttachmentAdded` |

## Modifications côté client

### Nouveau handler SignalR

```csharp
private record AttachmentAddedEvent(Guid MessageId, AttachmentClient.AttachmentDTOPublic Attachment);

hubConnection.On<AttachmentAddedEvent>("AttachmentAdded", async evt =>
{
    var message = messages.FirstOrDefault(m => m.Id == evt.MessageId);
    if (message != null)
    {
        message.Attachments ??= new List<AttachmentDTOPublic>();
        message.Attachments.Add(evt.Attachment);
        await InvokeAsync(StateHasChanged);
    }
});
```

### Simplification de SendMessage

Le client n'ajoute plus manuellement le message après l'upload — tout arrive via SignalR :

```csharp
if (selectedFile != null)
{
    var result = await ChatClient.CreateMessageAsync(selectedTeamId.Value, messageText);
    if (result.Success && result.Value != null)
    {
        // Message apparaît via "ReceiveMessage" (broadcast serveur)
        using var stream = selectedFile.OpenReadStream(maxAllowedSize: 26_214_400);
        await AttachmentClient.UploadAsync(result.Value.Id, stream, selectedFile.Name, selectedFile.ContentType);
        // Attachment apparaît via "AttachmentAdded" (broadcast serveur)
        selectedFile = null;
    }
    newMessageText = string.Empty;
}
```

## Gestion des erreurs

| Situation | Comportement |
|-----------|--------------|
| Upload échoue après création message | Message reste sans attachment. Utilisateur peut réessayer. |
| Utilisateur offline pendant broadcast | Recevra les messages au prochain `LoadMessages()`. |
| Broadcast échoue | Log warning, message existe en DB, visible au refresh. |
| Double broadcast | Client vérifie `messages.Any(m => m.Id == message.Id)` avant d'ajouter. |

## Flux complet avec fichier

1. Client → `POST /api/message` → MessageController
2. MessageController → MessageService.CreateAsync() → Message créé en DB
3. MessageController → RealtimeService.BroadcastMessageAsync() → **Tous voient le message**
4. Client → `POST /api/attachment` → AttachmentController
5. AttachmentController → AttachmentService.CreateAsync() → Fichier stocké
6. AttachmentController → RealtimeService.BroadcastAttachmentAddedAsync() → **Fichier apparaît**

## Flux sans fichier (via SignalR)

1. Client → SignalR `SendMessageToTeam` → ChatHub
2. ChatHub → MessageService.CreateAsync() → Message créé
3. ChatHub → RealtimeService.BroadcastMessageAsync() → **Tous voient le message**
