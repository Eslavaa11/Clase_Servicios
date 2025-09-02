using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UdpServerUI : MonoBehaviour
{
    public int serverPort = 5555;
    [SerializeField] private UDPServer _server;
    [SerializeField] private TMP_InputField messageInput;
    public void SendServerMessage()
    {
        if(!_server.isServerRunning){
            Debug.Log("The server is not running");
            return;
        }

        if(messageInput.text == ""){
            Debug.Log("The chat entry is empty");
            return;
        }

        string message = messageInput.text; // Get the text from the message entry
        _server.SendData(message); // Send message to the client
    }

    public void StartServer()
    {
        _server.StartUDPServer(serverPort);
    }
}
