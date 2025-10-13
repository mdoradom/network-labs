using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class TCPClient : MonoBehaviour
{
    [Header("TCP Client Settings")]
    [SerializeField] private string serverIP = "127.0.0.1";
    [SerializeField] private int serverPort = 9050;
    [SerializeField] private string username = "Player";
    
    [Header("UI References")]
    [SerializeField] private TMP_InputField serverIPField;
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI roomInfoText;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private ScrollRect chatDisplayText;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button sendMessageButton;
    
    private Socket clientSocket;
    private bool isConnected = false;
    private Thread receiveThread;
    private List<string> chatMessages = new List<string>();
    
    // Thread-safe queue for UI updates
    private Queue<System.Action> mainThreadActions = new Queue<System.Action>();
    private readonly object lockObject = new object();
    
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
        if (serverIPField != null)
        {
            serverIPField.text = serverIP;
            serverIPField.onValueChanged.AddListener(value => serverIP = value);
        }
        
        if (usernameField != null)
        {
            usernameField.text = username;
            usernameField.onValueChanged.AddListener(value => username = value);
        }
        
        if (connectButton != null)
            connectButton.onClick.AddListener(ConnectToServer);
        
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(DisconnectFromServer);
        
        if (sendMessageButton != null)
            sendMessageButton.onClick.AddListener(SendChatMessage);
        
        UpdateUI();
    }
    
    public void ConnectToServer()
    {
        if (isConnected) return;
        
        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            
            QueueMainThreadAction(() => {
                Debug.Log($"Attempting to connect to server at {serverIP}:{serverPort}...");
                UpdateUI();
            });
            
            clientSocket.Connect(serverEndPoint);
            
            // Send username
            byte[] usernameBytes = Encoding.ASCII.GetBytes(username);
            clientSocket.Send(usernameBytes);
            
            isConnected = true;
            
            // Start receive thread
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();
            
            QueueMainThreadAction(() => {
                Debug.Log("Successfully connected to server!");
                UpdateUI();
            });
        }
        catch (SocketException ex)
        {
            QueueMainThreadAction(() => {
                Debug.LogError($"Failed to connect to server: {ex.Message}");
                CleanupConnection();
                UpdateUI();
            });
        }
        catch (Exception ex)
        {
            QueueMainThreadAction(() => {
                Debug.LogError($"Unexpected error: {ex.Message}");
                CleanupConnection();
                UpdateUI();
            });
        }
    }
    
    public void DisconnectFromServer()
    {
        if (!isConnected) return;
        
        isConnected = false;
        
        try
        {
            clientSocket?.Close();
            receiveThread?.Join(2000);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error disconnecting: {ex.Message}");
        }
        
        QueueMainThreadAction(() => {
            Debug.Log("Disconnected from server");
            CleanupConnection();
            UpdateUI();
        });
    }
    
    void ReceiveLoop()
    {
        byte[] buffer = new byte[1024];
        
        while (isConnected)
        {
            try
            {
                int received = clientSocket.Receive(buffer);
                if (received == 0) break; // Server disconnected
                
                string message = Encoding.ASCII.GetString(buffer, 0, received);
                
                if (message.StartsWith("WELCOME:"))
                {
                    string roomName = message.Substring(8);
                    QueueMainThreadAction(() => {
                        Debug.Log($"Joined room: {roomName}");
                        if (roomInfoText != null)
                        {
                            roomInfoText.text = $"Room: {roomName}";
                        }
                    });
                }
                else if (message.StartsWith("CHAT:"))
                {
                    string chatMsg = message.Substring(5);
                    QueueMainThreadAction(() => AddChatMessage(chatMsg));
                }
                else if (message == "PONG")
                {
                    QueueMainThreadAction(() => Debug.Log("Received pong from server"));
                }
            }
            catch (SocketException)
            {
                if (isConnected)
                {
                    QueueMainThreadAction(() => {
                        Debug.Log("Lost connection to server");
                        DisconnectFromServer();
                    });
                }
                break;
            }
        }
    }
    
    public void SendChatMessage()
    {
        if (!isConnected || chatInputField == null || string.IsNullOrEmpty(chatInputField.text))
            return;
        
        try
        {
            string message = $"CHAT:{chatInputField.text}";
            byte[] messageBytes = Encoding.ASCII.GetBytes(message);
            clientSocket.Send(messageBytes);
            
            chatInputField.text = "";
        }
        catch (Exception ex)
        {
            QueueMainThreadAction(() => {
                Debug.LogError($"Error sending message: {ex.Message}");
                DisconnectFromServer();
            });
        }
    }
    
    public void SendPing()
    {
        if (!isConnected) return;
        
        try
        {
            byte[] pingBytes = Encoding.ASCII.GetBytes("PING");
            clientSocket.Send(pingBytes);
        }
        catch (Exception ex)
        {
            QueueMainThreadAction(() => {
                Debug.LogError($"Error sending ping: {ex.Message}");
                DisconnectFromServer();
            });
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
            statusText.text = isConnected ? 
                $"Connected to {serverIP}:{serverPort}" : 
                "Not Connected";
        }
        
        if (connectButton != null)
            connectButton.interactable = !isConnected;
        
        if (disconnectButton != null)
            disconnectButton.interactable = isConnected;
        
        if (sendMessageButton != null)
            sendMessageButton.interactable = isConnected;
        
        if (serverIPField != null)
            serverIPField.interactable = !isConnected;
        
        if (usernameField != null)
            usernameField.interactable = !isConnected;
    }
    
    void CleanupConnection()
    {
        isConnected = false;
        clientSocket = null;
        chatMessages.Clear();
        UpdateChatDisplay();
        
        if (roomInfoText != null)
        {
            roomInfoText.text = "";
        }
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
        DisconnectFromServer();
    }
    
    void OnApplicationQuit()
    {
        DisconnectFromServer();
    }
}