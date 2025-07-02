using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;

public class LobbyManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text lobbyNameText;
    public Button leaveLobbyButton;
    public Button readyButton;
    public Button startGameButton;
    public Transform playerListContent; // The "Content" object of your scroll view
    public GameObject playerItemPrefab; // The prefab for a single player row

    [Header("Scripts")]
    public GameFlow gameFlow;

    private Lobby currentLobby;
    private bool isPlayerReady = false;

    void Awake()
    {

        Debug.Log("A LobbyManager instance has AWAKENED.", this.gameObject);
        // Hook up the OnClick events for each button to their corresponding methods
        readyButton.onClick.AddListener(OnReadyButtonClicked);
        leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        startGameButton.onClick.AddListener(OnStartGameButtonClicked);
    }

    private void OnEnable()
    {
        // Start listening for lobby updates when this panel becomes active
        if (currentLobby != null)
        {
            HeartbeatAndPollLobby();
        }
    }

    private void OnDisable()
    {
    }

    // This is called by ConnectionManagerUI to give this script the lobby data
    public void SetCurrentLobby(Lobby lobby)
    {

        if (lobby == null)
        {
            Debug.LogError("CRITICAL ERROR: SetCurrentLobby was called, but the lobby object passed to it was NULL!");
        }
        else
        {
            Debug.Log($"LobbyManager received lobby successfully. Name: {lobby.Name}, ID: {lobby.Id}");
        }

        Debug.Log($"This LobbyManager has received the lobby data.", this.gameObject);


        currentLobby = lobby;



        // When we first enter the lobby, immediately draw the UI
        RedrawPlayerList();

        // If this script is already active, start the update coroutine
        if (gameObject.activeInHierarchy)
        {
            HeartbeatAndPollLobby();
        }
    }

    private async void HeartbeatAndPollLobby()
    {
        // Continue polling as long as we are in a lobby
        while (this != null && currentLobby != null)
        {
            try
            {
                // Send a heartbeat ping to keep the lobby alive
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);

                // Poll for the latest lobby data
                Lobby updatedLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                currentLobby = updatedLobby;

                // Redraw the UI with the new data
                RedrawPlayerList();

                // Wait for 2 seconds before the next poll
                await System.Threading.Tasks.Task.Delay(2000);
            }
            catch (LobbyServiceException e)
            {
                Debug.Log($"Lobby polling stopped due to error: {e}");
                currentLobby = null; // Stop the loop if an error occurs
            }
        }
    }

    private void RedrawPlayerList()
    {
        // Clear the existing list of player items
        foreach (Transform child in playerListContent)
        {
            Destroy(child.gameObject);
        }

        // Update the lobby name text
        lobbyNameText.text = currentLobby.Name;

        bool allPlayersReady = true;

        // Create a new UI item for each player in the lobby
        foreach (Player player in currentLobby.Players)
        {
            GameObject playerItemInstance = Instantiate(playerItemPrefab, playerListContent);
            TMP_Text playerNameText = playerItemInstance.transform.Find("PlayerNameText").GetComponent<TMP_Text>();
            TMP_Text readyStatusText = playerItemInstance.transform.Find("ReadyStatusText").GetComponent<TMP_Text>();



            // Get the player name from their lobby data
            playerNameText.text = player.Data["PlayerName"].Value;

            if (player.Data.ContainsKey("IsReady") && player.Data["IsReady"].Value == "true")
            {
                readyStatusText.text = "Ready";
                readyStatusText.color = Color.green;
            }
            else
            {
                readyStatusText.text = "Not Ready";
                readyStatusText.color = Color.yellow;
                allPlayersReady = false; // If we find anyone not ready, set this to false
            }
        }

        bool isHost = currentLobby.HostId == AuthenticationService.Instance.PlayerId;
        startGameButton.gameObject.SetActive(isHost); // Show the button only if I am the host

        // The host can only click "Start" if all players are ready
        if(isHost)
        {
            startGameButton.interactable = allPlayersReady;
        }
    }



    public async void OnLeaveLobbyClicked()
    {
        Debug.Log($"'Leave Lobby' button was clicked. This message is from the LobbyManager on object:", this.gameObject);
        Debug.Log("Attempting to leave lobby...");

        // Store a temporary reference to the lobby we are leaving
        Lobby lobbyToLeave = currentLobby;

        // This is the new way to stop the polling loop.
        // The 'while (currentLobby != null)' condition in HeartbeatAndPollLobby will become false.
        currentLobby = null;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            // Use the temporary reference to leave the correct lobby
            await LobbyService.Instance.RemovePlayerAsync(lobbyToLeave.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log($"Error leaving lobby: {e}");
        }

        // Shutdown the network connection
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Reload the scene to go back to the connection screen
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public async void OnReadyButtonClicked()
    {
        // Toggle the local ready status
        isPlayerReady = !isPlayerReady;

        try
        {
            // Update this player's data in the lobby
            string playerId = AuthenticationService.Instance.PlayerId;
            UpdatePlayerOptions options = new UpdatePlayerOptions();
            options.Data = new Dictionary<string, PlayerDataObject>()
            {
                {
                    "IsReady", new PlayerDataObject(
                        visibility: PlayerDataObject.VisibilityOptions.Member,
                        value: isPlayerReady.ToString().ToLower())
                }
            };

            await LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id, playerId, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void OnStartGameButtonClicked()
    {
        // This button should only be clickable by the host, but we double-check here.
        if (currentLobby.HostId != AuthenticationService.Instance.PlayerId) return;

        try
        {
            Debug.Log("Host is starting the game...");

            // Lock the lobby to prevent new players from joining mid-game
            await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions { IsLocked = true });

            // Tell all clients to start the game
            gameFlow.StartGameClientRpc();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
}
