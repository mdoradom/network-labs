using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class TCPClient : MonoBehaviour
{
    [Header("TCP Client Settings")]
    [SerializeField] private string serverIP = "127.0.0.1";
    [SerializeField] private int serverPort = 9050;
    [SerializeField] private bool connectOnStart = false;
    
    private Socket clientSocket;
    private bool isConnected = false;
    private IPEndPoint serverEndPoint;
    
    void Start()
    {
        if (connectOnStart)
        {
            ConnectToServer();
        }
    }
    
    [ContextMenu("Connect to Server")]
    public void ConnectToServer()
    {
        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            IPAddress serverAddress = IPAddress.Parse(serverIP);
            serverEndPoint = new IPEndPoint(serverAddress, serverPort);
            
            Debug.Log($"Attempting to connect to server at {serverIP}:{serverPort}...");
            
            clientSocket.Connect(serverEndPoint);
            
            isConnected = true;
            
            Debug.Log($"Successfully connected to server!");
            Debug.Log($"Local endpoint: {clientSocket.LocalEndPoint}");
            Debug.Log($"Remote endpoint: {clientSocket.RemoteEndPoint}");
            Debug.Log($"Connection established between client and server");
        }
        catch (SocketException ex)
        {
            Debug.LogError($"Failed to connect to server: {ex.Message}");
            Debug.LogError($"Socket Error Code: {ex.SocketErrorCode}");
            
            switch (ex.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    Debug.LogError("Connection refused. Make sure the server is running and listening on the specified port.");
                    break;
                case SocketError.TimedOut:
                    Debug.LogError("Connection timed out. Server may be unreachable.");
                    break;
                case SocketError.HostUnreachable:
                    Debug.LogError("Host unreachable. Check the server IP address.");
                    break;
                default:
                    Debug.LogError($"Other socket error: {ex.SocketErrorCode}");
                    break;
            }
            
            CleanupConnection();
        }
        catch (FormatException ex)
        {
            Debug.LogError($"Invalid IP address format: {serverIP}. {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected error connecting to server: {ex.Message}");
            CleanupConnection();
        }
    }
    
    [ContextMenu("Disconnect from Server")]
    public void DisconnectFromServer()
    {
        if (isConnected && clientSocket != null)
        {
            try
            {
                Debug.Log("Disconnecting from server...");
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
                isConnected = false;
                Debug.Log("Disconnected from server successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error disconnecting from server: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("Client is not connected to any server");
        }
    }
    
    private void CleanupConnection()
    {
        if (clientSocket != null)
        {
            try
            {
                clientSocket.Close();
            }
            catch { }
            finally
            {
                clientSocket = null;
                isConnected = false;
            }
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
    
    public bool IsConnected()
    {
        return isConnected && clientSocket != null && clientSocket.Connected;
    }
    
    public string GetConnectionInfo()
    {
        if (IsConnected())
        {
            return $"Connected to {serverEndPoint} from {clientSocket.LocalEndPoint}";
        }
        return "Not connected to any server";
    }
    
    public void SetServerAddress(string ip, int port)
    {
        if (!IsConnected())
        {
            serverIP = ip;
            serverPort = port;
            Debug.Log($"Server address updated to {ip}:{port}");
        }
        else
        {
            Debug.LogWarning("Cannot change server address while connected. Disconnect first.");
        }
    }
}