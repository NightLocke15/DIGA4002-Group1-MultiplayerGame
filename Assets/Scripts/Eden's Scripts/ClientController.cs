using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Globalization;

public class ClientController : MonoBehaviour
{
    public string serverIP;
    public int serverPort = 7777;

    public TMP_InputField ipField;
    public TMP_Text statusText;

    public GameObject connectPanel;
    public GameObject controlPanel;
    public DirectionButtons leftButton;
    public DirectionButtons rightButton;

    UdpClient udp;
    IPEndPoint serverEP;
    int playerId = 0;  
    bool running;

    void Start()
    {
        Screen.orientation = ScreenOrientation.LandscapeRight;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

        Input.gyro.enabled = true;

        if (ipField) ipField.text = serverIP;
        if (controlPanel) controlPanel.SetActive(false);
    }

    public void Connect()
    {
        try
        {
            if (ipField && !string.IsNullOrWhiteSpace(ipField.text))
                serverIP = ipField.text.Trim();

            udp = new UdpClient();
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            byte[] hello = Encoding.UTF8.GetBytes("HELLO");
            udp.Send(hello, hello.Length, serverEP);

            running = true;
            StartCoroutine(AssignListener());
            StartCoroutine(SendLoop());

            if (statusText) statusText.text = "Connecting";
            if (connectPanel) connectPanel.SetActive(false);
            if (controlPanel) controlPanel.SetActive(true);
        }
        catch (Exception e)
        {
            if (statusText) statusText.text = "Connect failed: " + e.Message;
        }
    }

    IEnumerator AssignListener()
    {
        float timeout = Time.time + 5f;

        while (running && Time.time < timeout && playerId == 0)
        {
            if (udp.Available > 0)
            {
                IPEndPoint ep = null;
                byte[] data = udp.Receive(ref ep);
                string msg = Encoding.UTF8.GetString(data);
                var parts = msg.Split(',');
                if (parts.Length == 2 && parts[0] == "ASSIGN")
                {
                    if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid))
                    {
                        playerId = pid;
                        if (statusText) statusText.text = $"Assigned P{playerId}";
                        break;
                    }
                }
            }
            yield return null;
        }

        if (playerId == 0)
        {
            playerId = 1;
            if (statusText) statusText.text = "No assign, default P1";
        }
    }

    IEnumerator SendLoop()
    {
        var wait = new WaitForSeconds(1f / 30f);

        Vector3 lastA = Vector3.zero;
        float lastGz = 0f;
        bool gyroOk = false;

        while (running)
        {
            float yawButton = 0f;
            if (rightButton && rightButton.IsPressed) yawButton = 1f;
            else if (leftButton && leftButton.IsPressed) yawButton = -1f;

            Vector3 a = Input.acceleration;
            Vector3 r = Input.gyro.rotationRateUnbiased;
            float gz = r.z;

            if (!gyroOk)
            {
                float deltaA = Mathf.Abs(a.x - lastA.x) + Mathf.Abs(a.y - lastA.y) + Mathf.Abs(a.z - lastA.z);
                if (deltaA > 0.02f || Mathf.Abs(gz - lastGz) > 0.02f) gyroOk = true;
            }
            lastA = a;
            lastGz = gz;

            string msg = string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1:F3},{2:F3},{3:F3},{4:F3},{5:F3},{6}",
                playerId, yawButton, a.x, a.y, a.z, gz, gyroOk ? 1 : 0
            );

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
