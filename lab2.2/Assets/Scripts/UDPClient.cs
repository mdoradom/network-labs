using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UDPClient : MonoBehaviour
{
    [Header("UDP Client Settings")]
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
    private IPEndPoint serverEndPoint;
    
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
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            
            QueueMainThreadAction(() => {
                Debug.Log($"Attempting to connect to UDP server at {serverIP}:{serverPort}...");
                UpdateUI();
            });
            
            // Send JOIN message with username
            string joinMessage = $"JOIN:{username}";
            byte[] joinBytes = Encoding.ASCII.GetBytes(joinMessage);
            clientSocket.SendTo(joinBytes, serverEndPoint);
            
            isConnected = true;
            
            // Start receive thread
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();
            
            QueueMainThreadAction(() => {
                Debug.Log("Successfully connected to UDP server!");
                UpdateUI();
            });
        }
        catch (SocketException ex)
        {
            QueueMainThreadAction(() => {
                Debug.LogError($"Failed to connect to UDP server: {ex.Message}");
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
        
        try
        {
            // Send LEAVE message
            byte[] leaveBytes = Encoding.ASCII.GetBytes("LEAVE");
            clientSocket?.SendTo(leaveBytes, serverEndPoint);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending leave message: {ex.Message}");
        }
        
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
            Debug.Log("Disconnected from UDP server");
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
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint senderEndPoint = sender;
                
                int received = clientSocket.ReceiveFrom(buffer, ref senderEndPoint);
                
                if (received > 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, received);
                    
                    if (message.StartsWith("WELCOME:"))
                    {
                        string roomName = message.Substring(8);
                        QueueMainThreadAction(() => {
                            Debug.Log($"Joined UDP room: {roomName}");
                            if (roomInfoText != null)
                            {
                                roomInfoText.text = $"UDP Room: {roomName}";
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
                        QueueMainThreadAction(() => Debug.Log("Received pong from UDP server"));
                    }
                }
            }
            catch (SocketException)
            {
                if (isConnected)
                {
                    QueueMainThreadAction(() => {
                        Debug.Log("Lost connection to UDP server");
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
            clientSocket.SendTo(messageBytes, serverEndPoint);
            
            chatInputField.text = "";
        }
        catch (Exception ex)
        {
            QueueMainThreadAction(() => {
                Debug.LogError($"Error sending UDP message: {ex.Message}");
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
            clientSocket.SendTo(pingBytes, serverEndPoint);
        }
        catch (Exception ex)
        {
            QueueMainThreadAction(() => {
                Debug.LogError($"Error sending UDP ping: {ex.Message}");
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
                $"Connected to UDP {serverIP}:{serverPort}" : 
                "Not Connected (UDP)";
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