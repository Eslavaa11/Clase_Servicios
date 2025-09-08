using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class UDPClient : MonoBehaviour
{
    private UdpClient udpClient;                 // Socket UDP del cliente
    private IPEndPoint remoteEndPoint;           // EndPoint del servidor

    public bool isServerConnected { get; private set; } = false;

    /// Evento para notificar a la UI cuando llega texto
    public event Action<string> OnMessageReceived;

    private void OnDisable()         => StopUDPClient();
    private void OnApplicationQuit() => StopUDPClient();

    /// Inicia el cliente (evita doble inicio)
    public void StartUDPClient(string ipAddress, int port)
    {
        StartUDPClient(ipAddress, port, /*sendHello*/ true);
    }

    public void StartUDPClient(string ipAddress, int port, bool sendHello)
    {
        if (udpClient != null) return; // ya iniciado

        remoteEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        udpClient = new UdpClient();   // puerto efímero local

        try
        {
            udpClient.BeginReceive(ReceiveData, null);
            if (sendHello) SendData("¡Servidor conectado!");
        }
        catch (Exception e)
        {
            Debug.LogError("[UDPClient] Start error: " + e.Message);
            StopUDPClient();
            return;
        }

        isServerConnected = false; // se confirmará al recibir
    }

    public void StopUDPClient()
    {
        try { udpClient?.Close(); } catch { }
        udpClient = null;
        isServerConnected = false;
    }

    private void ReceiveData(IAsyncResult ar)
    {
        try
        {
            if (udpClient == null) return;

            byte[] bytes = udpClient.EndReceive(ar, ref remoteEndPoint);
            string msg = Encoding.UTF8.GetString(bytes);

            if (!isServerConnected) isServerConnected = true;

            Debug.Log("[UDPClient] Received: " + msg);
            OnMessageReceived?.Invoke(msg);
        }
        catch (ObjectDisposedException)
        {
            // socket cerrado
        }
        catch (Exception e)
        {
            Debug.LogError("[UDPClient] Receive error: " + e.Message);
        }
        finally
        {
            if (udpClient != null)
            {
                try { udpClient.BeginReceive(ReceiveData, null); }
                catch (Exception e) { Debug.LogError("[UDPClient] BeginReceive error: " + e.Message); }
            }
        }
    }

    public void SendData(string message)
    {
        if (udpClient == null)
        {
            Debug.LogWarning("[UDPClient] Send called but client not started.");
            return;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            udpClient.Send(bytes, bytes.Length, remoteEndPoint);
            Debug.Log("[UDPClient] Sent: " + message);
        }
        catch (Exception e)
        {
            Debug.LogError("[UDPClient] Send error: " + e.Message);
        }
    }
}
