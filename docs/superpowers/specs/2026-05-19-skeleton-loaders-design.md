# Design: Skeleton Loaders for Async Data

**Date:** 2026-05-19
**Statut:** Approuvé
**Scope:** Frontend (Blazor WebAssembly)

## Résumé

Remplacer les flags booléens de chargement (`isLoadingMessages`, etc.) et les pages vides par des skeleton loaders qui reproduisent visuellement la structure finale des composants. Couvre 4 zones du Chat (teams sidebar, friends sidebar, messages list, members rail) avec gestion explicite des 4 états : Loading / Error / Empty / Success.

---

## 1. Architecture des composants

```
EncryptedChat.Client/
├── Models/
│   └── LoadState.cs
└── Pages/Components/Skeleton/
    ├── SkeletonBox.razor           # Primitive : Width, Height, BorderRadius
    ├── SkeletonText.razor          # Multi-lignes : Lines, Width
    ├── LoadingState.razor          # Wrapper état + templates
    ├── SkeletonTeamRow.razor       # Mime un TeamRow
    ├── SkeletonMessage.razor       # Mime une message-row
    ├── SkeletonFriendRow.razor     # Mime un FriendRow
    └── SkeletonMemberRow.razor     # Mime une entrée Members rail
```

**Responsabilités :**
- `SkeletonBox` / `SkeletonText` : primitives visuelles (shimmer, forme).
- `LoadingState` : logique d'état (switch entre les 4 templates).
- Compositions spécifiques : reproduisent la structure du composant final (dimensions et paddings identiques) pour éviter le layout shift.

---

## 2. Enum `LoadState`

```csharp
// Models/LoadState.cs
namespace EncryptedChat.Client.Models;

public enum LoadState
{
    Loading,
    Error,
    Empty,
    Success
}
```

---

## 3. Composant `LoadingState`

### API

```razor
<LoadingState Status="@teamsState"
              ErrorMessage="@teamsError"
              OnRetry="LoadTeams">
    <SkeletonTemplate>
        @for (int i = 0; i < 5; i++)
        {
            <SkeletonTeamRow />
        }
    </SkeletonTemplate>
    <EmptyTemplate>
        <div class="empty-state">No teams yet. Create one to get started.</div>
    </EmptyTemplate>
    <SuccessTemplate>
        @foreach (var team in teams)
        {
            <TeamRow Team="team" />
        }
    </SuccessTemplate>
</LoadingState>
```

### Paramètres

| Paramètre | Type | Description |
|-----------|------|-------------|
| `Status` | `LoadState` | État courant (requis) |
| `ErrorMessage` | `string?` | Message d'erreur affiché en état Error |
| `OnRetry` | `EventCallback` | Action du bouton Retry (masqué si `null`) |
| `SkeletonTemplate` | `RenderFragment` | Contenu Loading (requis) |
| `EmptyTemplate` | `RenderFragment` | Contenu Empty (requis) |
| `SuccessTemplate` | `RenderFragment` | Contenu Success (requis) |
| `ErrorTemplate` | `RenderFragment?` | Override du template Error |

### Comportement interne

```razor
@switch (Status)
{
    case LoadState.Loading:
        <div aria-busy="true">@SkeletonTemplate</div>
        break;
    case LoadState.Error:
        @(ErrorTemplate ?? DefaultErrorTemplate)
        break;
    case LoadState.Empty:
        @EmptyTemplate
        break;
    case LoadState.Success:
        @SuccessTemplate
        break;
}
```

**Template Error par défaut :**
```razor
<div class="loading-state-error">
    <svg class="loading-state-error-icon"><!-- alert icon --></svg>
    <div class="loading-state-error-message">@ErrorMessage</div>
    @if (OnRetry.HasDelegate)
    {
        <button class="loading-state-retry-btn" @onclick="OnRetry">Retry</button>
    }
</div>
```

---

## 4. Composants primitifs

### `SkeletonBox.razor`

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

### `SkeletonText.razor`

```razor
<div class="skeleton-text-group">
    @for (int i = 0; i < Lines; i++)
    {
        <div class="skeleton skeleton-text" style="width: @(i == Lines - 1 ? LastLineWidth : "100%");"></div>
    }
</div>

@code {
    [Parameter] public int Lines { get; set; } = 1;
    [Parameter] public string LastLineWidth { get; set; } = "65%";
}
```

---

## 5. Compositions spécifiques

### `SkeletonTeamRow.razor` — Mime un team dans la sidebar

```razor
<div class="skeleton-team-row">
    <SkeletonBox Width="32px" Height="32px" BorderRadius="10px" />
    <div class="skeleton-team-row-content">
        <SkeletonBox Width="70%" Height="12px" />
        <SkeletonBox Width="40%" Height="10px" CssClass="skeleton-mt-4" />
    </div>
</div>
```

### `SkeletonMessage.razor` — Mime une message-row

```razor
<div class="skeleton-message @(IsOwn ? "own" : "")">
    @if (!IsOwn)
    {
        <SkeletonBox Width="32px" Height="32px" BorderRadius="50%" />
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

### `SkeletonFriendRow.razor` — Mime un friend row

```razor
<div class="skeleton-friend-row">
    <SkeletonBox Width="34px" Height="34px" BorderRadius="50%" />
    <div class="skeleton-friend-row-content">
        <SkeletonBox Width="60%" Height="12px" />
        <SkeletonBox Width="35%" Height="10px" CssClass="skeleton-mt-4" />
    </div>
</div>
```

### `SkeletonMemberRow.razor` — Mime une entrée du members rail

```razor
<div class="skeleton-member-row">
    <SkeletonBox Width="28px" Height="28px" BorderRadius="50%" />
    <div class="skeleton-member-row-content">
        <SkeletonBox Width="75%" Height="11px" />
        <SkeletonBox Width="45%" Height="9px" CssClass="skeleton-mt-3" />
    </div>
</div>
```

---

## 6. CSS — Shimmer + intégration design system

### Variables (ajout à `app.css`)

```css
:root {
    --skeleton-bg: rgba(255, 255, 255, 0.06);
    --skeleton-shimmer: rgba(255, 255, 255, 0.10);
}
```

### Classe de base et animation

```css
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

/* Spacing helpers */
.skeleton-mt-3 { margin-top: 3px; }
.skeleton-mt-4 { margin-top: 4px; }
.skeleton-mb-4 { margin-bottom: 4px; }

/* Layout des compositions */
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

/* État Error par défaut */
.loading-state-error {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 10px;
    padding: 30px 20px;
    text-align: center;
    color: rgba(255,255,255,0.65);
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
    border: 0.5px solid rgba(255,255,255,0.18);
    background: rgba(255,255,255,0.08);
    color: white;
    font-size: 12px;
    cursor: pointer;
    transition: background 0.12s;
}

.loading-state-retry-btn:hover {
    background: rgba(255,255,255,0.14);
}

/* Accessibilité */
@media (prefers-reduced-motion: reduce) {
    .skeleton::after {
        animation: none;
    }
}
```

**Cohérence Liquid Glass :**
- `--skeleton-bg` = `rgba(255,255,255,0.06)` (cohérent avec `.message-action-btn`).
- Pas de blur ni saturate (les skeletons vivent à l'intérieur de `GlassPanel`).
- Border-radius alignés avec les composants réels (10px rows, 14px bulles).

---

## 7. Intégration dans `Chat.razor`

### Refactor des flags

**Supprimer :**
```csharp
private bool isLoadingMessages;
```

**Ajouter :**
```csharp
private LoadState teamsState = LoadState.Loading;
private LoadState friendsState = LoadState.Loading;
private LoadState messagesState = LoadState.Loading;
private LoadState membersState = LoadState.Loading;
private string? teamsError;
private string? friendsError;
private string? messagesError;
private string? membersError;
```

### Pattern type pour chaque chargement

```csharp
private async Task LoadTeams()
{
    if (!teams.Any()) teamsState = LoadState.Loading;
    teamsError = null;
    try
    {
        var result = await TeamClient.GetMyTeamsAsync();
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

### Cas particulier `LoadMessages`

Toujours reset à `Loading` au début (décision : skeleton à chaque changement de team) :

```csharp
private async Task LoadMessages(bool scrollToBottom = false)
{
    if (!selectedTeamId.HasValue) return;
    messagesState = LoadState.Loading;
    messagesError = null;
    try
    {
        var result = await MessageClient.GetMessagesAsync(selectedTeamId.Value);
        if (!result.Success)
        {
            messagesState = LoadState.Error;
            messagesError = result.ErrorMessage ?? "Failed to load messages.";
            return;
        }
        messages = result.Value ?? [];
        messagesState = messages.Any() ? LoadState.Success : LoadState.Empty;
        if (scrollToBottom) await ScrollToBottom();
    }
    catch (Exception ex)
    {
        messagesState = LoadState.Error;
        messagesError = ex.Message;
    }
}
```

### Wrappers dans le markup

**Teams sidebar :**
```razor
<LoadingState Status="@teamsState" ErrorMessage="@teamsError" OnRetry="LoadTeams">
    <SkeletonTemplate>
        @for (int i = 0; i < 5; i++) { <SkeletonTeamRow /> }
    </SkeletonTemplate>
    <EmptyTemplate>
        <div class="empty-state">No teams yet.</div>
    </EmptyTemplate>
    <SuccessTemplate>
        @foreach (var team in teams) { <!-- existing team-row markup --> }
    </SuccessTemplate>
</LoadingState>
```

**Friends sidebar :**
```razor
<LoadingState Status="@friendsState" ErrorMessage="@friendsError" OnRetry="LoadFriends">
    <SkeletonTemplate>
        @for (int i = 0; i < 4; i++) { <SkeletonFriendRow /> }
    </SkeletonTemplate>
    <EmptyTemplate>
        <div class="empty-state">No friends yet.</div>
    </EmptyTemplate>
    <SuccessTemplate>
        @foreach (var friend in friends) { <!-- existing friend-row markup --> }
    </SuccessTemplate>
</LoadingState>
```

**Messages list :**
```razor
<LoadingState Status="@messagesState" ErrorMessage="@messagesError" OnRetry="() => LoadMessages(true)">
    <SkeletonTemplate>
        <SkeletonMessage IsOwn="false" BubbleWidth="240px" />
        <SkeletonMessage IsOwn="true" BubbleWidth="180px" />
        <SkeletonMessage IsOwn="false" BubbleWidth="280px" />
        <SkeletonMessage IsOwn="false" BubbleWidth="160px" />
        <SkeletonMessage IsOwn="true" BubbleWidth="220px" />
        <SkeletonMessage IsOwn="false" BubbleWidth="200px" />
    </SkeletonTemplate>
    <EmptyTemplate>
        <div class="empty-state">No messages yet. Be the first to say hi.</div>
    </EmptyTemplate>
    <SuccessTemplate>
        @foreach (var msg in FilteredMessages) { <!-- existing message markup --> }
    </SuccessTemplate>
</LoadingState>
```

**Members rail :**
```razor
<LoadingState Status="@membersState" ErrorMessage="@membersError" OnRetry="LoadTeamDetails">
    <SkeletonTemplate>
        @for (int i = 0; i < 4; i++) { <SkeletonMemberRow /> }
    </SkeletonTemplate>
    <EmptyTemplate>
        <div class="empty-state">No members.</div>
    </EmptyTemplate>
    <SuccessTemplate>
        @foreach (var m in teamDetails.Members) { <!-- existing member markup --> }
    </SuccessTemplate>
</LoadingState>
```

---

## 8. Tableau récapitulatif des zones

| Page | Zone | Composant Skeleton | Nb placeholders | Source d'état |
|------|------|---------------------|-----------------|---------------|
| `Chat.razor` | Teams sidebar | `SkeletonTeamRow` | 5 | `teamsState` |
| `Chat.razor` | Friends sidebar | `SkeletonFriendRow` | 4 | `friendsState` |
| `Chat.razor` | Messages list | `SkeletonMessage` (alterné) | 6 | `messagesState` |
| `Chat.razor` | Members rail | `SkeletonMemberRow` | 4 | `membersState` |

---

## 9. Garanties de non-régression

- Pas de modification des services (`TeamClient`, `MessageClient`, `FriendsClient`).
- Pas de modification des modèles ou DTOs.
- Les bindings et formulaires existants restent intacts.
- `isLoadingMessages` et `isLoadingGifs` sont remplacés (pas dupliqués) — sauf `isLoadingGifs` qui reste car la modal GIF est hors scope.
- Les messages "empty" existants sont réutilisés dans les `EmptyTemplate`.

---

## 10. Hors scope (YAGNI)

| Élément | Raison |
|---------|--------|
| `Profile.razor` skeleton | Chargement court au démarrage, pas critique |
| `Login` / `Register` | États de bouton déjà suffisants pour le submit |
| GIFs modal | A déjà son propre indicateur, modal éphémère |
| `PinnedDropdown` | Chargement court à l'ouverture |
| Skeleton avec délai > 200ms | Complexité non justifiée |
| Animation pulse alternative | Décision : shimmer uniquement |

---

## 11. Accessibilité

- `aria-busy="true"` sur le wrapper Loading (annonce aux lecteurs d'écran que le contenu est en train de charger).
- `@media (prefers-reduced-motion: reduce)` désactive l'animation shimmer (garde le fond statique).
- Bouton Retry focusable au clavier (élément `<button>` natif).

---

## 12. Tests requis

**Tests manuels (Blazor WASM, pas de tests unitaires UI dans ce projet) :**

1. **Loading** : Rechargement de page → 4 zones affichent leurs skeletons puis transitionnent vers Success.
2. **Empty** : Compte sans teams/friends/messages → `EmptyTemplate` affiché.
3. **Error + Retry** : Simuler échec réseau (DevTools → Offline) → message d'erreur + bouton Retry fonctionnel.
4. **Changement de team** : Sélection d'une autre team → skeleton messages affiché brièvement puis remplacé.
5. **No layout shift** : Vérifier que le passage skeleton → contenu ne fait pas sauter le layout (dimensions identiques).
6. **Reduced motion** : Activer `prefers-reduced-motion` (DevTools → Rendering) → animation désactivée.

---

## 13. Fichiers à créer/modifier

### Nouveaux fichiers
| Fichier | Action |
|---------|--------|
| `Models/LoadState.cs` | Créer |
| `Pages/Components/Skeleton/SkeletonBox.razor` | Créer |
| `Pages/Components/Skeleton/SkeletonText.razor` | Créer |
| `Pages/Components/Skeleton/LoadingState.razor` | Créer |
| `Pages/Components/Skeleton/SkeletonTeamRow.razor` | Créer |
| `Pages/Components/Skeleton/SkeletonMessage.razor` | Créer |
| `Pages/Components/Skeleton/SkeletonFriendRow.razor` | Créer |
| `Pages/Components/Skeleton/SkeletonMemberRow.razor` | Créer |

### Fichiers modifiés
| Fichier | Action |
|---------|--------|
| `Pages/Chat.razor` | Refactor 4 zones + état enum + retry logic |
| `wwwroot/css/app.css` | Ajout variables, classes skeleton, animation, error state |
| `_Imports.razor` | Ajouter `@using EncryptedChat.Client.Models` si nécessaire |
