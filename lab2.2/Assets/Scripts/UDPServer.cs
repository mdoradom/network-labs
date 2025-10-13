using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UDPServer : MonoBehaviour
{
    [Header("UDP Server Settings")]
    [SerializeField] private int port = 9050;
    [SerializeField] private string roomName = "Unity UDP Game Room";
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI connectedClientsText;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private ScrollRect chatDisplayText;
    [SerializeField] private Button sendMessageButton;
    [SerializeField] private Button startServerButton;
    [SerializeField] private Button stopServerButton;
    
    private Socket serverSocket;
    private bool isServerRunning = false;
    private Thread serverThread;
    private Dictionary<IPEndPoint, ClientInfo> connectedClients = new Dictionary<IPEndPoint, ClientInfo>();
    private List<string> chatMessages = new List<string>();
    
    // Thread-safe queue for UI updates
    private Queue<System.Action> mainThreadActions = new Queue<System.Action>();
    private readonly object lockObject = new object();
    
    private class ClientInfo
    {
        public string username;
        public IPEndPoint endPoint;
        public DateTime lastSeen;
        
        public ClientInfo(string name, IPEndPoint ep)
        {
            username = name;
            endPoint = ep;
            lastSeen = DateTime.Now;
        }
    }
    
    void Start()
    {
        SetupUI();
    }
    
    void Update()
    {
        lock (lockObject)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue().Invoke();
            }
        }
    }
    
    void SetupUI()
    {
        if (startServerButton != null)
            startServerButton.onClick.AddListener(StartServer);
        
        if (stopServerButton != null)
            stopServerButton.onClick.AddListener(StopServer);
        
        if (sendMessageButton != null)
            sendMessageButton.onClick.AddListener(SendChatMessage);
        
        UpdateUI();
    }
    
    public void StartServer()
    {
        if (isServerRunning) return;
        
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            
            serverSocket.Bind(localEndPoint);
            
            isServerRunning = true;
            
            serverThread = new Thread(ServerLoop);
            serverThread.Start();
            
            QueueMainThreadAction(() => {
                Debug.Log($"UDP Server started on port {port}");
                UpdateUI();
            });
        }
        catch (SocketException ex)
        {
            Debug.LogError($"Failed to start UDP server: {ex.Message}");
            QueueMainThreadAction(() => UpdateUI());
        }
    }
    
    public void StopServer()
    {
        if (!isServerRunning) return;
        
        isServerRunning = false;
        
        try
        {
            lock (connectedClients)
            {
                connectedClients.Clear();
            }
            
            serverSocket?.Close();
            serverThread?.Join(2000);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error stopping server: {ex.Message}");
        }
        
        QueueMainThreadAction(() => {
            Debug.Log("UDP Server stopped");
            UpdateUI();
        });
    }
    
    void ServerLoop()
    {
        byte[] buffer = new byte[1024];
        
        while (isServerRunning)
        {
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint senderEndPoint = sender;
                
                int received = serverSocket.ReceiveFrom(buffer, ref senderEndPoint);
                
                if (received > 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, received);
                    IPEndPoint clientEndPoint = (IPEndPoint)senderEndPoint;
                    
                    QueueMainThreadAction(() => {
                        Debug.Log($"Received from {clientEndPoint}: {message}");
                    });
                    
                    HandleMessage(message, clientEndPoint);
                }
            }
            catch (SocketException)
            {
                if (isServerRunning)
                    QueueMainThreadAction(() => Debug.LogError("UDP Server socket error"));
                break;
            }
        }
    }
    
    void HandleMessage(string message, IPEndPoint clientEndPoint)
    {
        try
        {
            if (message.StartsWith("JOIN:"))
            {
                string username = message.Substring(5);
                
                lock (connectedClients)
                {
                    connectedClients[clientEndPoint] = new ClientInfo(username, clientEndPoint);
                }
                
                // Send welcome message
                string welcomeMessage = $"WELCOME:{roomName}";
                byte[] response = Encoding.ASCII.GetBytes(welcomeMessage);
                serverSocket.SendTo(response, clientEndPoint);
                
                QueueMainThreadAction(() => {
                    Debug.Log($"User '{username}' joined from {clientEndPoint}");
                    AddChatMessage($"Server: {username} joined the room");
                    UpdateUI();
                });
            }
            else if (message.StartsWith("CHAT:"))
            {
                string chatMsg = message.Substring(5);
                
                ClientInfo clientInfo;
                lock (connectedClients)
                {
                    if (connectedClients.TryGetValue(clientEndPoint, out clientInfo))
                    {
                        clientInfo.lastSeen = DateTime.Now;
                    }
                }
                
                if (clientInfo != null)
                {
                    BroadcastMessage($"{clientInfo.username}: {chatMsg}");
                }
            }
            else if (message == "PING")
            {
                // Update last seen and respond with PONG
                lock (connectedClients)
                {
                    if (connectedClients.ContainsKey(clientEndPoint))
                    {
                        connectedClients[clientEndPoint].lastSeen = DateTime.Now;
                    }
                }
                
                byte[] pong = Encoding.ASCII.GetBytes("PONG");
                serverSocket.SendTo(pong, clientEndPoint);
            }
            else if (message == "LEAVE")
            {
                ClientInfo clientInfo = null;
                lock (connectedClients)
                {
                    if (connectedClients.TryGetValue(clientEndPoint, out clientInfo))
                    {
                        connectedClients.Remove(clientEndPoint);
                    }
                }
                
                if (clientInfo != null)
                {
                    QueueMainThreadAction(() => {
                        Debug.Log($"User '{clientInfo.username}' left the room");
                        AddChatMessage($"Server: {clientInfo.username} left the room");
                        UpdateUI();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            QueueMainThreadAction(() => Debug.LogError($"Error handling message: {ex.Message}"));
        }
    }
    
    void BroadcastMessage(string message)
    {
        QueueMainThreadAction(() => AddChatMessage(message));
        
        byte[] messageBytes = Encoding.ASCII.GetBytes($"CHAT:{message}");
        
        lock (connectedClients)
        {
            List<IPEndPoint> disconnectedClients = new List<IPEndPoint>();
            
            foreach (var kvp in connectedClients)
            {
                try
                {
                    serverSocket.SendTo(messageBytes, kvp.Key);
                }
                catch
                {
                    disconnectedClients.Add(kvp.Key);
                }
            }
            
            foreach (var client in disconnectedClients)
            {
                connectedClients.Remove(client);
            }
        }
    }
    
    public void SendChatMessage()
    {
        if (chatInputField != null && !string.IsNullOrEmpty(chatInputField.text))
        {
            string message = chatInputField.text;
            chatInputField.text = "";
            BroadcastMessage($"Server: {message}");
        }
    }
    
    void AddChatMessage(string message)
    {
        chatMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (chatMessages.Count > 50)
        {
            chatMessages.RemoveAt(0);
        }
        UpdateChatDisplay();
    }
    
    void UpdateChatDisplay()
    {
        if (chatDisplayText != null && chatDisplayText.content != null)
        {
            var chatText = chatDisplayText.content.GetComponent<TextMeshProUGUI>();
            if (chatText != null)
            {
                chatText.text = string.Join("\n", chatMessages);
            }
        }
    }
    
    void UpdateUI()
    {
        if (statusText != null)
        {
            statusText.text = isServerRunning ? 
                $"UDP Server Running on Port {port}" : 
                "UDP Server Stopped";
        }
        
        if (connectedClientsText != null)
        {
            int clientCount;
            lock (connectedClients)
            {
                clientCount = connectedClients.Count;
            }
            connectedClientsText.text = $"Connected Clients: {clientCount}";
        }
        
        if (startServerButton != null)
            startServerButton.interactable = !isServerRunning;
        
        if (stopServerButton != null)
            stopServerButton.interactable = isServerRunning;
    }
    
    void QueueMainThreadAction(System.Action action)
    {
        lock (lockObject)
        {
            mainThreadActions.Enqueue(action);
        }
    }
    
    void OnDestroy()
    {
        StopServer();
    }
    
    void OnApplicationQuit()
    {
        StopServer();
    }
}