using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class TCPServer : MonoBehaviour
{
    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream networkStream;
    private byte[] receiveBuffer;

    public bool isServerRunning { get; private set; } = false;

    /// Evento para notificar a la UI cuando llega texto
    public event Action<string> OnMessageReceived;

    private void OnDisable()         => StopServer();
    private void OnApplicationQuit() => StopServer();

    public void StartServer(int port)
    {
        if (isServerRunning) return;

        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            isServerRunning = true;
            Debug.Log($"[TCPServer] Started on port {port}. Waiting for connections...");
            tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null);
        }
        catch (Exception e)
        {
            Debug.LogError("[TCPServer] Start error: " + e.Message);
            StopServer();
        }
    }

    public void StopServer()
    {
        isServerRunning = false;

        try { networkStream?.Close(); } catch { }
        try { connectedClient?.Close(); } catch { }
        try { tcpListener?.Stop(); } catch { }

        networkStream = null;
        connectedClient = null;
        tcpListener = null;
    }

    private void HandleIncomingConnection(IAsyncResult ar)
    {
        if (!isServerRunning || tcpListener == null) return;

        try
        {
            // Acepta cliente
            connectedClient = tcpListener.EndAcceptTcpClient(ar);
            networkStream = connectedClient.GetStream();
            receiveBuffer = new byte[connectedClient.ReceiveBufferSize > 0 ? connectedClient.ReceiveBufferSize : 8192];

            Debug.Log("[TCPServer] Client connected: " + connectedClient.Client.RemoteEndPoint);

            // Empieza lectura del cliente aceptado
            networkStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, ReceiveData, null);
        }
        catch (ObjectDisposedException)
        {
            // Listener parado
            return;
        }
        catch (Exception e)
        {
            Debug.LogError("[TCPServer] Accept error: " + e.Message);
        }
        finally
        {
            // Sigue aceptando nuevos clientes (si quieres 1 solo, elimina esta línea)
            if (isServerRunning && tcpListener != null)
            {
                try { tcpListener.BeginAcceptTcpClient(HandleIncomingConnection, null); }
                catch (Exception e) { Debug.LogError("[TCPServer] BeginAccept error: " + e.Message); }
            }
        }
    }

    private void ReceiveData(IAsyncResult ar)
    {
        try
        {
            if (networkStream == null) return;

            int bytesRead = networkStream.EndRead(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("[TCPServer] Client disconnected.");
                try { connectedClient?.Close(); } catch { }
                connectedClient = null;
                networkStream = null;
                return;
            }

            var msg = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);
            Debug.Log("[TCPServer] Received: " + msg);

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
            Debug.LogError("[TCPServer] Receive error: " + e.Message);
        }
    }

    public void SendData(string message)
    {
        try
        {
            if (networkStream == null || connectedClient == null || !connectedClient.Connected)
            {
                Debug.LogWarning("[TCPServer] No client to send.");
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(message);
            networkStream.Write(bytes, 0, bytes.Length);
            networkStream.Flush();
            Debug.Log("[TCPServer] Sent: " + message);
        }
        catch (Exception e)
        {
            Debug.LogError("[TCPServer] Send error: " + e.Message);
        }
    }
}
