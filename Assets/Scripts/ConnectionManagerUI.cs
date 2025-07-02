using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Multiplayer;
using TMPro; // For TextMeshPro UI elements
using UnityEngine.UI;

public class ConnectionManagerUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject connectionPanel;
    public GameObject gamePanel;
    public GameObject connectingStatusPanel;

    [Header("Lobby UI")]
    public GameObject lobbyPanel;
    public TMP_InputField playerNameInputField;
    public TMP_InputField roomNameInputField;
    public TMP_Dropdown playerCountDropdown;
    public Button createRoomButton;


    [Header("Lobby List UI")]
    public Button refreshLobbiesButton;
    public GameObject lobbyItemPrefab; // The prefab for a single row in the list
    public Transform lobbyListContent; // The "Content" GameObject inside your ScrollView


    [Header("Scripts")]
    public CardManager cardManager;
    public LobbyManager lobbyManager;

    // Add or modify the Awake() method in ConnectionManagerUI.cs
    private void Awake()
    {
        if (lobbyManager == null)
        {
            Debug.LogError("In ConnectionManagerUI, the LobbyManager reference is NULL!", this.gameObject);
        }
        else
        {
            Debug.Log("In ConnectionManagerUI, the LobbyManager reference is assigned.", this.gameObject);
        }
    }

    async void Start()
    {
        connectionPanel.SetActive(true);
        gamePanel.SetActive(false);
        connectingStatusPanel.SetActive(false);

        createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        refreshLobbiesButton.onClick.AddListener(OnRefreshLobbiesClicked);

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        Debug.Log($"Player is signed in with ID: {AuthenticationService.Instance.PlayerId}");
    }

  private async void OnCreateRoomClicked()
    {
        string selectedPlayerCountText = playerCountDropdown.options[playerCountDropdown.value].text;
        int maxPlayers = int.Parse(selectedPlayerCountText);

        // Show a "Connecting..." or "Creating..." screen
        connectingStatusPanel.SetActive(true);
        connectionPanel.SetActive(false);

        // Create a Relay allocation ---
        // This gets a join code that we will hide inside our new lobby.
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        // --- Step 2: Set up the Lobby options ---
        CreateLobbyOptions options = new CreateLobbyOptions();
        options.Player = new Player { Data = new Dictionary<string, PlayerDataObject>() };

        // We can add player data like their name. "S" means String.
        options.Player.Data.Add("PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerNameInputField.text));

        // The lobby's public data will hold our Relay join code.
        // This is how clients will find the Relay server to connect to.
        options.Data = new Dictionary<string, DataObject>()
        {
            {
                "JoinCode", new DataObject(
                    visibility: DataObject.VisibilityOptions.Public,
                    value: joinCode)
            }
        };

        // --- Step 3: Create the Lobby ---
        Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(roomNameInputField.text, maxPlayers, options);
        Debug.Log($"ConnectionManagerUI is calling SetCurrentLobby on the object named: '{lobbyManager.gameObject.name}'", lobbyManager.gameObject);
    lobbyManager.SetCurrentLobby(lobby);


        // --- Step 4: Start the Host using the Relay data ---
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartHost();

        // --- Step 5: Transition to the Lobby UI ---
        // You would switch to a new UI panel here where the host can see connected players
        // and a "Start Game" button. For now, we can just go to the main game UI.
        connectingStatusPanel.SetActive(false);
        lobbyPanel.SetActive(true);

        Debug.Log($"Successfully created lobby '{lobby.Name}' with code '{lobby.LobbyCode}' and Relay join code '{joinCode}'");


    }


    private async void OnRefreshLobbiesClicked()
    {
        Debug.Log("Refreshing lobby list...");

        // Query the Lobby service for a list of all public lobbies
        QueryLobbiesOptions options = new QueryLobbiesOptions();
        options.Filters = new List<QueryFilter>()
        {
            // We only want to see lobbies that are not full
            new QueryFilter(
                field: QueryFilter.FieldOptions.AvailableSlots,
                op: QueryFilter.OpOptions.GT,
                value: "0")
        };

        QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);

        // First, clear out the old list of lobbies
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        // Now, create a new UI element for each lobby found
        foreach (Lobby lobby in queryResponse.Results)
        {
            GameObject lobbyItemInstance = Instantiate(lobbyItemPrefab, lobbyListContent);

            // Get the UI elements from the prefab instance
            // Note: .Find() is simple, but for larger projects, a dedicated script on the prefab is better.
            TMP_Text roomNameText = lobbyItemInstance.transform.Find("RoomName").GetComponent<TMP_Text>();
            TMP_Text playerCountText = lobbyItemInstance.transform.Find("PlayerCount").GetComponent<TMP_Text>();
            Button joinButton = lobbyItemInstance.transform.Find("JoinButton").GetComponent<Button>();

            // Populate the UI elements with the lobby's information
            roomNameText.text = lobby.Name;
            playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

            // Add a listener to the join button to call our join method, passing this specific lobby
            joinButton.onClick.AddListener(() => OnJoinLobbyClicked(lobby));
        }
    }

    private async void OnJoinLobbyClicked(Lobby lobby)
    {
        Debug.Log($"Attempting to join lobby {lobby.Name} ({lobby.Id})");

        // Show a "Connecting..." screen
        connectingStatusPanel.SetActive(true);
        connectionPanel.SetActive(false);

        // Join the selected lobby
        Lobby joiningLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);

        // Get the Relay join code from the lobby's public data
        string joinCode = joiningLobby.Data["JoinCode"].Value;

        Debug.Log($"Retrieved Relay join code: {joinCode}");

        // Join the Relay allocation using the retrieved code
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // Configure the transport and start the client
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartClient();
    }



    
    // This method now only handles switching panels.
    public void ShowGameUI()
    {
        if (connectingStatusPanel != null) connectingStatusPanel.SetActive(false);

        // These lines hide the connection UI and show the game UI
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);
    }
}
