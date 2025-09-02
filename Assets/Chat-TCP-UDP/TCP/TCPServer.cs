using System; 
using System.Net;
using System.Net.Sockets; 
using UnityEngine;

public class TCPServer : MonoBehaviour
{
    private TcpListener tcpListener; // TCP server declaration
    private TcpClient connectedClient; // Connected client declaration
    private NetworkStream networkStream; // Network data stream
    private byte[] receiveBuffer; // Buffer to store received data

    public bool isServerRunning;

    public void StartServer(int port)
    {
        tcpListener = new TcpListener(IPAddress.Any, port); // Configures the TCP server to listen on any IP and the specified port
        tcpListener.Start(); // Starts the TCP server
        Debug.Log("Server started, waiting for connections..."); // Displays a message in the Unity console indicating that the server has started
        tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null); // Begins asynchronously accepting clients
        isServerRunning = true;
    }

    private void HandleIncomingConnection(IAsyncResult result)
    {
        connectedClient = tcpListener.EndAcceptTcpClient(result); // Completes client acceptance and establishes the connection
        networkStream = connectedClient.GetStream(); // Retrieves the network data stream from the connected client
        Debug.Log("Client connected: " + connectedClient.Client.RemoteEndPoint); // Displays a message in the console indicating that a client has connected
        receiveBuffer = new byte[connectedClient.ReceiveBufferSize]; // Initializes the reception buffer with the client's buffer size
        networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null); // Begins asynchronously reading data from the network stream
        tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null); // Continues waiting for new client connections asynchronously
    }

    private void ReceiveData(IAsyncResult result)
    {
        int bytesRead = networkStream.EndRead(result); // Completes reading data from the network stream and gets the number of bytes read
        
        if (bytesRead <= 0) // If no bytes are read, the client has disconnected
        {
            Debug.Log("Client disconnected: " + connectedClient.Client.RemoteEndPoint); // Displays a message in the console indicating that the client has disconnected
            connectedClient.Close(); // Closes the connection with the client
            return;
        }
        byte[] receivedBytes = new byte[bytesRead]; // Copies the received data into a new byte array
        Array.Copy(receiveBuffer, receivedBytes, bytesRead);
        string receivedMessage = System.Text.Encoding.UTF8.GetString(receivedBytes); // Converts the received bytes into a text message
        Debug.Log("Received from client: " + receivedMessage); // Displays the received message in the console
        networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null); // Continues reading data from the network stream asynchronously
    }

    public void SendData(string message)
    {
        try{
            byte[] sendBytes = System.Text.Encoding.UTF8.GetBytes(message); // Converts the message into a byte array
            networkStream.Write(sendBytes, 0, sendBytes.Length); // Writes the bytes to the network stream to send them to the client
            networkStream.Flush(); // Clears the data stream to ensure data is sent
            Debug.Log("Sent to client: " + message); // Displays a message in the console indicating that the message has been sent
        }
        catch{
            Debug.Log("There is no client to send the message: " + message);
        }
        
    }

}

