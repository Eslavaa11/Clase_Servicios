using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UDPServer : MonoBehaviour
{
    private UdpClient udpServer;
    private IPEndPoint anyEndPoint;            // para EndReceive
    private IPEndPoint lastClientEndPoint;     // último cliente que habló

    public bool isServerRunning = false;

    /// Evento para notificar a la UI cuando llega texto
    public event Action<string> OnMessageReceived;

    private void OnDisable()         => StopUDPServer();
    private void OnApplicationQuit() => StopUDPServer();

    public void StartUDPServer(int port)
    {
        if (udpServer != null) return; // ya iniciado

        try
        {
            udpServer = new UdpClient(port);
            anyEndPoint = new IPEndPoint(IPAddress.Any, 0); // 0: aceptamos cualquier puerto del cliente
            Debug.Log("[UDPServer] Started on port " + port);
            udpServer.BeginReceive(ReceiveData, null);
            isServerRunning = true;
        }
        catch (Exception e)
        {
            Debug.LogError("[UDPServer] Start error: " + e.Message);
            StopUDPServer();
        }
    }

    public void StopUDPServer()
    {
        try { udpServer?.Close(); } catch { }
        udpServer = null;
        isServerRunning = false;
        lastClientEndPoint = null;
    }

    private void ReceiveData(IAsyncResult ar)
    {
        try
        {
            if (udpServer == null) return;

            byte[] bytes = udpServer.EndReceive(ar, ref anyEndPoint);
            lastClientEndPoint = anyEndPoint;               // recordamos el remitente
            string msg = Encoding.UTF8.GetString(bytes);

            Debug.Log("[UDPServer] Received: " + msg);
            OnMessageReceived?.Invoke(msg);                 // notifica a la UI del servidor

            
        }
        catch (ObjectDisposedException)
        {
            // socket cerrado
        }
        catch (Exception e)
        {
            Debug.LogError("[UDPServer] Receive error: " + e.Message);
        }
        finally
        {
            if (udpServer != null)
            {
                try { udpServer.BeginReceive(ReceiveData, null); }
                catch (Exception e) { Debug.LogError("[UDPServer] BeginReceive error: " + e.Message); }
            }
        }
    }

    public void SendData(string message)
    {
        if (udpServer == null)
        {
            Debug.LogWarning("[UDPServer] Send called but server not started.");
            return;
        }
        if (lastClientEndPoint == null)
        {
            Debug.LogWarning("[UDPServer] No client endpoint yet. Envía después de recibir el primer mensaje del cliente.");
            return;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            udpServer.Send(bytes, bytes.Length, lastClientEndPoint);
            Debug.Log("[UDPServer] Sent: " + message);
        }
        catch (Exception e)
        {
            Debug.LogError("[UDPServer] Send error: " + e.Message);
        }
    }
}
