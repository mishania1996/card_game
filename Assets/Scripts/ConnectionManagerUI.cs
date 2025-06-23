using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class ConnectionManagerUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject connectionPanel;
    public GameObject gamePanel;

    [Header("Connection Buttons")]
    public Button hostButton;
    public Button clientButton;

    [Header("In-Game Buttons")]
    public Button p1_DrawButton;
    public Button p2_DrawButton;

    private CardManager cardManager;

    private bool hasSetButtonVisibility = false;
    private bool hasHookedUpButtons = false;

    void Start()
    {
        connectionPanel.SetActive(true);
        gamePanel.SetActive(false);

        if (p1_DrawButton != null) p1_DrawButton.gameObject.SetActive(false);
        if (p2_DrawButton != null) p2_DrawButton.gameObject.SetActive(false);

        hostButton.onClick.AddListener(() => StartHost());
        clientButton.onClick.AddListener(() => StartClient());
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
                    p2_DrawButton.gameObject.SetActive(false);
                }
                else
                {
                    p1_DrawButton.gameObject.SetActive(false);
                    p2_DrawButton.gameObject.SetActive(true);
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

                    hasHookedUpButtons = true;
                }
            }
        }
    }

    private void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        ShowGameUI();
    }

    private void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        ShowGameUI();
    }
    
    // This method now only handles switching panels.
    private void ShowGameUI()
    {
        connectionPanel.SetActive(false);
        gamePanel.SetActive(true);
    }
}
