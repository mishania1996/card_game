using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class CreateLobbyOnStart : MonoBehaviour
{
    // This method runs automatically when you press Play
    async void Start()
    {
        try
        {
            // Initialize and sign in to Unity's services
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Debug.Log($"Signed in as Player: {AuthenticationService.Instance.PlayerId}");

            // Define the lobby details
            string lobbyName = "auto_created_lobby";
            int maxPlayers = 4;

            // Create the lobby and get the result
            Lobby myLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers);

            // Check if the returned lobby object is null
            if (myLobby != null)
            {
                Debug.Log($"✅ Success! Created a non-null Lobby. Code: {myLobby.LobbyCode}");
            }
            else
            {
                Debug.LogError("Lobby object is null.");
            }
        }
        catch (System.Exception e)
        {
            // Catch and log any errors
            Debug.LogError($"Lobby creation failed: {e.Message}");
        }
    }
}
