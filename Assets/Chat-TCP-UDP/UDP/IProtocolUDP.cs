using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
public interface IProtocolUDP
{
    public bool isServer { get; set; }
    public event Action OnConnected;
    public event Action<string> OnDataReceived;
    public void StartUDP(string ipAddress, int port);
    public void ReceiveData(IAsyncResult result);
    public void SendData(string message);
}
