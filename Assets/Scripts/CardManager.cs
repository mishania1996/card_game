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

    // Server-side authoritative lists
    private List<GameObject> deck = new List<GameObject>();
    private List<GameObject> discardPile = new List<GameObject>();
    // Client-side visual lists
    private List<GameObject> player1Hand = new List<GameObject>();
    private List<GameObject> player2Hand = new List<GameObject>();

    // Player tracking
    private Dictionary<ulong, bool> playersReady = new Dictionary<ulong, bool>();
    private ulong player1_clientId = 99;
    private ulong player2_clientId = 99;
    
    // Card definitions for building the deck
    private readonly List<string> suits = new List<string> { "hearts", "diamonds", "clubs", "spades" };
    private readonly List<string> ranks = new List<string> { "2", "3", "4", "5", "6", "7", "8", "9", "10", "jack", "queen", "king", "ace" };


    public override void OnNetworkSpawn()
    {
        LoadCardSprites();

        if (IsServer)
        {
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

            // --- THIS IS THE FIX ---
            // This logic cleans up the sprite names that Unity provides.
            // For example, it turns "king_of_spades_0" into "king_of_spades"
            int underscoreIndex = spriteName.LastIndexOf('_');
            if (underscoreIndex > -1)
            {
                string suffix = spriteName.Substring(underscoreIndex + 1);
                if(int.TryParse(suffix, out _))
                {
                    spriteName = spriteName.Substring(0, underscoreIndex);
                }
            }
            
            // This prevents adding the back card or duplicates to the front card dictionary
            if (spriteName != "back_card" && !allCardSprites.ContainsKey(spriteName))
            {
                allCardSprites.Add(spriteName, sprite);
            }
        }
        
        Debug.Log($"Loaded {allCardSprites.Count} unique card front sprites from Resources.");
    }

    private void ServerOnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        if (!playersReady.ContainsKey(clientId))
        {
            playersReady.Add(clientId, false);
        }

        if (player1_clientId == 99) player1_clientId = clientId;
        else if (player2_clientId == 99) player2_clientId = clientId;

        if (playersReady.Count == 2)
        {
            Debug.Log($"Two players connected. P1 is Client {player1_clientId}, P2 is Client {player2_clientId}. Starting game setup.");
            StartCoroutine(ServerGameSetup());
        }
    }

    IEnumerator ServerGameSetup()
    {
        yield return new WaitForEndOfFrame();
        InitializeDeck();
        ShuffleDeck();
        DealInitialHands(5);
    }

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

                    CardData cardData = newCard.GetComponent<CardData>();
                    cardData.Rank.Value = rank;
                    cardData.Suit.Value = suit;
                    newCard.name = cardSpriteName;
                    
                    deck.Add(newCard);
                    // Pass empty strings for suit/rank since it's face down anyway
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

    void DealInitialHands(int numCardsPerPlayer)
    {
        if (!IsServer) return;

        // --- NEW DEBUG LOG ---
        // This will tell us if the deck has cards before we try to deal.
        Debug.Log($"Dealing hands. Deck count is: {deck.Count}");

        for (int i = 0; i < numCardsPerPlayer; i++)
        {
            if(deck.Count > 0) DrawCard(player1_clientId);
            if(deck.Count > 0) DrawCard(player2_clientId);
        }
    }

    public void DrawCard(ulong targetClientId)
    {
        if (!IsServer || deck.Count == 0) return;
        GameObject cardToDraw = deck[0];
        deck.RemoveAt(0);
        
        List<GameObject> targetHand = (targetClientId == player1_clientId) ? player1Hand : player2Hand;
        targetHand.Add(cardToDraw);

        CardData cardData = cardToDraw.GetComponent<CardData>();
        // Pass the card's actual suit and rank into the RPC
        ParentAndAnimateCardClientRpc(new NetworkObjectReference(cardToDraw), CardLocation.PlayerHand, targetClientId, cardData.Suit.Value, cardData.Rank.Value);
    }

    public void PlayCardToDiscardPile(GameObject cardToPlay, ulong requestingClientId)
    {
        if (!IsServer) return;

        if (!IsCardInPlayerHand(cardToPlay, requestingClientId))
        {
            Debug.LogError($"SECURITY VIOLATION: Client {requestingClientId} tried to play a card they do not own!");
            return;
        }

        if (requestingClientId == player1_clientId) player1Hand.Remove(cardToPlay);
        else if (requestingClientId == player2_clientId) player2Hand.Remove(cardToPlay);

        discardPile.Add(cardToPlay);

        CardData cardData = cardToPlay.GetComponent<CardData>();
        // Pass the card's actual suit and rank into the RPC
        ParentAndAnimateCardClientRpc(new NetworkObjectReference(cardToPlay), CardLocation.Discard, 0, cardData.Suit.Value, cardData.Rank.Value);
    }

    public void OnDrawCardButtonPressed()
    {
        RequestDrawCardServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDrawCardServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        DrawCard(requestingClientId);
    }
    
    // --- THIS IS THE MISSING ENUM ---
    private enum CardLocation { Deck, PlayerHand, Discard }

    [ClientRpc]
    private void ParentAndAnimateCardClientRpc(NetworkObjectReference cardRef, CardLocation location, ulong ownerClientId, FixedString32Bytes suit, FixedString32Bytes rank, ClientRpcParams clientRpcParams = default)
    {
        if (!cardRef.TryGet(out NetworkObject cardNetworkObject)) return;
        
        GameObject card = cardNetworkObject.gameObject;
        bool isMyCard = (ownerClientId == NetworkManager.Singleton.LocalClientId);
        
        switch (location)
        {
            case CardLocation.Deck:
                card.transform.SetParent(deckDrawArea, false);
                SetCardFace(card, false, "", ""); // Face down
                break;
            case CardLocation.Discard:
                card.GetComponent<PlayableCard>().ClearHandReference();
                if(player1Hand.Contains(card)) player1Hand.Remove(card);
                if(player2Hand.Contains(card)) player2Hand.Remove(card);
                if(!discardPile.Contains(card)) discardPile.Add(card);
                card.transform.SetParent(discardPileArea, false);
                SetCardFace(card, true, suit, rank); // Face up, passing data
                break;
            case CardLocation.PlayerHand:
                RectTransform targetArea = isMyCard ? player1HandArea : player2HandArea;
                List<GameObject> targetHandList = isMyCard ? player1Hand : player2Hand;
                if(!targetHandList.Contains(card)) targetHandList.Add(card);
                card.GetComponent<PlayableCard>().SetHandReference(targetHandList);
                card.transform.SetParent(targetArea, false);
                SetCardFace(card, isMyCard, suit, rank); // Show face based on ownership, passing data
                break;
        }

        card.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        ArrangeHand(player1Hand, player1HandArea);
        ArrangeHand(player2Hand, player2HandArea);
    }
    
    private void SetCardFace(GameObject card, bool showFront, FixedString32Bytes suit, FixedString32Bytes rank)
    {
        Sprite spriteToSet = cardBackSprite;
        if (showFront)
        {
            // We construct the name from the parameters passed directly into the RPC
            string spriteName = $"{rank}_of_{suit}";
            if (!allCardSprites.TryGetValue(spriteName, out spriteToSet))
            {
                Debug.LogError($"Could not find sprite named '{spriteName}' in the dictionary.");
                spriteToSet = cardBackSprite; // Fallback
            }
        }
        card.GetComponent<Image>().sprite = spriteToSet;
    }
    
    

    

    private bool IsCardInPlayerHand(GameObject card, ulong targetClientId)
    {
        List<GameObject> targetHand = (targetClientId == player1_clientId) ? player1Hand : player2Hand;
        return targetHand != null && targetHand.Contains(card);
    }
    
    void ArrangeHand(List<GameObject> hand, RectTransform handArea)
    {
        if (hand == null || hand.Count == 0) return;
        
        hand.RemoveAll(item => item == null);
        if (hand.Count == 0) return;

        float cardWidth = hand[0].GetComponent<RectTransform>().rect.width;
        float overlapAmount = 0.6f;
        float spacing = cardWidth * (1 - overlapAmount);
        float totalHandWidth = (hand.Count - 1) * spacing + cardWidth;
        float startX = -totalHandWidth / 2f;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i] == null) continue; 
            RectTransform cardRect = hand[i].GetComponent<RectTransform>();
            cardRect.anchoredPosition = new Vector2(startX + (i * spacing), 0);
            cardRect.SetAsLastSibling();
        }
    }
}
