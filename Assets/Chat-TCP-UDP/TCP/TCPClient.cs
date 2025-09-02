using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class TCPClient : MonoBehaviour
{
    private TcpClient tcpClient; // TCP client to connect to the server
    private NetworkStream networkStream; // Network data stream for sending and receiving data
    private byte[] receiveBuffer; // Buffer to store the data received from the server

    public bool isServerConnected;

    public void ConnectToServer(string ipAddress, int port)
    {
        tcpClient = new TcpClient(); // Initializes the TCP client
        tcpClient.Connect(IPAddress.Parse(ipAddress), port); // Connects the client to the server using the given IP address and port
        networkStream = tcpClient.GetStream(); // Gets the network data stream for communication with the server
        receiveBuffer = new byte[tcpClient.ReceiveBufferSize]; // Initializes the receive buffer with the client's buffer size
        networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null); // Starts reading data from the network stream asynchronously
        isServerConnected = true;
    }

    private void ReceiveData(IAsyncResult result)
    {
        int bytesRead = networkStream.EndRead(result); // Completes the data reading from the network stream and gets the number of bytes read
        byte[] receivedBytes = new byte[bytesRead]; // Copies the received data into a new byte array
        Array.Copy(receiveBuffer, receivedBytes, bytesRead);
        string receivedMessage = System.Text.Encoding.UTF8.GetString(receivedBytes); // Converts the received bytes into a text message
        Debug.Log("Received from server: " + receivedMessage); // Displays the message received from the server in the console
        networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null); // Continues reading data from the network stream asynchronously
    }

    public void SendData(string message)
    {
        try
        {
            byte[] sendBytes = System.Text.Encoding.UTF8.GetBytes(message); // Converts the message into a byte array
            networkStream.Write(sendBytes, 0, sendBytes.Length); // Writes the bytes to the network stream to send them to the client
            networkStream.Flush(); // Clears the data stream to ensure data is sent
            Debug.Log("Sent to client: " + message); // Displays a message in the console indicating that the message has been sent
        }
        catch
        {
            Debug.Log("There is no client to send the message: " + message);
        }
    }
}

