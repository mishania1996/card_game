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
    public RectTransform player1HandArea;
    public RectTransform player2HandArea;

    [Header("Prefabs")]
    public GameObject cardPrefab;

    private Dictionary<string, Sprite> allCardSprites = new Dictionary<string, Sprite>();
    private Sprite cardBackSprite;
    private GameFlow gameFlow;

    // Server-side authoritative lists
    private List<GameObject> deck = new List<GameObject>();
    private List<GameObject> discardPile = new List<GameObject>();
    // Client-side visual lists
    private List<GameObject> player1Hand = new List<GameObject>();
    private List<GameObject> player2Hand = new List<GameObject>();

    // Player tracking
    private Dictionary<ulong, bool> playersReady = new Dictionary<ulong, bool>();
    public ulong player1_clientId = 99;
    public ulong player2_clientId = 99;
    
    // Card definitions for building the deck
    private readonly List<string> suits = new List<string> { "hearts", "diamonds", "clubs", "spades" };
    private readonly List<string> ranks = new List<string> { "6", "7", "8", "9", "10", "jack", "queen", "king", "ace" };

	// This is a special Netcode function called automatically on all clients and the host
    // when this NetworkObject is created on the network. It's the main entry point for setup.
    public override void OnNetworkSpawn()
    {	
    	// This runs for everyone to ensure all players have the card art loaded.
        LoadCardSprites();
		gameFlow = FindAnyObjectByType<GameFlow>();
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
            
            // This prevents adding the back card or duplicates to the front card dictionary
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
		
		// Add the new client to our player tracking dictionary if they aren't already in it.
        // This prevents adding the same player twice.
        if (!playersReady.ContainsKey(clientId))
        {
            playersReady.Add(clientId, false);
        }
		
		// Assign the connecting player to an open player slot (P1 or P2).
        if (player1_clientId == 99) player1_clientId = clientId;
        else if (player2_clientId == 99) player2_clientId = clientId;

        if (playersReady.Count == 2)
        {
            Debug.Log($"Two players connected. P1 is Client {player1_clientId}, P2 is Client {player2_clientId}. Starting game setup.");
            StartCoroutine(ServerGameSetup());
        }
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
        gameFlow.SetPlayerTurn(player1_clientId);
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
            // We now call DrawCard with Forced: true to bypass the turn check.
            if(deck.Count > 0) DrawCard(player1_clientId, true);
            if(deck.Count > 0) DrawCard(player2_clientId, true);
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

        if (deck.Count == 0 && discardPile.Count <= 1) return;
        if (deck.Count == 0)
        {
            // If it is, start the new coroutine to handle the delayed reshuffle.
            StartCoroutine(ReshuffleWithDelay());
            return;
        }
        
        // Take the top card from the deck list.
        GameObject cardToDraw = deck[0];
        deck.RemoveAt(0);
        
        // Add the card to the server's logical hand list for the target player.
        List<GameObject> targetHand = (targetClientId == player1_clientId) ? player1Hand : player2Hand;
        targetHand.Add(cardToDraw);

        CardData cardData = cardToDraw.GetComponent<CardData>();
        // Tell all clients to perform the visual action of moving the card.
        ParentAndAnimateCardClientRpc(new NetworkObjectReference(cardToDraw), CardLocation.PlayerHand, targetClientId, cardData.Suit.Value, cardData.Rank.Value);
       
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
        
        if (!IsCardInPlayerHand(cardToPlay, requestingClientId))
        {
            Debug.LogError($"SECURITY VIOLATION: Client {requestingClientId} tried to play a card they do not own!");
            return;
        }
		
        CardData cardToPlayData = cardToPlay.GetComponent<CardData>();

        CardData topCardData = null;

		if (discardPile.Count > 0)
        {
            topCardData = discardPile[discardPile.Count - 1].GetComponent<CardData>();
        }


        if (!gameFlow.IsValidPlay(cardToPlayData, topCardData))
        {
            Debug.LogWarning("Invalid move attempted!");
            return;
        }

        List<GameObject> sourceHand = (requestingClientId == player1_clientId) ? player1Hand : player2Hand;
        sourceHand.Remove(cardToPlay);
        discardPile.Add(cardToPlay);
		
		// Notify all clients of the visual change.
        CardData cardData = cardToPlay.GetComponent<CardData>();
        ParentAndAnimateCardClientRpc(new NetworkObjectReference(cardToPlay), CardLocation.Discard, 0, cardData.Suit.Value, cardData.Rank.Value);

        if (cardData.Rank.Value == "6")
        {
            ulong nextPlayerId = (requestingClientId == player1_clientId) ? player2_clientId : player1_clientId;
            DrawCard(nextPlayerId, true);
            return;
        }
        if (cardData.Rank.Value == "7")
        {
            ulong nextPlayerId = (requestingClientId == player1_clientId) ? player2_clientId : player1_clientId;
            DrawCard(nextPlayerId, true);
            DrawCard(nextPlayerId, true);
            return;
        }
        
        if (cardData.Rank.Value == "ace" || cardData.Rank.Value == "8") return ;
        gameFlow.SwitchTurn(requestingClientId);
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
    
    // A simple "enum" to create readable names for a card's possible locations.
    private enum CardLocation { Deck, PlayerHand, Discard }


	// This is a command sent FROM the server TO all clients.
    // Its job is to perform all the visual actions of moving a card.
    [ClientRpc]
    private void ParentAndAnimateCardClientRpc(NetworkObjectReference cardRef, CardLocation location, ulong ownerClientId, FixedString32Bytes suit, FixedString32Bytes rank, ClientRpcParams clientRpcParams = default)
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
                card.GetComponent<PlayableCard>().ClearHandReference(); // Make the card un-playable.
                if(player1Hand.Contains(card)) player1Hand.Remove(card);
                if(player2Hand.Contains(card)) player2Hand.Remove(card);
                if(!discardPile.Contains(card)) discardPile.Add(card);
                card.transform.SetParent(discardPileArea, false);
                SetCardFace(card, true, suit, rank); // Discarded cards are always face up.
                break;
            case CardLocation.PlayerHand:
            	// Determine the correct hand area (bottom for me, top for opponent).
                RectTransform targetArea = isMyCard ? player1HandArea : player2HandArea;
                List<GameObject> targetHandList = isMyCard ? player1Hand : player2Hand;
                if(!targetHandList.Contains(card)) targetHandList.Add(card);
                card.GetComponent<PlayableCard>().SetHandReference(targetHandList);
                card.transform.SetParent(targetArea, false);
                SetCardFace(card, isMyCard, suit, rank); // Only I see my card's face ownership, passing data
                break;
        }
		
		// Reset the card's position and then update the layout of both hands.
        card.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        ArrangeHand(player1Hand, player1HandArea);
        ArrangeHand(player2Hand, player2HandArea);
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
	
	// A server-only helper function to check if a card is in a specific player's hand.
    private bool IsCardInPlayerHand(GameObject card, ulong targetClientId)
    {
        List<GameObject> targetHand = (targetClientId == player1_clientId) ? player1Hand : player2Hand;
        return targetHand != null && targetHand.Contains(card);
    }
    
    // Arranges the cards in a hand visually to fan them out and center them.
    void ArrangeHand(List<GameObject> hand, RectTransform handArea)
    {
        if (hand == null || hand.Count == 0) return;
        
        // Safety check to remove any null entries from the list (e.g., if a card was destroyed).
        hand.RemoveAll(item => item == null);
        if (hand.Count == 0) return;

        float cardWidth = hand[0].GetComponent<RectTransform>().rect.width;
        float overlapAmount = 0.6f;
        float spacing = cardWidth * (1 - overlapAmount);
        float totalHandWidth = (hand.Count - 1) * spacing + cardWidth;
        float startX = -totalHandWidth / 2f;
        
        // --- Position Each Card ---
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] == null) continue; 
            RectTransform cardRect = hand[i].GetComponent<RectTransform>();
            cardRect.anchoredPosition = new Vector2(startX + (i * spacing), 0);
            // SetAsLastSibling ensures cards on the right render on top of cards on the left.
            cardRect.SetAsLastSibling();
        }
    }
}
