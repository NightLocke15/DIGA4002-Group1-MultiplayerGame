using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using TMPro;
using System.Collections;

public class ClientController : MonoBehaviour
{
    public string serverIP = "192.168.0.42";
    public int serverPort = 7777;
    public TMP_InputField ipField;
    public TMP_Text statusText;

    UdpClient udp;
    IPEndPoint serverEP;
    int playerId = 1;    // force player one
    bool running;

    void Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeRight;
        if (ipField) ipField.text = serverIP;
    }

    public void Connect()
    {
        try
        {
            if (ipField && !string.IsNullOrWhiteSpace(ipField.text))
                serverIP = ipField.text.Trim();

            udp = new UdpClient();
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            // optional hello
            byte[] hello = Encoding.UTF8.GetBytes("HELLO");
            udp.Send(hello, hello.Length, serverEP);

            running = true;
            StartCoroutine(SendLoop());

            if (statusText) statusText.text = "Connected as P1";
        }
        catch (Exception e)
        {
            if (statusText) statusText.text = "Connect failed: " + e.Message;
        }
    }

    IEnumerator SendLoop()
    {
        var wait = new WaitForSeconds(1f / 30f);

        float sens = 1.8f;
        float dead = 0.06f;
        float smooth = 0.15f;
        Vector3 zero = Input.acceleration;
        float steerSmooth = 0f;

        while (running)
        {
            Vector3 a = Input.acceleration - zero;
            float steer = Mathf.Clamp(a.x * sens, -1f, 1f);
            if (Mathf.Abs(steer) < dead) steer = 0f;
            steerSmooth = Mathf.Lerp(steerSmooth, steer, 1f - smooth);

            float throttle = 0f;
            float brake = 0f;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.position.x > Screen.width * 0.5f) throttle = 1f;
                    else brake = 1f;
                }
            }

            string msg = $"{playerId},{steerSmooth:F3},{throttle:F3},{brake:F3},0";
            byte[] bytes = Encoding.UTF8.GetBytes(msg);
            try { udp.Send(bytes, bytes.Length, serverEP); } catch { }

            yield return wait;
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        udp?.Close();
    }
}
