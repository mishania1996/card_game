using UnityEngine;
using UnityEngine.UI; // Needed for the Image component

public class CardFlipper : MonoBehaviour
{
    public Sprite cardFrontSprite; // Drag your card front image here in the Inspector
    public Sprite cardBackSprite;  // Drag your card back image here in the Inspector

    private Image cardImage; // Reference to the Image component on THIS GameObject
    private bool isFrontShowing = true; // To keep track of which side is visible

    void Awake() // Awake is called when the script instance is being loaded
    {
        // Get the Image component that's attached to this GameObject (the Card itself)
        cardImage = GetComponent<Image>();

        // Ensure the front sprite is shown initially
        if (cardImage != null && cardFrontSprite != null)
        {
            cardImage.sprite = cardFrontSprite;
        }
    }

    public void FlipCard()
    {
        if (cardImage == null)
        {
            Debug.LogError("Card Image component not found on this GameObject!", this);
            return;
        }

        if (isFrontShowing)
        {
            // If front is showing, switch to back
            if (cardBackSprite != null)
            {
                cardImage.sprite = cardBackSprite;
                isFrontShowing = false;
            }
            else
            {
                Debug.LogWarning("Card Back Sprite is not assigned!", this);
            }
        }
        else
        {
            // If back is showing, switch to front
            if (cardFrontSprite != null)
            {
                cardImage.sprite = cardFrontSprite;
                isFrontShowing = true;
            }
            else
            {
                Debug.LogWarning("Card Front Sprite is not assigned!", this);
            }
        }
        Debug.Log("Card Flipped! Is front showing: " + isFrontShowing);
    }
}
