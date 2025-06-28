using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP; // This is the one that fixes the error
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using TMPro; // For TextMeshPro UI elements
using UnityEngine.UI;

public class ConnectionManagerUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject connectionPanel;
    public GameObject gamePanel;
    public GameObject connectingStatusPanel;

    [Header("Connection Buttons")]
    public Button hostButton;
    public Button clientButton;

    [Header("Connection Fields")]
    public TMP_InputField joinCodeInputField;
    public TMP_Text joinCodeText;

    [Header("In-Game Buttons")]
    public Button p1_DrawButton;
    public Button p2_DrawButton;
    public Button p1_PassButton;
    public Button p2_PassButton;

    private CardManager cardManager;

    private bool hasSetButtonVisibility = false;
    private bool hasHookedUpButtons = false;


    async void Start()
    {
        connectionPanel.SetActive(true);
        gamePanel.SetActive(false);
        connectingStatusPanel.SetActive(false);

        if (p1_DrawButton != null) p1_DrawButton.gameObject.SetActive(false);
        if (p2_DrawButton != null) p2_DrawButton.gameObject.SetActive(false);
        if (p1_PassButton != null) p1_PassButton.gameObject.SetActive(false);
        if (p2_PassButton != null) p2_PassButton.gameObject.SetActive(false);

        hostButton.onClick.AddListener(OnHostButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);

        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    void Update()
    {
        // We only need to run this logic until everything is set up.
        if (hasSetButtonVisibility && hasHookedUpButtons)
        {
            return;
        }

        // Wait until we are connected to the network.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // --- Part 1: Set button visibility ---
            if (!hasSetButtonVisibility)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    p1_DrawButton.gameObject.SetActive(true);
                    p1_PassButton.gameObject.SetActive(true);
                    p2_DrawButton.gameObject.SetActive(false);
                    p2_PassButton.gameObject.SetActive(false);
                }
                else
                {
                    p1_DrawButton.gameObject.SetActive(false);
                    p1_PassButton.gameObject.SetActive(false);
                    p2_DrawButton.gameObject.SetActive(true);
                    p2_PassButton.gameObject.SetActive(true);
                }
                hasSetButtonVisibility = true;
            }

            // --- Part 2: Hook up button listeners ---
            if (!hasHookedUpButtons)
            {
                // Find the CardManager only after we've connected.
                cardManager = FindAnyObjectByType<CardManager>();
                if (cardManager != null)
                {
                    p1_DrawButton.onClick.RemoveAllListeners();
                    p1_DrawButton.onClick.AddListener(() => cardManager.OnDrawCardButtonPressed());

                    p2_DrawButton.onClick.RemoveAllListeners();
                    p2_DrawButton.onClick.AddListener(() => cardManager.OnDrawCardButtonPressed());

                    p1_PassButton.onClick.RemoveAllListeners();
                    p1_PassButton.onClick.AddListener(() => cardManager.OnPassButtonPressed());

                    p2_PassButton.onClick.RemoveAllListeners();
                    p2_PassButton.onClick.AddListener(() => cardManager.OnPassButtonPressed());


                    hasHookedUpButtons = true;
                }
            }
        }
    }


    private async void OnHostButtonClicked()
    {
        connectingStatusPanel.SetActive(true);
        connectionPanel.SetActive(false);

        try
        {
            // 1. Create a Relay allocation (1 is the number of players besides the host)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);

            // 2. Get the join code for the allocation
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            joinCodeText.text = $"Join Code: {joinCode}";
            Debug.Log($"Host created relay with join code: {joinCode}");

            // 3. Configure the Unity Transport to use the Relay server
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls")); // "dtls" enables encryption

            // 4. Start the host
            NetworkManager.Singleton.StartHost();
            // Call your existing method to switch panels
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to start host via Relay: {e.Message}");
            connectingStatusPanel.SetActive(false);
        }
    }

    private async void OnClientButtonClicked()
    {


        string joinCode = joinCodeInputField.text;
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("Join code cannot be empty.");
            return;
        }

        try
        {
            // 1. Join the host's allocation using the join code
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Debug.Log("Client joined relay successfully.");

            // 2. Configure the Unity Transport to use the Relay server
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            // 3. Start the client
            NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to join host via Relay: {e.Message}");
            connectingStatusPanel.SetActive(false);
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.Count == 2)
        {
            Debug.Log("Connection successful! Showing Game UI.");
            ShowGameUI();
        }

    }
    
    // This method now only handles switching panels.
    private void ShowGameUI()
    {
        if (connectingStatusPanel != null) connectingStatusPanel.SetActive(false);

        // These lines hide the connection UI and show the game UI
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);
    }
}
