using Unity.Netcode;
using UnityEngine;

public class GameFlow : NetworkBehaviour
{
    // This variable will be synced to all players. It holds the ID of the client whose turn it is.
    public NetworkVariable<ulong> CurrentPlayerId = new NetworkVariable<ulong>();

    private CardManager cardManager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); // It's good practice to call the base method.
        cardManager = FindAnyObjectByType<CardManager>();
    }

    // A server-only function to set the turn to a specific player.
    public void SetPlayerTurn(ulong playerId)
    {
        if (!IsServer) return;
        CurrentPlayerId.Value = playerId;
    }

    // This server-side method checks if a card can be legally played.
    public bool IsValidPlay(CardData cardToPlay, CardData topOfDiscard)
    {
        if (topOfDiscard == null)
        {
            return true;
        }

        // The move is valid if the suits match OR the ranks match.
        if (cardToPlay.Suit.Value == topOfDiscard.Suit.Value || cardToPlay.Rank.Value == topOfDiscard.Rank.Value)
        {
            return true;
        }

        if (topOfDiscard.Rank.Value == "8" || cardToPlay.Rank.Value == "jack")
        {
            return true;
        }

        return false;
    }

    public void SwitchTurn(ulong playerWhoJustPlayed)
    {
        if (!IsServer) return;

        // Determine who the next player is.

        ulong nextPlayerId = (playerWhoJustPlayed == cardManager.player1_clientId) ? cardManager.player2_clientId : cardManager.player1_clientId;

        // Set the turn to the next player.
        SetPlayerTurn(nextPlayerId);
        Debug.Log($"Turn switched. It is now Client {nextPlayerId}'s turn.");
    }

    public void ApplyPower(ulong actingPlayer, ulong actedPlayer, CardData cardData)
    {
        if (cardData.Rank.Value == "6")
        {
            cardManager.DrawCard(actedPlayer, true);
            return;
        }
        if (cardData.Rank.Value == "7")
        {
            cardManager.DrawCard(actedPlayer, true);
            cardManager.DrawCard(actedPlayer, true);
            return;
        }

        if (cardData.Rank.Value == "9" && cardData.Suit.Value == "diamonds")
        {
            for (int i = 0; i < 5; i++)
            {
                cardManager.DrawCard(actedPlayer, true);
            }
            return;
        }

        if (cardData.Rank.Value == "ace" || cardData.Rank.Value == "8") return ;
        SwitchTurn(actingPlayer);
    }


}
