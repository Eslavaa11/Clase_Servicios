using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UdpClientUI: MonoBehaviour
{
    public int serverPort = 5555;
    public string serverAddress = "127.0.0.1";
    [SerializeField] private UDPClient _client;
    [SerializeField] private TMP_InputField messageInput;
    public void SendClientMessage()
    {
        if(!_client.isServerConnected)
        {
            Debug.Log("The client is not connected");
            return;
        }

        if(messageInput.text == ""){
            Debug.Log("The chat entry is empty");
            return;
        }

        string message = messageInput.text;
        _client.SendData(message);
    }

    public void ConnectClient()
    {
        _client.StartUDPClient(serverAddress, serverPort);
    }
}
