// protected override async Task OnInitializedAsync()
// {
//     await LoadUsersAsync();
// }



// // Users loaded from API
// private List<UserClient.UserDTOPublic> availableUsers = new();

// // Selected IDs for Admins / Members
// private HashSet<string> selectedAdminIds = new();
// private HashSet<string> selectedMemberIds = new();

// private async Task LoadUsersAsync()
// {
//     var result = await UserClient.GetUsersAsync();

//     if (!result.Success)
//     {
//         errorMessage = result.ErrorMessage;
//         return;
//     }

//     availableUsers = result.Value ?? new List<UserClient.UserDTOPublic>();
//     foreach (var item in availableUsers)
//     {
//         Console.WriteLine("users : " + item);
//     }
// }