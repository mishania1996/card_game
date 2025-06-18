using UnityEngine;
using UnityEngine.EventSystems; // Needed for IPointerClickHandler
using System.Collections.Generic; // Needed for List<GameObject> if we pass the hand directly

public class PlayableCard : MonoBehaviour, IPointerClickHandler
{
    private CardManager cardManager; // Reference to the main CardManager
    private List<GameObject> myCurrentHand; // To know which hand this card belongs to

    void Awake()
    {
        // Find the CardManager in the scene
        cardManager = FindAnyObjectByType<CardManager>();
        if (cardManager == null)
        {
            Debug.LogError("CardManager not found in the scene! Make sure it's present and active.", this);
        }
    }

    // This method is called by Unity's Event System when the card GameObject is clicked
    public void OnPointerClick(PointerEventData eventData)
    {
        // Only allow playing if the card is in a hand (not deck or discard pile)
        // And potentially, only if it's the player's hand, not opponent's.
        // For now, any card in a hand is playable by clicking.
        if (myCurrentHand != null && cardManager != null)
        {
            Debug.Log($"Card '{gameObject.name}' clicked! Attempting to play.", this);
            cardManager.PlayCardToDiscardPile(gameObject, myCurrentHand);
        }
        else
        {
            Debug.LogWarning($"Card '{gameObject.name}' clicked but cannot be played (not in hand or no CardManager).", this);
        }
    }

    // Call this method from CardManager when the card is drawn into a hand
    public void SetHandReference(List<GameObject> handList)
    {
        myCurrentHand = handList;
    }

    // Call this from CardManager when the card is moved OUT of a hand (e.g., to discard)
    public void ClearHandReference()
    {
        myCurrentHand = null;
    }
}
