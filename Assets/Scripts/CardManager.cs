using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using System.Linq;
using Unity.Collections; 

public class CardManager : NetworkBehaviour
{
    [Header("UI Areas")]
    public RectTransform deckDrawArea;
    public RectTransform discardPileArea;
    public List<RectTransform> playerHandAreas;
    public GameObject gamePanel;

    [Header("Game Over UI")]
    public GameObject endGamePanel;
    public GameObject winScreenPanel;
    public GameObject loseScreenPanel;

    [Header("Suit Choice UI")]
    public GameObject suitChoicePanel;
    public Button heartsButton;
    public Button diamondsButton;
    public Button clubsButton;
    public Button spadesButton;

    [Header("Prefabs")]
    public GameObject cardPrefab;
    public GameFlow gameFlow;

    private Dictionary<string, Sprite> allCardSprites = new Dictionary<string, Sprite>();
    private Sprite cardBackSprite;


    // Server-side authoritative lists
    private List<GameObject> deck = new List<GameObject>();
    private List<GameObject> discardPile = new List<GameObject>();

    // Player tracking
    public Dictionary<ulong, PlayerData> players = new Dictionary<ulong, PlayerData>();
    public bool active_player_has_drawn = false;

    // This will only hold cards for the local player running the game.
    private Dictionary<ulong, string> playerNames = new Dictionary<ulong, string>();
    private List<GameObject> myLocalHand = new List<GameObject>();
    private int myPlayerIndex = -1;

    // Card definitions for building the deck
    private readonly List<string> suits = new List<string> { "hearts", "diamonds", "clubs", "spades" };
    private readonly List<string> ranks = new List<string> { "6", "7", "8", "9", "10", "jack", "queen", "king", "ace" };

	// This is a special Netcode function called automatically on all clients and the host when this NetworkObject is created on the network. It's the main entry point for setup.
    public override void OnNetworkSpawn()
    {	
    	// This runs for everyone to ensure all players have the card art loaded.
        LoadCardSprites();
		// The following logic is for the server only.
        if (IsServer)
        {	
        	// Subscribe a function to be called every time a new client connects.
            NetworkManager.Singleton.OnClientConnectedCallback += ServerOnClientConnected;
        }
    }


    private void LoadCardSprites()
    {
        cardBackSprite = Resources.Load<Sprite>("CardSprites/back_card");
        if (cardBackSprite == null) Debug.LogError("Card Back Sprite not found! Ensure it's named 'back_card' in 'Assets/Resources/CardSprites'.");

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>("CardSprites");
        allCardSprites.Clear();

        foreach (Sprite sprite in loadedSprites)
        {
            string spriteName = sprite.name;

            if (spriteName != "back_card" && !allCardSprites.ContainsKey(spriteName))
            {
                allCardSprites.Add(spriteName, sprite);
            }
        }

        Debug.Log($"Loaded {allCardSprites.Count} unique card front sprites from Resources.");
    }
	
	// This server-only function is called automatically when a client connects.
    private void ServerOnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
		
        if (players.Count >= gameFlow.NumberOfPlayers.Value) return;
		// Add the new client to our player tracking dictionary if they aren't already in it.
        // This prevents adding the same player twice.
        if (players.ContainsKey(clientId)) return;

        RectTransform handArea = playerHandAreas[players.Count];
        players.Add(clientId, new PlayerData(clientId, handArea, players.Count));

        Debug.Log($"Player with ClientId {clientId} connected. Total players: {players.Count}");

    }

    public void StartGameSetup()
    {
        if (IsServer)
        {
            StartCoroutine(ServerGameSetup());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RegisterPlayerNameServerRpc(FixedString32Bytes name, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        string receivedName = name.ToString();
        Debug.Log($"--- SERVER LOG: Received name registration from Client ID {senderId} with name: '{receivedName}'");

        playerNames[senderId] = receivedName;
    }

    public string GetPlayerName(ulong clientId)
    {
        if (playerNames.TryGetValue(clientId, out string name))
        {
            return name;
        }
        return $"Player {clientId}"; // Fallback
    }


    public void SetLocalPlayerInfo(int playerIndex)
    {
        myPlayerIndex = playerIndex;
    }
	
	// This is a Coroutine that orchestrates the start of the game on the server.
    IEnumerator ServerGameSetup()
    {	
    	// We wait one frame to ensure all clients are synchronized before we start spawning cards.
        yield return new WaitForEndOfFrame();
        
        // Now, in the next frame, we perform the setup sequence.
        InitializeDeck();
        ShuffleDeck();
        DealInitialHands(5);
    }
	
	// This server-only function creates a full 36-card deck.
    void InitializeDeck()
    {
        if (!IsServer) return;

        foreach (string suit in suits)
        {
            foreach (string rank in ranks)
            {
                string cardSpriteName = $"{rank}_of_{suit}";
                if (allCardSprites.ContainsKey(cardSpriteName))
                {
                    GameObject newCard = Instantiate(cardPrefab);
                    NetworkObject no = newCard.GetComponent<NetworkObject>();
                    no.Spawn(true);
					
					// Set the card's synchronized data (its permanent identity)
                    CardData cardData = newCard.GetComponent<CardData>();
                    cardData.Rank.Value = rank;
                    cardData.Suit.Value = suit;
                    newCard.name = cardSpriteName;
                    
                    deck.Add(newCard);
                    // Tell all clients to visually place this new card in the deck area.
                    ParentAndAnimateCardClientRpc(new NetworkObjectReference(newCard), CardLocation.Deck, 0, "", "");
                }
                else
                {
                    Debug.LogWarning($"Card sprite not found for {cardSpriteName} in Resources. Skipping this card.");
                }
            }
        }
    }

    void ShuffleDeck()
    {
        if (!IsServer) return;

        var random = new System.Random();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int randomIndex = random.Next(0, i + 1);
            
            // Swap the elements at position i and the random position
            GameObject temp = deck[i];
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
        Debug.Log("Deck has been shuffled correctly.");
    }
	
	// Takes all but the top card of the discard pile and makes it the new deck.
    private void ReshuffleDiscardPile()
    {
        if (discardPile.Count <= 1)
        {
            Debug.Log("Not enough cards in discard pile to reshuffle.");
            return;
        }

        // Keep the top card of the discard pile.
        GameObject topCard = discardPile[discardPile.Count - 1];
        discardPile.RemoveAt(discardPile.Count - 1);

        // Move the rest of the discard pile into the deck list.
        deck.AddRange(discardPile);
        
        // Clear the discard pile and add the top card back.
        discardPile.Clear();
        discardPile.Add(topCard);

        // Shuffle the newly created deck.
        ShuffleDeck();

        // Create a list of network references for all cards now in the deck.
        List<NetworkObjectReference> newDeckRefs = new List<NetworkObjectReference>();
        foreach(var card in deck)
        {
            newDeckRefs.Add(card);
        }

        // Send the list of new deck cards to the clients.
        ReshuffleClientRpc(new NetworkObjectReference(topCard), newDeckRefs.ToArray());
        
    }

    [ClientRpc]
    private void ReshuffleClientRpc(NetworkObjectReference topCardRef, NetworkObjectReference[] newDeckRefs)
    {
    	// On each client, first clear the visual deck list.
        deck.Clear();

        // Repopulate the visual deck list and parent the cards to the deck area.
        foreach (var cardRef in newDeckRefs)
        {
            if (cardRef.TryGet(out NetworkObject cardNetworkObject))
            {
                GameObject card = cardNetworkObject.gameObject;
                deck.Add(card); // Add to client's visual deck list
                card.transform.SetParent(deckDrawArea, false);
                card.transform.localPosition = Vector3.zero; // Reset position
                SetCardFace(card, false, "", ""); // Ensure it's face down
            }
        }


        // On each client, clear the visual discard pile list.
        discardPile.Clear();

        // Find the top card and add it back to the visual discard pile list.
        if (topCardRef.TryGet(out NetworkObject topCardNetworkObject))
        {
            discardPile.Add(topCardNetworkObject.gameObject);
        }

        // The deck is now visually empty, but that's okay. The server knows
        // where the cards are, and the next DrawCard call will correctly
        // move a "new" card from the deck area to a player's hand.
        Debug.Log("Client is visually updating reshuffled discard pile.");
    }
    
    // This coroutine adds a delay before triggering the reshuffle logic.
    private IEnumerator ReshuffleWithDelay()
    {
        Debug.Log("Deck is empty. Reshuffling in 2 seconds...");
        // This is the pause. It waits for 2 seconds in real-time.
        yield return new WaitForSeconds(2f);
        
        // After the wait, call the actual reshuffle method.
        ReshuffleDiscardPile();
    }
    
	// Deals a specified number of cards to each player at the start of the game.
    void DealInitialHands(int numCardsPerPlayer)
    {
        if (!IsServer) return;

        // This will tell us if the deck has cards before we try to deal.
        Debug.Log($"Dealing hands. Deck count is: {deck.Count}");

        for (int i = 0; i < numCardsPerPlayer; i++)
        {	
            foreach (ulong clientId in players.Keys)
            {
                if (deck.Count > 0) DrawCard(clientId, true);
            }
        }
    }
	
	// The server-side logic for moving one card from the deck to a player's hand.
    public void DrawCard(ulong targetClientId, bool Forced = false)
    {
        if (!IsServer) return;
        if (!Forced && targetClientId != gameFlow.CurrentPlayerId.Value)
        {
            Debug.LogWarning($"It is not Client {targetClientId}'s turn!");
            return;
        }
        if (!players.ContainsKey(targetClientId))
        {
            Debug.LogError($"Player with ID {targetClientId} not found.");
            return;
        }
        if (deck.Count == 0 && discardPile.Count <= 1) return;
        if (deck.Count == 0)
        {
            // If it is, start the new coroutine to handle the delayed reshuffle.
            StartCoroutine(ReshuffleWithDelay());
            return;
        }



        if (!Forced && active_player_has_drawn) return;
        
        // Take the top card from the deck list.
        GameObject cardToDraw = deck[0];
        deck.RemoveAt(0);
        
        // Add the card to the server's logical hand list for the target player.
        PlayerData targetPlayer = players[targetClientId];
        targetPlayer.Hand.Add(cardToDraw);

        CardData cardData = cardToDraw.GetComponent<CardData>();
        int ownerPlayerIndex = targetPlayer.PlayerIndex;

        // Tell all clients to perform the visual action of moving the card.
        ParentAndAnimateCardClientRpc(new NetworkObjectReference(cardToDraw), CardLocation.PlayerHand, targetClientId, cardData.Suit.Value, cardData.Rank.Value, ownerPlayerIndex);

        if (!Forced) active_player_has_drawn = true;
    }
	
	// This server-only function validates and processes a "play card" action.
    public void PlayCardToDiscardPile(GameObject cardToPlay, ulong requestingClientId)
    {
        if (!IsServer) return;
        
		// Check if it is the requesting player's turn before any other checks.
        if (requestingClientId != gameFlow.CurrentPlayerId.Value)
        {
            Debug.LogWarning($"It is not Client {requestingClientId}'s turn!");
            return;
        }
        
        if (!players[requestingClientId].Hand.Contains(cardToPlay))
        {
            Debug.LogError($"SECURITY VIOLATION: Client {requestingClientId} tried to play a card they do not own!");
            return;
        }
		
        CardData cardData = cardToPlay.GetComponent<CardData>();

        CardData topCardData = null;

		if (discardPile.Count > 0)
        {
            topCardData = discardPile[discardPile.Count - 1].GetComponent<CardData>();
        }


        if (!gameFlow.IsValidPlay(cardData, topCardData))
        {
            Debug.LogWarning("Invalid move attempted!");
            return;
        }

        if (cardData.Rank.Value != "jack")
        {
            gameFlow.ActiveSuit.Value = "";
        }

        players[requestingClientId].Hand.Remove(cardToPlay);
        discardPile.Add(cardToPlay);
		
		// Notify all clients of the visual change.
        ParentAndAnimateCardClientRpc(new NetworkObjectReference(cardToPlay), CardLocation.Discard, 0, cardData.Suit.Value, cardData.Rank.Value);

        gameFlow.ApplyPower(requestingClientId, cardData);
    }
	
	// This public function is the entry point for the UI button's OnClick event.
    public void OnDrawCardButtonPressed()
    {	
	    // It immediately calls a ServerRpc to pass the request to the server.
        RequestDrawCardServerRpc();
    }

    // This function is called by a client, but runs only on the server.
    [ServerRpc(RequireOwnership = false)]
    private void RequestDrawCardServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        DrawCard(requestingClientId);
    }

    public void OnPassButtonPressed()
    {
        // It calls a ServerRpc to ask the server to switch the turn.
        RequestPassTurnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPassTurnServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;

        // We only allow a player to pass on their own turn.
        if (!active_player_has_drawn)
        {
            Debug.Log($"Player must draw a card first");
            return;
        }

        if (requestingClientId == gameFlow.CurrentPlayerId.Value)
        {
            Debug.Log($"Client {requestingClientId} passed their turn.");
            gameFlow.SetPlayerTurn(gameFlow.GetNextPlayerInTurn(requestingClientId));
        }

    }
    
    // A simple "enum" to create readable names for a card's possible locations.
    private enum CardLocation { Deck, PlayerHand, Discard }


	// This is a command sent FROM the server TO all clients.
    // Its job is to perform all the visual actions of moving a card.
    [ClientRpc]
    private void ParentAndAnimateCardClientRpc(NetworkObjectReference cardRef, CardLocation location, ulong ownerClientId, FixedString32Bytes suit, FixedString32Bytes rank, int ownerPlayerIndex = -1, ClientRpcParams clientRpcParams = default)
    {	
    	// Find the specific card object on the local machine.
        if (!cardRef.TryGet(out NetworkObject cardNetworkObject)) return;
        
        GameObject card = cardNetworkObject.gameObject;
        // Check if the card's owner is the player running on this machine.
        bool isMyCard = (ownerClientId == NetworkManager.Singleton.LocalClientId);
        
        // Use the 'location' parameter to decide which action to take.
        switch (location)
        {
            case CardLocation.Deck:
                card.transform.SetParent(deckDrawArea, false); // Moves card in Unity
                SetCardFace(card, false, "", ""); // Deck cards are always face down.
                break;
            case CardLocation.Discard:
                if (myLocalHand.Contains(card)) myLocalHand.Remove(card);
                card.GetComponent<PlayableCard>().ClearHandReference();
                card.transform.SetParent(discardPileArea, false);
                SetCardFace(card, true, suit, rank); // Discarded cards are always face up.
                break;
            case CardLocation.PlayerHand:
            	// Determine the correct hand area
                int uiSlotIndex = (ownerPlayerIndex - myPlayerIndex + gameFlow.NumberOfPlayers.Value) % gameFlow.NumberOfPlayers.Value;
                RectTransform targetArea = playerHandAreas[uiSlotIndex];

                if (isMyCard)
                {
                    myLocalHand.Add(card);
                    card.GetComponent<PlayableCard>().SetHandReference(myLocalHand);
                }
                else
                {
                    card.GetComponent<PlayableCard>().ClearHandReference();
                }

                card.transform.SetParent(targetArea, false);
                SetCardFace(card, isMyCard, suit, rank);
                break;
        }
		
		// Reset the card's position and then update the layout of both hands.
        card.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        foreach (var area in playerHandAreas)
        {
            ArrangeHand(area);
        }
    }
    

    
    // Sets the visible sprite for a card (either its front face or its back).
    private void SetCardFace(GameObject card, bool showFront, FixedString32Bytes suit, FixedString32Bytes rank)
    {	
  	 	// Default to showing the card back
        Sprite spriteToSet = cardBackSprite;
        if (showFront)
        {
            string spriteName = $"{rank}_of_{suit}";
            
            // Safely try to get the sprite from our dictionary.
            if (!allCardSprites.TryGetValue(spriteName, out spriteToSet))
            {
                Debug.LogError($"Could not find sprite named '{spriteName}' in the dictionary.");
                spriteToSet = cardBackSprite; // Fallback
            }
        }
        // Apply the determined sprite to the card's Image component
        card.GetComponent<Image>().sprite = spriteToSet;
    }
	
    
    // Arranges the cards in a hand visually to fan them out and center them.
    void ArrangeHand(RectTransform handArea)
    {
        // Create a temporary list of all card GameObjects that are children of the handArea.
        List<RectTransform> cardsInHand = new List<RectTransform>();
        foreach (Transform child in handArea.transform)
        {
            // Make sure we only add active cards to the list
            if (child.gameObject.activeInHierarchy)
            {
                cardsInHand.Add(child.GetComponent<RectTransform>());
            }
        }

        if (cardsInHand.Count == 0) return;

        float cardWidth = cardsInHand[0].rect.width;
        float overlapAmount = 0.6f;
        float spacing = cardWidth * (1 - overlapAmount);
        float totalHandWidth = (cardsInHand.Count - 1) * spacing + cardWidth;
        float startX = -totalHandWidth / 2f;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            RectTransform cardRect = cardsInHand[i];
            cardRect.anchoredPosition = new Vector2(startX + (i * spacing), 0);
            cardRect.SetAsLastSibling();
        }
    }

    public void ShowSuitChoicePanel()
    {
        suitChoicePanel.SetActive(true);

        // Set up the button listeners. They only need to be set up once.
        // We remove any old listeners first to be safe.
        heartsButton.onClick.RemoveAllListeners();
        diamondsButton.onClick.RemoveAllListeners();
        clubsButton.onClick.RemoveAllListeners();
        spadesButton.onClick.RemoveAllListeners();

        heartsButton.onClick.AddListener(() => { OnSuitChosen("hearts"); });
        diamondsButton.onClick.AddListener(() => { OnSuitChosen("diamonds"); });
        clubsButton.onClick.AddListener(() => { OnSuitChosen("clubs"); });
        spadesButton.onClick.AddListener(() => { OnSuitChosen("spades"); });
    }

    private void OnSuitChosen(string chosenSuit)
    {
        // Hide the panel immediately after a choice is made
        suitChoicePanel.SetActive(false);

        // Call a new ServerRpc on the GameFlow script to tell the server our choice
        gameFlow.SetActiveSuitServerRpc(chosenSuit, NetworkManager.Singleton.LocalClientId);
    }

    public void ShowWinScreen()
    {
        // Hide all other panels and show the win screen
        gamePanel.SetActive(false);
        winScreenPanel.SetActive(true);
    }

    public void ShowLoseScreen()
    {
        // Hide all other panels and show the lose screen
        gamePanel.SetActive(false);
        loseScreenPanel.SetActive(true);
    }
}

[System.Serializable]
public class PlayerData
{
    public ulong ClientId;
    public List<GameObject> Hand = new List<GameObject>();
    public RectTransform HandArea; // The UI area for this player's hand
    public int PlayerIndex;

    public PlayerData(ulong clientId, RectTransform handArea, int playerIndex)
    {
        ClientId = clientId;
        HandArea = handArea;
        PlayerIndex = playerIndex;
    }
}



