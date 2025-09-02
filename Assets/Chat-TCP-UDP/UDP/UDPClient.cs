using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class UDPClient : MonoBehaviour
{
    private UdpClient udpClient; // UDP client to handle network communication
    private IPEndPoint remoteEndPoint; // Endpoint to identify the remote server
    public bool isServerConnected = false; // Flag to check if the client is connected to the server

    public void StartUDPClient(string ipAddress, int port)
    {
        udpClient = new UdpClient(); // Initializes the UDP client without binding to any local port
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port); // Sets the remote server endpoint using the given IP address and port
        udpClient.BeginReceive(ReceiveData, null); // Starts receiving data from the server asynchronously
        SendData("Hello, server!");
        isServerConnected = true; // Sets the client connected flag to true
    }

    private void ReceiveData(IAsyncResult result)
    {
        byte[] receivedBytes = udpClient.EndReceive(result, ref remoteEndPoint); // Completes the data reception and retrieves the received bytes
        string receivedMessage = System.Text.Encoding.UTF8.GetString(receivedBytes); // Converts the received bytes into a text message
        Debug.Log("Received from server: " + receivedMessage);
        udpClient.BeginReceive(ReceiveData, null); // Continues receiving data asynchronously
    }

    public void SendData(string message)
    {
        byte[] sendBytes = System.Text.Encoding.UTF8.GetBytes(message); // Converts the message into a byte array
        udpClient.Send(sendBytes, sendBytes.Length, remoteEndPoint); // Sends the bytes to the remote server using UDP
        Debug.Log("Sent to server: " + message);
    }
}
