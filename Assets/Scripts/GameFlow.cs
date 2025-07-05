using Unity.Netcode;
using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using System.Collections;

public class GameFlow : NetworkBehaviour
{
    // This variable will be synced to all players. It holds the ID of the client whose turn it is.
    public NetworkVariable<ulong> CurrentPlayerId = new NetworkVariable<ulong>();
    public NetworkVariable<int> NumberOfPlayers = new NetworkVariable<int>(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString32Bytes> ActiveSuit = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<FixedString64Bytes> TurnInfoText = new NetworkVariable<FixedString64Bytes>();
    private List<ulong> turnOrder = new List<ulong>();
    public CardManager cardManager;
    public LobbyManager lobbyManager;
    public ConnectionManagerUI connectionManagerUI;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); // It's good practice to call the base method.
    }

    // Add this new public method to GameFlow.cs
    public void ServerSideGameStart()
    {
        // This method should only ever run on the server.
        if (!IsServer) return;

        // Sets the turn order and tells the CardManager to deal the cards.
        cardManager.ClearGameBoard();
        List<ulong> connectedPlayerIds = new List<ulong>(cardManager.players.Keys);
        StartGameWithPlayers(connectedPlayerIds);
        cardManager.StartGameSetup();
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

        if (cardManager != null && connectionManagerUI != null)
        {
            cardManager.RegisterPlayerNameServerRpc(connectionManagerUI.playerNameInputField.text);
        }


        connectionManagerUI.ShowGameUI();
        lobbyManager.HideLobby();
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
        string playerName = cardManager.GetPlayerName(playerId);
        TurnInfoText.Value = $"{playerName}'s turn";
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
        int nextPlayerIndex = (currentPlayerIndex - 1 + turnOrder.Count) % turnOrder.Count;
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
            TriggerGameOver(actingPlayer, cardData);
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

    private void TriggerGameOver(ulong winnerId, CardData winningCard)
    {
        if (!IsServer) return;

        string winningRank = winningCard.Rank.Value.ToString();

        foreach (ulong clientId in cardManager.players.Keys)
        {
            int playerScore = cardManager.CalculateScoreForHand(clientId, winnerId, winningRank);
            lobbyManager.UpdatePlayerScore(clientId, playerScore);
        }

        // 3. Tell all clients the game is over and it's time to return to the lobby.
        ReturnToLobbyClientRpc();
    }


    [ClientRpc]
    private void ReturnToLobbyClientRpc()
    {
        // This runs on all clients.
        // For now, it just prints a message. Later it will show the score panel.
        Debug.Log("Game over! Check the server log for scores. Returning to lobby soon...");

        StartCoroutine(LobbyReturnCoroutine());
    }


    private IEnumerator LobbyReturnCoroutine()
    {
        // Wait a few seconds for players to read the message.
        yield return new WaitForSeconds(3f);

        // Hide the game panel and show the lobby panel again.
        // We are NOT shutting down the network.
        cardManager.gamePanel.SetActive(false);
        lobbyManager.gameObject.SetActive(true);
    }




}
