using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using TMPro;

public class ClientTest : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;

    public string serverIP = "192.168.8.41";
    public int port = 8080;

    [SerializeField] private TextMeshProUGUI text;

    async void Start()
    {
        try
        {
            client = new TcpClient();
            await client.ConnectAsync(serverIP, port);
            stream = client.GetStream();
            text.text = "Connected to Server!";
            Debug.Log("Connected to Server!");
        }
        catch (Exception ex)
        {
            text.text = "Failed to Connect: " + ex.Message;
            Debug.LogError("Failed to Connect: " + ex.Message);
        }
    }

    private void Update()
    {
        if (client == null || !client.Connected) return;
        string message = "This is a message";
        Send(message);
    }

    async void Send(string message)
    {
        if (stream == null) return;
        byte[] data = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(data, 0, data.Length);
    }

    private void OnApplicationQuit()
    {
        stream?.Close();
        client?.Close();
    }
}
