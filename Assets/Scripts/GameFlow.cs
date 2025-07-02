using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;

public class GameFlow : NetworkBehaviour
{
    // This variable will be synced to all players. It holds the ID of the client whose turn it is.
    public NetworkVariable<ulong> CurrentPlayerId = new NetworkVariable<ulong>();
    public NetworkVariable<int> NumberOfPlayers = new NetworkVariable<int>(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> ActiveSuit = new NetworkVariable<FixedString32Bytes>();
    private List<ulong> turnOrder = new List<ulong>();
    private CardManager cardManager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); // It's good practice to call the base method.
        cardManager = FindAnyObjectByType<CardManager>();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetActiveSuitServerRpc(string chosenSuit, ulong actingPlayerId)
    {
        Debug.Log($"Player {actingPlayerId} chose {chosenSuit} as the new suit.");
        ActiveSuit.Value = chosenSuit;

        // Now that the choice is made, we can switch the turn.
        SetPlayerTurn(GetNextPlayerInTurn(actingPlayerId));
    }

    [ClientRpc]
    public void StartGameClientRpc()
    {
        // Find the ConnectionManagerUI in the scene and tell it to show the game panel.
        FindAnyObjectByType<ConnectionManagerUI>().ShowGameUI();
    }


    [ClientRpc]
    public void PromptForSuitChoiceClientRpc(ClientRpcParams clientRpcParams = default)
    {
        // This will run on the client who played the Jack
        cardManager.ShowSuitChoicePanel();
    }

    [ClientRpc]
    private void InitializeClientRpc(int playerIndex, ClientRpcParams clientRpcParams = default)
    {
        // Find the CardManager on the client and pass the info
        cardManager.SetLocalPlayerInfo(playerIndex);
    }

    public void StartGameWithPlayers(List<ulong> playerIds)
    {
        if (!IsServer) return;

        turnOrder = playerIds;

        // Set the turn to the first player in the list
        SetPlayerTurn(turnOrder[0]);

        for (int i = 0; i < turnOrder.Count; i++)
        {
            ulong clientId = turnOrder[i];
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            InitializeClientRpc(i, clientRpcParams);
        }
    }

    // A server-only function to set the turn to a specific player.
    public void SetPlayerTurn(ulong playerId)
    {
        if (!IsServer) return;
        CurrentPlayerId.Value = playerId;
        cardManager.active_player_has_drawn = false;
    }

    public ulong GetNextPlayerInTurn(ulong currentPlayerId)
    {
        int currentPlayerIndex = turnOrder.IndexOf(currentPlayerId);
        int nextPlayerIndex = (currentPlayerIndex + 1) % turnOrder.Count;
        return turnOrder[nextPlayerIndex];
    }

    public ulong GetNextNextPlayerInTurn(ulong currentPlayerId)
    {
        int currentPlayerIndex = turnOrder.IndexOf(currentPlayerId);
        int nextPlayerIndex = (currentPlayerIndex + 2) % turnOrder.Count;
        return turnOrder[nextPlayerIndex];
    }

    public ulong GetPrevPlayerInTurn(ulong currentPlayerId)
    {
        int currentPlayerIndex = turnOrder.IndexOf(currentPlayerId);
        int nextPlayerIndex = (currentPlayerIndex - 1) % turnOrder.Count;
        return turnOrder[nextPlayerIndex];
    }


    public void SwitchTurn(ulong playerWhoJustPlayed)
    {
        if (!IsServer) return;

        // Determine who the next player is.

        int currentPlayerIndex = turnOrder.IndexOf(playerWhoJustPlayed);
        int nextPlayerIndex = (currentPlayerIndex + 1) % turnOrder.Count;
        ulong nextPlayerId = turnOrder[nextPlayerIndex];

        // Set the turn to the next player.
        SetPlayerTurn(nextPlayerId);
        Debug.Log($"Turn switched. It is now Client {nextPlayerId}'s turn.");
    }

    // This server-side method checks if a card can be legally played.
    public bool IsValidPlay(CardData cardToPlay, CardData topOfDiscard)
    {
        if (topOfDiscard == null)
        {
            return true;
        }

        if (cardToPlay.Rank.Value == "jack")
        {
            return true;
        }

        if (topOfDiscard.Rank.Value == "jack")
        {
            // The play is only valid if the played card's suit matches the ActiveSuit
            return cardToPlay.Suit.Value == ActiveSuit.Value;
        }

        // The move is valid if the suits match OR the ranks match.
        if (cardToPlay.Suit.Value == topOfDiscard.Suit.Value || cardToPlay.Rank.Value == topOfDiscard.Rank.Value)
        {
            return true;
        }

        if (topOfDiscard.Rank.Value == "8")
        {
            return true;
        }

        return false;
    }


    public void ApplyPower(ulong actingPlayer, CardData cardData)
    {
        if ((cardData.Rank.Value == "queen" || cardData.Rank.Value == "jack")  && cardManager.players[actingPlayer].Hand.Count == 0 )
        {
            TriggerGameOverClientRpc(actingPlayer);
            return;
        }

        if (cardData.Rank.Value == "6")
        {
            ulong actedPlayerId = GetPrevPlayerInTurn(actingPlayer);
            cardManager.DrawCard(actedPlayerId, true);
            if (NumberOfPlayers.Value > 2)
            {
                SetPlayerTurn(GetNextPlayerInTurn(actingPlayer));
                return;
            }
            if(cardManager.players[actingPlayer].Hand.Count == 0) cardManager.active_player_has_drawn = false;
            return;
        }
        if (cardData.Rank.Value == "7")
        {
            ulong actedPlayerId = GetNextPlayerInTurn(actingPlayer);
            cardManager.DrawCard(actedPlayerId, true);
            cardManager.DrawCard(actedPlayerId, true);

            if (NumberOfPlayers.Value > 2)
            {
                SetPlayerTurn(GetNextNextPlayerInTurn(actingPlayer));
                return;
            }

            if(cardManager.players[actingPlayer].Hand.Count == 0) cardManager.active_player_has_drawn = false;
            return;
        }

        if (cardData.Rank.Value == "9" && cardData.Suit.Value == "diamonds")
        {
            ulong actedPlayerId = GetNextPlayerInTurn(actingPlayer);
            for (int i = 0; i < 5; i++)
            {
                cardManager.DrawCard(actedPlayerId, true);
            }

            if (NumberOfPlayers.Value > 2)
            {
                SetPlayerTurn(GetNextNextPlayerInTurn(actingPlayer));
                return;
            }

            if(cardManager.players[actingPlayer].Hand.Count == 0) cardManager.active_player_has_drawn = false;
            return;
        }

        if (cardData.Rank.Value == "jack")
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { actingPlayer }
                }
            };
            PromptForSuitChoiceClientRpc(clientRpcParams);
            return; // Stop here until the player makes a choice
        }

        if (cardData.Rank.Value == "ace")
        {
            SetPlayerTurn(GetNextNextPlayerInTurn(actingPlayer));
            return;
        }

        if (cardData.Rank.Value == "8")
        {
            if(cardManager.players[actingPlayer].Hand.Count == 0) cardManager.active_player_has_drawn = false;
            return;
        }

        SetPlayerTurn(GetNextPlayerInTurn(actingPlayer));
    }

    [ClientRpc]
    private void TriggerGameOverClientRpc(ulong winnerId)
    {
        // This runs on ALL clients, including the host.
        // Each client checks if THEY are the winner.
        cardManager.endGamePanel.SetActive(true);
        if (winnerId == NetworkManager.Singleton.LocalClientId)
        {
            cardManager.ShowWinScreen();
        }
        else
        {
            cardManager.ShowLoseScreen();
        }

        // Start a coroutine on each client to wait 3 seconds, then reset.
        StartCoroutine(EndGameAndReset());
    }

    private System.Collections.IEnumerator EndGameAndReset()
    {
        // Wait for 3 seconds to show the win/lose screen.
        yield return new WaitForSeconds(3f);

        // This is the simplest and most robust way to reset the game.
        // It shuts down the current network session.
        NetworkManager.Singleton.Shutdown();

        // It then reloads the very first scene (build index 0) to start over.
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }


}
