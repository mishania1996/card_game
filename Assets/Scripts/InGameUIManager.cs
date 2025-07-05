using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

public class InGameUIManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button drawButton;
    public Button passButton;

    [Header("Info Panel")]
    public TMP_Text turnInfoText;
    public TMP_Text suitInfoText;

    [Header("Manager Scripts")]
    public CardManager cardManager;
    public GameFlow gameFlow;

    void Start()
    {
        // Find the managers if they aren't assigned in the inspector
        cardManager = FindAnyObjectByType<CardManager>();
        gameFlow = FindAnyObjectByType<GameFlow>();

        // Hook up the button listeners to the functions in CardManager
        drawButton.onClick.AddListener(() => cardManager.OnDrawCardButtonPressed());
        passButton.onClick.AddListener(() => cardManager.OnPassButtonPressed());
    }

    void Update()
    {
        // This runs every frame to ensure buttons are only shown to the correct player.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // Check if the currently active player's ID matches my own ID.
            bool isMyTurn = (NetworkManager.Singleton.LocalClientId == gameFlow.CurrentPlayerId.Value);

            // Show or hide the buttons based on whether it's my turn.
            drawButton.gameObject.SetActive(isMyTurn);
            passButton.gameObject.SetActive(isMyTurn);
            // Display the turn text prepared by the server.
            turnInfoText.text = gameFlow.TurnInfoText.Value.ToString();

            // Display the active suit text if one is set.
            string activeSuit = gameFlow.ActiveSuit.Value.ToString();
            if (!string.IsNullOrEmpty(activeSuit))
            {
                suitInfoText.text = $"Jack is demanding {activeSuit}";
                suitInfoText.gameObject.SetActive(true);
            }
            else
            {
                suitInfoText.gameObject.SetActive(false);
            }
        }
        else
        {
            // If we are not connected, hide the buttons.
            drawButton.gameObject.SetActive(false);
            passButton.gameObject.SetActive(false);
        }
    }
}
