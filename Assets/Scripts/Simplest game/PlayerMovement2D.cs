using Unity.Netcode;
using UnityEngine;

public class PlayerMovement2D : NetworkBehaviour
{
    public float moveSpeed = 5f;

    // This check is the most important part of a multiplayer script.
    // It makes sure this code only runs for the player that you control,
    // and not for any other player's character in the game.
    void Update()
    {
        if (!IsOwner) return;

        // Get input from the horizontal and vertical axes (arrow keys or WASD)
        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");

        // Create a movement vector for 2D
        Vector3 move = new Vector3(moveX, moveY, 0);

        // Move the player's position
        transform.position += move * moveSpeed * Time.deltaTime;
    }
}
