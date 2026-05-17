# Design: Debug scroll automatique

**Date:** 2026-05-18  
**Statut:** Approuvé  
**Priorité:** UX

## Problème

Le scroll automatique vers le dernier message ne fonctionne pas :
- Le scroll reste en haut au chargement de page
- Le JS manuel (`scrollTop = scrollHeight`) fonctionne
- Pas d'erreurs console
- Conclusion : problème de timing entre Blazor et le DOM

## Solution

Ajouter des logs de diagnostic pour identifier où le flux échoue, puis corriger.

### Logs à ajouter

**JS (index.html)** :
```javascript
function scrollToBottom(elementId) {
    const container = document.getElementById(elementId);
    console.log('[Scroll] Called, container:', !!container, 
                'scrollHeight:', container?.scrollHeight, 
                'clientHeight:', container?.clientHeight);
    if (container) {
        container.scrollTop = container.scrollHeight;
        console.log('[Scroll] After: scrollTop =', container.scrollTop);
    }
}
```

**Blazor (Chat.razor)** :

Dans `LoadMessages()` :
```csharp
Console.WriteLine($"[Scroll] LoadMessages: setting flag, messages count = {messages.Count}");
_shouldScrollToBottom = true;
```

Dans `OnAfterRenderAsync()` :
```csharp
if (_shouldScrollToBottom)
{
    Console.WriteLine("[Scroll] OnAfterRenderAsync: scrolling now");
    _shouldScrollToBottom = false;
    await JS.InvokeVoidAsync("scrollToBottom", "messages-container");
}
```

### Diagnostic attendu

| Scénario | Cause probable | Fix |
|----------|----------------|-----|
| Pas de log "LoadMessages" | Flag jamais set | Vérifier le flow d'appel |
| Pas de log "OnAfterRenderAsync" | StateHasChanged pas appelé | Ajouter StateHasChanged() |
| Pas de log JS "[Scroll] Called" | JS interop échoue | Vérifier IJSRuntime |
| scrollHeight = clientHeight | Pas de scroll nécessaire | Vérifier CSS overflow |
| scrollHeight > 0, scrollTop = 0 après | Le scroll est annulé | Chercher ce qui reset le scroll |

## Fichiers à modifier

| Fichier | Changement |
|---------|------------|
| `wwwroot/index.html` | Ajouter console.log dans scrollToBottom |
| `Pages/Chat.razor` | Ajouter Console.WriteLine dans LoadMessages et OnAfterRenderAsync |

## Étapes

1. Ajouter les logs
2. Tester (reload, changement de team)
3. Analyser les logs console
4. Corriger selon le diagnostic
5. Retirer les logs après fix
