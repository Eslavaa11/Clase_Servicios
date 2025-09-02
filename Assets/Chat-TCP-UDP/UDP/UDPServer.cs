using System;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UDPServer : MonoBehaviour
{
    private UdpClient udpServer; // UDP client to handle network communication
    private IPEndPoint remoteEndPoint; // Endpoint to identify the remote client

    public bool isServerRunning = false; // Flag to check if the server is running

    public void StartUDPServer(int port)
    {
        udpServer = new UdpClient(port); // Initializes the UDP client to listen on the given port
        remoteEndPoint = new IPEndPoint(IPAddress.Any, port); // Configures the endpoint to accept messages from any IP address on the given port.
        Debug.Log("Server started. Waiting for messages...");
        udpServer.BeginReceive(ReceiveData, null); // Asynchronous data reception begins
        isServerRunning = true; // Sets the server running flag to true
    }

    private void ReceiveData(IAsyncResult result)
    {
        byte[] receivedBytes = udpServer.EndReceive(result, ref remoteEndPoint); // Completes data reception and gets the received bytes.
        string receivedMessage = System.Text.Encoding.UTF8.GetString(receivedBytes); // Converts received bytes to a string
        Debug.Log("Received from client: " + receivedMessage);
        udpServer.BeginReceive(ReceiveData, null); // Continues to receive data asynchronously
    }

    public void SendData(string message)
    {
        byte[] sendBytes = System.Text.Encoding.UTF8.GetBytes(message); // Converts the message to a byte array
        udpServer.Send(sendBytes, sendBytes.Length, remoteEndPoint); // Sends bytes to the remote client using UDP
        Debug.Log("Sent to client: " + message);
    }
}
