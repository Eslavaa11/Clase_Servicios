using System;
using System.Collections.Concurrent;
using UnityEngine;

public class PlayerHost : MonoBehaviour
{
    public UDPProtocol protocolUDP;

    [SerializeField] private GameObject playerPrefab;
    public Transform multiplayerTransform;
    public Transform spawnPosition;

    private ConcurrentQueue<Vector3> positionQueue = new ConcurrentQueue<Vector3>();
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    void Start()
    {
        if (protocolUDP == null)
        {
            Debug.LogError("UDPProtocol reference is missing!");
            return;
        }

        protocolUDP.OnConnected += () => mainThreadActions.Enqueue(PlayerConnection);
        protocolUDP.OnDataReceived += ReceivePosition;
        
    }

    public void StartProtocol()
    {
        protocolUDP.StartUDP("127.0.0.1", 12345);
    }

    void PlayerConnection()
    {
        Debug.Log("Instance");
        multiplayerTransform = Instantiate(playerPrefab, spawnPosition).transform;
    }

   void Update()
    {
        
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
        if (protocolUDP.isServer)
        {
            while (positionQueue.TryDequeue(out Vector3 newPosition))
            {
                if (multiplayerTransform != null)
                    multiplayerTransform.position = newPosition;
            }
        }else
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SendPosition();
            }
        }
        
   }
        
    

    public void SendPosition()
    {
        if (multiplayerTransform == null)
        {
            Debug.LogError("Player not instantiated!");
            return;
        }

        Vector3 position = multiplayerTransform.position;
        string positionData = $"{position.x};{position.y};{position.z}";
        Debug.Log($"Sending position: {positionData}");
        protocolUDP.SendData(positionData);
    }

    public void ReceivePosition(string positionData)
    {
        if (multiplayerTransform == null)
        {
            Debug.LogError("Player not instantiated!");
            return;
        }

        try
        {
            string[] values = positionData.Split(';');
            if (values.Length != 3)
            {
                Debug.LogError("Invalid position data received.");
                return;
            }

            float x = float.Parse(values[0]);
            float y = float.Parse(values[1]);
            float z = float.Parse(values[2]);

            positionQueue.Enqueue(new Vector3(x, y, z));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing position data: {ex.Message}");
        }
    }
}
