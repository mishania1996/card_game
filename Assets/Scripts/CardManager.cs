using System.Collections.Generic; // For List
using UnityEngine;
using UnityEngine.UI; // For Image and RectTransform

public class CardManager : MonoBehaviour
{
    // Public variables to assign in the Inspector
    public GameObject cardPrefab; // Drag your 'Card' prefab here
    public List<Sprite> allCardFronts = new List<Sprite>(); // Drag all 36 front sprites here
    public Sprite cardBackSprite; // Drag your single card back sprite here

    [Header("UI Areas - Drag Panels from Hierarchy")]
    public RectTransform deckDrawArea;
    public RectTransform discardPileArea;
    public RectTransform player1HandArea;
    public RectTransform player2HandArea;

    // Private lists to hold cards
    private List<GameObject> deck = new List<GameObject>(); // Cards in the draw pile
    private List<GameObject> discardPile = new List<GameObject>(); // Cards in the discard pile
    private List<GameObject> player1Hand = new List<GameObject>(); // Player 1's cards
    private List<GameObject> player2Hand = new List<GameObject>(); // Player 2's cards (opponent)

    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
        PlaceDeckOnDrawArea();
        DealInitialHands(5); // Deal 5 cards to each player
    }

    // --- Core Card Management Functions ---

    // Sets the visual face of a card based on whether it should show its front or back
    void SetCardFace(GameObject card, Sprite specificFrontSprite, bool showFront)
    {
        // Get the main Image component (for the front)
        Image frontImageComponent = card.GetComponent<Image>();
        // Get the child Image component for the back
        Image backImageComponent = card.transform.Find("CardBackDisplay")?.GetComponent<Image>();

        if (frontImageComponent == null || backImageComponent == null)
        {
            Debug.LogError("Card prefab missing required Image components (main or CardBackDisplay child)!", card);
            return;
        }

        // Assign the specific front sprite if showing front
        if (showFront && specificFrontSprite != null)
        {
            frontImageComponent.sprite = specificFrontSprite;
            frontImageComponent.enabled = true; // Show the front image
            backImageComponent.enabled = false;  // Hide the back image
        }
        else // Show the back, or if no specific front sprite is provided
        {
            frontImageComponent.enabled = false; // Hide the front image
            backImageComponent.enabled = true;   // Show the back image
        }
    }

    // Initializes the deck with all 36 cards, assigning a unique front to each
    void InitializeDeck()
    {
        if (cardPrefab == null) { Debug.LogError("Card Prefab is not assigned!"); return; }
        if (allCardFronts.Count == 0) { Debug.LogError("No card front sprites assigned!"); return; }
        if (cardBackSprite == null) { Debug.LogError("Card Back Sprite is not assigned!"); return; }
        if (allCardFronts.Count != 36) { Debug.LogWarning("Expected 36 card fronts, but found " + allCardFronts.Count); }


        for (int i = 0; i < allCardFronts.Count; i++)
        {
            // Instantiate a new card from the prefab
            GameObject newCard = Instantiate(cardPrefab, transform); // 'transform' makes it a child of CardManager
            newCard.name = "Card_" + i; // Name for debugging (e.g., Card_0, Card_1, etc.)

            // Store the unique front sprite on the card itself for later reference
            // A simple way for now is to use its main Image component, it will hold the reference.
            Image frontImageComponent = newCard.GetComponent<Image>();
            if (frontImageComponent != null)
            {
                frontImageComponent.sprite = allCardFronts[i]; // Temporarily store the front sprite here
            }
            else
            {
                Debug.LogError("Card prefab missing main Image component!", newCard);
            }

            deck.Add(newCard); // Add the newly created card to our deck list
        }
        Debug.Log("Deck initialized with " + deck.Count + " cards.");
    }

    // Randomly shuffles the deck
    void ShuffleDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            GameObject temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
        Debug.Log("Deck shuffled.");
    }

    // Places all cards in the deck onto the visual draw area, showing their backs
    void PlaceDeckOnDrawArea()
    {
        if (deckDrawArea == null) { Debug.LogError("Deck Draw Area is not assigned!"); return; }

        foreach (GameObject card in deck)
        {
            card.transform.SetParent(deckDrawArea); // Move card to draw area parent
            card.GetComponent<RectTransform>().anchoredPosition = Vector2.zero; // Center it within the area
            card.GetComponent<RectTransform>().SetAsLastSibling(); // Puts it visually on top of other cards in the stack

            // Always show the back for cards in the draw pile
            SetCardFace(card, null, false); // Null for front sprite, as we just need to show back
        }
    }

    // Deals initial hands to players
    void DealInitialHands(int numCardsPerPlayer)
    {
        if (deck.Count < numCardsPerPlayer * 2)
        {
            Debug.LogWarning("Not enough cards in deck to deal " + numCardsPerPlayer + " to each player.");
            return;
        }

        // Deal to Player 1 (show front)
        for (int i = 0; i < numCardsPerPlayer; i++)
        {
            DrawCard(player1Hand, player1HandArea, true);
        }
        Debug.Log("Dealt " + player1Hand.Count + " cards to Player 1.");

        // Deal to Player 2 (opponent, show back)
        for (int i = 0; i < numCardsPerPlayer; i++)
        {
            DrawCard(player2Hand, player2HandArea, false);
        }
        Debug.Log("Dealt " + player2Hand.Count + " cards to Player 2.");
    }
    
    // --- Public Actions (Callable by UI Buttons) ---
    // --- New Wrapper Method for Draw Card Button ---
    public void OnDrawPlayer1CardButtonClicked()
    {
    // Call the existing DrawCard method, specifying Player 1's hand, area, and visibility
        DrawCard(player1Hand, player1HandArea, true);
    }
    // --- End New Wrapper Method ---
    
    // Draws a single card from the main deck to a specified hand/area
    public void DrawCard(List<GameObject> targetHand, RectTransform targetArea, bool showFront)
    {
        if (deck.Count == 0)
        {
            Debug.Log("Deck is empty! Cannot draw a card.");
            return;
        }

        GameObject cardToDraw = deck[0]; // Get the top card from the deck
        deck.RemoveAt(0); // Remove it from the deck list

        cardToDraw.transform.SetParent(targetArea); // Move card to the target area's parent

        // Get the card's specific front sprite reference (which we stored in its main Image component)
        Sprite originalFrontSprite = cardToDraw.GetComponent<Image>().sprite;
        SetCardFace(cardToDraw, originalFrontSprite, showFront); // Set its face based on 'showFront'

        // Arrange the cards in the target hand (simple horizontal spread)
        targetHand.Add(cardToDraw); // Add to the target hand list
        // Get the PlayableCard component and tell it which hand it belongs to
        PlayableCard playableCard = cardToDraw.GetComponent<PlayableCard>();
        if (playableCard != null)
        {
            playableCard.SetHandReference(targetHand);
        }
        else
        {
            Debug.LogWarning($"Card {cardToDraw.name} is missing PlayableCard component!", cardToDraw);
        }

        ArrangeHand(targetHand, targetArea);

        Debug.Log("Card drawn. Deck size: " + deck.Count + ", Hand size: " + targetHand.Count + " for " + targetArea.name);
    }

    // Moves a card from a player's hand to the discard pile
    // NOTE: This basic implementation assumes you'll manually assign a card for testing.
    // For a real game, cards in hand would be clickable and pass themselves as 'cardToPlay'.
    public void PlayCardToDiscardPile(GameObject cardToPlay, List<GameObject> sourceHand)
    {
        if (!sourceHand.Contains(cardToPlay))
        {
            Debug.LogWarning("Card is not in the specified hand or has already been played!", cardToPlay);
            return;
        }

        sourceHand.Remove(cardToPlay); // Remove card from the source hand
        discardPile.Add(cardToPlay);   // Add card to the discard pile
        
        // Clear the hand reference from the PlayableCard component
        PlayableCard playableCard = cardToPlay.GetComponent<PlayableCard>();
        if (playableCard != null)
        {
            playableCard.ClearHandReference();
        }
        cardToPlay.transform.SetParent(discardPileArea); // Move card to the discard area
        
        
        cardToPlay.transform.SetParent(discardPileArea); // Move card to the discard area

        // Get the card's specific front sprite reference
        Sprite originalFrontSprite = cardToPlay.GetComponent<Image>().sprite;
        SetCardFace(cardToPlay, originalFrontSprite, true); // Always show front on discard pile

        cardToPlay.GetComponent<RectTransform>().anchoredPosition = Vector2.zero; // Center on discard pile
        cardToPlay.GetComponent<RectTransform>().SetAsLastSibling(); // Ensure it's on top of other discards visually

        Debug.Log(cardToPlay.name + " played to discard pile. Discard pile size: " + discardPile.Count);

        // Re-arrange the cards remaining in the source hand
        ArrangeHand(sourceHand, sourceHand == player1Hand ? player1HandArea : player2HandArea);
    }

    // Helper function to arrange cards horizontally in a hand/area
    void ArrangeHand(List<GameObject> hand, RectTransform handArea)
    {
        if (hand.Count == 0) return;

        // Get the width of a single card from the prefab (assuming all cards are same size)
        float cardWidth = cardPrefab.GetComponent<RectTransform>().rect.width;
        float spacing = cardWidth * 0.7f; // Overlap cards slightly (e.g., 70% of card width)

        // Calculate starting X position to center the hand
        float totalHandWidth = (hand.Count - 1) * spacing;
        float startX = -totalHandWidth / 2f;

        for (int i = 0; i < hand.Count; i++)
        {
            RectTransform cardRect = hand[i].GetComponent<RectTransform>();
            if (cardRect != null)
            {
                cardRect.anchoredPosition = new Vector2(startX + i * spacing, 0);
                cardRect.SetAsLastSibling(); // Ensures the card is rendered on top of previous cards in the same hand
            }
        }
    }
}
