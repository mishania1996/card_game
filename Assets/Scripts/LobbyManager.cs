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
    public ConnectionManagerUI connectionManagerUI;


    private Lobby currentLobby;
    private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();
    private bool isPlayerReady = false;
    private bool isLeaving = false;
    private Coroutine heartbeatCoroutine;
    private Coroutine pollCoroutine;

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
            StartLobbyUpdates();
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        }
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
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
            StartLobbyUpdates();
        }
    }

    private void StartLobbyUpdates()
    {
        // Starts two separate loops: one for heartbeats, one for polling.
        if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
        if (pollCoroutine != null) StopCoroutine(pollCoroutine);

        heartbeatCoroutine = StartCoroutine(HeartbeatLobbyCoroutine());
        pollCoroutine = StartCoroutine(PollLobbyCoroutine());
    }

    private IEnumerator HeartbeatLobbyCoroutine()
    {
        while (currentLobby != null)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            yield return new WaitForSeconds(15f);
        }
    }

    private IEnumerator PollLobbyCoroutine()
    {
        while (currentLobby != null)
        {
            var getLobbyTask = LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            yield return new WaitUntil(() => getLobbyTask.IsCompleted);

            try
            {
                Lobby updatedLobby = getLobbyTask.Result;
                currentLobby = updatedLobby;
                RedrawPlayerList(); // Redraw UI with fresh data
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"Failed to poll lobby: {e.InnerException.Message}");
                currentLobby = null; // Stop polling on error
            }
            yield return new WaitForSeconds(2f);
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
        startGameButton.gameObject.SetActive(isHost);

        // The host can only click "Start" if all players are ready
        if(isHost)
        {
            bool isLobbyFull = currentLobby.Players.Count == currentLobby.MaxPlayers;
            startGameButton.interactable = allPlayersReady && isLobbyFull;
        }
    }



    public async void OnLeaveLobbyClicked()
    {
        Debug.Log($"'Leave Lobby' button was clicked. This message is from the LobbyManager on object:", this.gameObject);
        Debug.Log("Attempting to leave lobby...");

        // Store a temporary reference to the lobby we are leaving
        Lobby lobbyToLeave = currentLobby;
        isLeaving = true;
        currentLobby = null;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(lobbyToLeave.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log($"Error leaving lobby: {e}");
        }

        // Shutdown the network connection
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
        {
            NetworkManager.Singleton.Shutdown();
        }

        gameObject.SetActive(false);
        connectionManagerUI.connectionPanel.SetActive(true);
        connectionManagerUI.gamePanel.SetActive(false);
        ReturnToMenu();
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

            gameFlow.ServerSideGameStart();

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


    public void HideLobby()
    {
        // A simple command to disable the GameObject this script is attached to.
        gameObject.SetActive(false);
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        // We only care if we are a client that has been disconnected by the host.
        if (NetworkManager.Singleton.IsHost) return;

        // If the disconnect was not because we clicked the "Leave" button, treat it as being kicked.
        if (!isLeaving)
        {
            Debug.Log("Lost connection to the host. Returning to menu.");
            ReturnToMenu();
        }
    }

    private void ReturnToMenu()
    {
        // Stop the lobby polling loops.
        currentLobby = null;

        // Shut down the local network connection.
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsHost))
        {
            NetworkManager.Singleton.Shutdown();
        }

        // Reset the flag and switch UI panels.
        isLeaving = false;
        gameObject.SetActive(false);
        connectionManagerUI.connectionPanel.SetActive(true);
        connectionManagerUI.gamePanel.SetActive(false);
    }

    public void UpdatePlayerScore(ulong clientId, int scoreChange)
    {
        // Ensure the player has a score entry
        if (!playerScores.ContainsKey(clientId))
        {
            playerScores[clientId] = 0;
        }

        playerScores[clientId] += scoreChange;

        // For now, we'll log the score change to the server's console.
        Debug.Log($"SERVER: Player {clientId}'s score is now {playerScores[clientId]}");
    }
}
