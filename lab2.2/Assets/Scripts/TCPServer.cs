using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TCPServer : MonoBehaviour
{
    [Header("TCP Server Settings")]
    [SerializeField] private int port = 9050;
    [SerializeField] private int maxConnections = 10;
    [SerializeField] private string roomName = "Unity Game Room";
    
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
    private Dictionary<Socket, ClientInfo> connectedClients = new Dictionary<Socket, ClientInfo>();
    private List<string> chatMessages = new List<string>();
    
    // Thread-safe queue for UI updates
    private Queue<System.Action> mainThreadActions = new Queue<System.Action>();
    private readonly object lockObject = new object();
    
    private class ClientInfo
    {
        public string username;
        public IPEndPoint endPoint;
        public Thread clientThread;
        
        public ClientInfo(string name, IPEndPoint ep)
        {
            username = name;
            endPoint = ep;
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
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            
            serverSocket.Bind(localEndPoint);
            serverSocket.Listen(maxConnections);
            
            isServerRunning = true;
            
            serverThread = new Thread(ServerLoop);
            serverThread.Start();
            
            QueueMainThreadAction(() => {
                Debug.Log($"TCP Server started on port {port}");
                UpdateUI();
            });
        }
        catch (SocketException ex)
        {
            Debug.LogError($"Failed to start TCP server: {ex.Message}");
            QueueMainThreadAction(() => UpdateUI());
        }
    }
    
    public void StopServer()
    {
        if (!isServerRunning) return;
        
        isServerRunning = false;
        
        try
        {
            // Close all client connections
            lock (connectedClients)
            {
                foreach (var kvp in connectedClients)
                {
                    try
                    {
                        kvp.Key.Close();
                        kvp.Value.clientThread?.Join(1000);
                    }
                    catch { }
                }
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
            Debug.Log("TCP Server stopped");
            UpdateUI();
        });
    }
    
    void ServerLoop()
    {
        while (isServerRunning)
        {
            try
            {
                Socket clientSocket = serverSocket.Accept();
                
                QueueMainThreadAction(() => {
                    Debug.Log($"New client connected from {clientSocket.RemoteEndPoint}");
                });
                
                Thread clientThread = new Thread(() => HandleClient(clientSocket));
                clientThread.Start();
            }
            catch (SocketException)
            {
                if (isServerRunning)
                    QueueMainThreadAction(() => Debug.LogError("Server socket error"));
                break;
            }
        }
    }
    
    void HandleClient(Socket clientSocket)
    {
        byte[] buffer = new byte[1024];
        ClientInfo clientInfo = null;
        
        try
        {
            // Receive initial message with username
            int received = clientSocket.Receive(buffer);
            if (received > 0)
            {
                string username = Encoding.ASCII.GetString(buffer, 0, received);
                clientInfo = new ClientInfo(username, (IPEndPoint)clientSocket.RemoteEndPoint);
                
                lock (connectedClients)
                {
                    connectedClients[clientSocket] = clientInfo;
                }
                
                // Send welcome message with room name
                string welcomeMessage = $"WELCOME:{roomName}";
                byte[] response = Encoding.ASCII.GetBytes(welcomeMessage);
                clientSocket.Send(response);
                
                QueueMainThreadAction(() => {
                    Debug.Log($"User '{username}' joined the room");
                    AddChatMessage($"Server: {username} joined the room");
                    UpdateUI();
                });
                
                // Handle client messages
                while (isServerRunning && clientSocket.Connected)
                {
                    try
                    {
                        received = clientSocket.Receive(buffer);
                        if (received == 0) break;
                        
                        string message = Encoding.ASCII.GetString(buffer, 0, received);
                        
                        if (message.StartsWith("CHAT:"))
                        {
                            string chatMsg = message.Substring(5);
                            BroadcastChatMessage($"{username}: {chatMsg}");
                        }
                        else if (message == "PING")
                        {
                            // Respond to ping
                            byte[] pong = Encoding.ASCII.GetBytes("PONG");
                            clientSocket.Send(pong);
                        }
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            QueueMainThreadAction(() => Debug.LogError($"Client handler error: {ex.Message}"));
        }
        finally
        {
            // Clean up client
            if (clientInfo != null)
            {
                lock (connectedClients)
                {
                    connectedClients.Remove(clientSocket);
                }
                
                QueueMainThreadAction(() => {
                    Debug.Log($"User '{clientInfo.username}' left the room");
                    AddChatMessage($"Server: {clientInfo.username} left the room");
                    UpdateUI();
                });
            }
            
            try
            {
                clientSocket.Close();
            }
            catch { }
        }
    }
    
    void BroadcastChatMessage(string message)
    {
        QueueMainThreadAction(() => AddChatMessage(message));
        
        byte[] messageBytes = Encoding.ASCII.GetBytes($"CHAT:{message}");
        
        lock (connectedClients)
        {
            List<Socket> disconnectedClients = new List<Socket>();
            
            foreach (var kvp in connectedClients)
            {
                try
                {
                    kvp.Key.Send(messageBytes);
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
            BroadcastChatMessage($"Server: {message}");
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
                $"Server Running on Port {port}" : 
                "Server Stopped";
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