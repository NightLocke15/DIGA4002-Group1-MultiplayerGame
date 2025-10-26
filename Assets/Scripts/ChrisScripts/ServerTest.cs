using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TMPro;

public class ServerTest : MonoBehaviour
{
    private TcpListener server;
    private bool running;

    [SerializeField] private TextMeshProUGUI text;

    private void Start()
    {
        StartServer();
    }

    private void StartServer()
    {
        try
        {
            int port = 8080;
            server = new TcpListener(IPAddress.Any, port);
            server.Start();
            running = true;
            text.text = $"Server started on port {port}";
            Debug.Log($"Server started on port {port}");
            server.BeginAcceptTcpClient(OnClientConnected, null);
        }
        catch (Exception e)
        {
            text.text = "Server start failed: " + e.Message;
            Debug.LogError("Server start failed: " + e.Message);
        }
    }

    private void OnClientConnected(IAsyncResult ar)
    {
        TcpClient client = server.EndAcceptTcpClient(ar);
        text.text = "Client Connected!";
        Debug.Log("Client Connected!");
        server.BeginAcceptTcpClient(OnClientConnected, null);

        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        stream.BeginRead(buffer, 0, buffer.Length, ar2 =>
        {
            int bytesRead = stream.EndRead(ar2);
            if (bytesRead > 0)
            {
                string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                text.text = $"Received: {msg}";
                Debug.Log($"Received: {msg}");
            }
        }, null);
    }

    private void OnApplicationQuit()
    {
        running = false;
        server?.Stop();
    }
}
