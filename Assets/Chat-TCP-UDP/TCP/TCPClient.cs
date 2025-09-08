using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class TCPClient : MonoBehaviour
{
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private byte[] receiveBuffer;

    public bool isServerConnected { get; private set; } = false;

    /// Evento para notificar a la UI cuando llega texto
    public event Action<string> OnMessageReceived;

    private void OnDisable()         => Disconnect();
    private void OnApplicationQuit() => Disconnect();

    public void ConnectToServer(string ipAddress, int port)
    {
        if (tcpClient != null && tcpClient.Connected) return;

        try
        {
            tcpClient = new TcpClient();
            tcpClient.Connect(IPAddress.Parse(ipAddress), port);

            networkStream = tcpClient.GetStream();
            receiveBuffer = new byte[tcpClient.ReceiveBufferSize > 0 ? tcpClient.ReceiveBufferSize : 8192];

            isServerConnected = true;
            Debug.Log($"[TCPClient] Connected to {ipAddress}:{port}");

            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
        }
        catch (Exception e)
        {
            Debug.LogError("[TCPClient] Connect error: " + e.Message);
            Disconnect();
        }
    }

    public void Disconnect()
    {
        isServerConnected = false;

        try { networkStream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }

        networkStream = null;
        tcpClient = null;
    }

    private void ReceiveData(IAsyncResult ar)
    {
        try
        {
            if (networkStream == null) return;

            int bytesRead = networkStream.EndRead(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("[TCPClient] Server closed connection.");
                Disconnect();
                return;
            }

            var msg = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);
            Debug.Log("[TCPClient] Received: " + msg);

            // Notifica a la UI (ChatUIManager se suscribe)
            OnMessageReceived?.Invoke(msg);

            // Sigue leyendo
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
        }
        catch (ObjectDisposedException)
        {
            // stream cerrado
        }
        catch (Exception e)
        {
            Debug.LogError("[TCPClient] Receive error: " + e.Message);
            Disconnect();
        }
    }

    public void SendData(string message)
    {
        try
        {
            if (networkStream == null || tcpClient == null || !tcpClient.Connected)
            {
                Debug.LogWarning("[TCPClient] Not connected; cannot send.");
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(message);
            networkStream.Write(bytes, 0, bytes.Length);
            networkStream.Flush();
            Debug.Log("[TCPClient] Sent: " + message);
        }
        catch (Exception e)
        {
            Debug.LogError("[TCPClient] Send error: " + e.Message);
            Disconnect();
        }
    }
}
