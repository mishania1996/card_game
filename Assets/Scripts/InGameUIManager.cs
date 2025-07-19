using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using UnityEngine.Localization.Components;
using Unity.Collections;
using UnityEngine.Localization;


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

    private LocalizedString turnInfoTemplate;
    private LocalizedString suitInfoTemplate;
    private string cachedTurnTemplate;
    private string cachedSuitTemplate;


    void Start()
    {


        // Hook up the button listeners to the functions in CardManager
        drawButton.onClick.AddListener(() => cardManager.OnDrawCardButtonPressed());
        passButton.onClick.AddListener(() => cardManager.OnPassButtonPressed());


    }

    void Awake()
    {
        // Initialize the LocalizedString objects to point to your table entries.
        turnInfoTemplate = new LocalizedString { TableReference = "UI_Text", TableEntryReference = "turn_info_text" };
        suitInfoTemplate = new LocalizedString { TableReference = "UI_Text", TableEntryReference = "suit_info_text" };
    }

    void OnEnable()
    {
        if (Application.isPlaying && gameFlow != null)
        {
            // Subscribe to events for when the LANGUAGE changes.
            turnInfoTemplate.StringChanged += OnTurnTemplateChanged;
            suitInfoTemplate.StringChanged += OnSuitTemplateChanged;

            // Subscribe to events for when the GAME DATA changes.
            gameFlow.CurrentPlayerName.OnValueChanged += OnGameDataChanged;
            gameFlow.ActiveSuit.OnValueChanged += OnGameDataChanged;
        }
    }

    void OnDisable()
    {
        if (Application.isPlaying && gameFlow != null)
        {
            // Unsubscribe from all events.
            turnInfoTemplate.StringChanged -= OnTurnTemplateChanged;
            suitInfoTemplate.StringChanged -= OnSuitTemplateChanged;
            gameFlow.CurrentPlayerName.OnValueChanged -= OnGameDataChanged;
            gameFlow.ActiveSuit.OnValueChanged -= OnGameDataChanged;
        }
    }

    void Update()
    {
        // This runs every frame to ensure buttons are only shown to the correct player.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // Check if the currently active player's ID matches my own ID.
            bool isMyTurn = (NetworkManager.Singleton.LocalClientId == gameFlow.CurrentPlayerId.Value);
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
    private void OnTurnTemplateChanged(string newTemplate)
    {
        cachedTurnTemplate = newTemplate;
        UpdateAllText();
    }

    private void OnSuitTemplateChanged(string newTemplate)
    {
        cachedSuitTemplate = newTemplate;
        UpdateAllText();
    }

    private void OnGameDataChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        UpdateAllText();
    }

    private void UpdateAllText()
    {
        if (gameFlow == null) return;

        // Update Turn Info Text
        if (!string.IsNullOrEmpty(cachedTurnTemplate))
        {
            turnInfoText.text = string.Format(cachedTurnTemplate, gameFlow.CurrentPlayerNameString);
        }

        // Update Suit Info Text
        string activeSuit = gameFlow.ActiveSuitString;
        bool suitIsActive = !string.IsNullOrEmpty(activeSuit);
        suitInfoText.gameObject.SetActive(suitIsActive);
        if (suitIsActive && !string.IsNullOrEmpty(cachedSuitTemplate))
        {
            suitInfoText.text = string.Format(cachedSuitTemplate, activeSuit);
        }
    }
}
