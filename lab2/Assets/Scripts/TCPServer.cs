using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class TCPServer : MonoBehaviour
{
    [Header("TCP Server Settings")]
    [SerializeField] private int port = 9050;
    [SerializeField] private int backlogSize = 10;
    
    private Socket serverSocket;
    private bool isServerRunning = false;
    
    void Start()
    {
        InitializeTCPServer();
    }
    
    void InitializeTCPServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            
            serverSocket.Bind(ipEndPoint);
            
            serverSocket.Listen(backlogSize);
            
            isServerRunning = true;
            
            Debug.Log($"TCP Server started successfully on port {port}");
            Debug.Log($"Server is listening for connections (max {backlogSize} pending connections)");
            Debug.Log($"Any IP address can connect to this server");
        }
        catch (SocketException ex)
        {
            Debug.LogError($"Failed to start TCP server: {ex.Message}");
            Debug.LogError($"Error Code: {ex.SocketErrorCode}");
            
            if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Debug.LogError($"Port {port} is already in use. Try a different port or close the application using it.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected error starting TCP server: {ex.Message}");
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
    
    void StopServer()
    {
        if (serverSocket != null && isServerRunning)
        {
            try
            {
                serverSocket.Shutdown(SocketShutdown.Both);
                serverSocket.Close();
                isServerRunning = false;
                Debug.Log("TCP Server stopped successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error stopping TCP server: {ex.Message}");
            }
        }
    }
    
    public bool IsServerRunning()
    {
        return isServerRunning && serverSocket != null && serverSocket.IsBound;
    }
    
    public string GetServerInfo()
    {
        if (IsServerRunning())
        {
            return $"TCP Server running on {serverSocket.LocalEndPoint}";
        }
        return "TCP Server not running";
    }
}