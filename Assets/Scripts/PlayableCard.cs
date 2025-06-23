using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Unity.Netcode;

public class PlayableCard : NetworkBehaviour, IPointerClickHandler
{
    private CardManager cardManager; 
    private List<GameObject> myCurrentHand; 

    void Awake()
    {
        // Find the CardManager in the scene
        cardManager = FindAnyObjectByType<CardManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // A client can only play a card if it's their turn and the card is in their hand.
        // The 'myCurrentHand' reference is now correctly set on each client by the CardManager's ClientRpc.
        if (myCurrentHand != null && cardManager != null)
        {
            Debug.Log($"Client {NetworkManager.Singleton.LocalClientId}: Card '{gameObject.name}' clicked! Sending request to server.", this);
            
            // Call the ServerRpc to request playing the card.
            PlayCardServerRpc();
        }
    }

    
    [ServerRpc(RequireOwnership = false)]
    void PlayCardServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong requestingClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"Server received request from Client {requestingClientId} to play card '{gameObject.name}'", this);
        
        // Pass the requesting client's ID to the manager for validation
        cardManager.PlayCardToDiscardPile(gameObject, requestingClientId);
    }


    public void SetHandReference(List<GameObject> handList)
    {
        myCurrentHand = handList;
    }

    public void ClearHandReference()
    {
        myCurrentHand = null;
    }
}
