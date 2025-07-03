using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class InGameUIManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button drawButton;
    public Button passButton;

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
        }
        else
        {
            // If we are not connected, hide the buttons.
            drawButton.gameObject.SetActive(false);
            passButton.gameObject.SetActive(false);
        }
    }
}
