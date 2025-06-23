using Unity.Netcode;
using Unity.Collections;

public class CardData : NetworkBehaviour
{
    // These variables will be automatically synced from the server to all clients.
    public NetworkVariable<FixedString32Bytes> Suit = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<FixedString32Bytes> Rank = new NetworkVariable<FixedString32Bytes>();
}
