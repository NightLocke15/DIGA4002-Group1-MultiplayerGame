using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class UdpHost : MonoBehaviour
{
    public int listenPort = 7777;
    public KartController player1;
    public KartController player2;
    public Camera camP1;
    public Camera camP2;

    UdpClient udp;
    IPEndPoint anyEP = new IPEndPoint(IPAddress.Any, 0);

    readonly Dictionary<string, int> addrToId = new Dictionary<string, int>();
    readonly string[] idToAddr = new string[3]; // index 1 and 2 used

    void Start()
    {
        if (!player1 || !player2 || !camP1 || !camP2)
        {
            Debug.LogError("Assign karts and cameras");
            return;
        }

        try
        {
            udp = new UdpClient(listenPort);
            _ = ReceiveLoop();
            Debug.Log($"UDP host listening on {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"UDP open failed {e.Message}");
        }

        camP1.rect = new Rect(0f, 0f, 0.5f, 1f);
        camP2.rect = new Rect(0.5f, 0f, 0.5f, 1f);
    }

    async Task ReceiveLoop()
    {
        while (true)
        {
            UdpReceiveResult r;
            try { r = await udp.ReceiveAsync(); }
            catch { break; }

            string key = r.RemoteEndPoint.ToString();
            if (!addrToId.TryGetValue(key, out int id))
            {
                id = AssignIdFor(key);
                if (id != 0)
                {
                    byte[] ack = Encoding.UTF8.GetBytes($"ASSIGN,{id}");
                    try { await udp.SendAsync(ack, ack.Length, r.RemoteEndPoint); } catch { }
                    Debug.Log($"Assigned player {id} to {key}");
                }
            }

            string msg = Encoding.UTF8.GetString(r.Buffer);
            // Expected "id,steer,throttle,brake,item"
            var parts = msg.Split(',');
            if (parts.Length < 5) continue;

            if (!int.TryParse(parts[0], out int pid)) continue;
            if (!float.TryParse(parts[1], out float steer)) continue;
            if (!float.TryParse(parts[2], out float throttle)) continue;
            if (!float.TryParse(parts[3], out float brake)) continue;
            // item is parts[4] if you need it

            ApplyInput(pid, steer, throttle, brake);
        }
    }

    int AssignIdFor(string key)
    {
        if (idToAddr[1] == null)
        {
            idToAddr[1] = key;
            addrToId[key] = 1;
            return 1;
        }
        if (idToAddr[2] == null)
        {
            idToAddr[2] = key;
            addrToId[key] = 2;
            return 2;
        }
        return 0;
    }

    void ApplyInput(int playerId, float steer, float throttle, float brake)
    {
        if (playerId == 1) player1.SetInput(steer, throttle, brake);
        else if (playerId == 2) player2.SetInput(steer, throttle, brake);
    }

    void OnApplicationQuit()
    {
        udp?.Close();
    }
}
