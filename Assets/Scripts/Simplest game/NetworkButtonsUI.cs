using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkButtonsUI : MonoBehaviour
{
    public Button hostButton;
    public Button clientButton;

    void Start()
    {
        hostButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartHost();
            gameObject.SetActive(false); // Hide buttons after connecting
        });

        clientButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
            gameObject.SetActive(false); // Hide buttons after connecting
        });
    }
}

