using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;  
using TMPro;

public class NetworkHost : MonoBehaviour
{
    [Header("Network")]
    public int listenPort = 7777;

    [Header("Players")]
    public KartController player1;
    public KartController player2;
    public PedalMover mover1;
    public PedalMover mover2;
    public Camera camP1;
    public Camera camP2;

    [Header("Panels")]
    public GameObject startPanel;   
    public GameObject waitingPanel;
    public GameObject gamePanel;    

    [Header("Start UI")]
    public TMP_InputField ipInput;   
    public Button enterButton;      

    [Header("Lobby")]
    [SerializeField] private int expectedPlayers = 2; 

    public TMP_Text poseText;
    public TMP_Text accelText;
    public TMP_Text gyroText;
    public TMP_Text pedalText;

    [SerializeField] private float smooth = 0.10f;
    [SerializeField] private float axRightEnter = 0.40f;
    [SerializeField] private float axRightExit = 0.30f;
    [SerializeField] private float axLeftEnter = -0.40f;
    [SerializeField] private float axLeftExit = -0.30f;
    [SerializeField] private float axNeutralAbs = 0.20f;

    UdpClient udp;
    bool hosting = false;

    readonly Dictionary<string, int> addrToId = new Dictionary<string, int>();
    readonly string[] idToAddr = new string[3];       
    readonly HashSet<int> connectedIds = new HashSet<int>();

    bool gotFirstPacket = false;
    float lastPacketTime = -999f;
    const float packetTimeout = 1.0f;

    string lastAccelStr = "ax 0.00 ay 0.00 az 0.00 gz 0.00";
    bool gyroOk = false;
    string lastPose = "N";
    float axSmooth = 0f, azSmooth = 0f;
    char stepSideP1 = 'N'; float lastStepTimeP1 = 0f;
    char stepSideP2 = 'N'; float lastStepTimeP2 = 0f;
    float pedalMsgTimer = 0f;
    const float pedalMsgHold = 0.6f;

    void Start()
    {
        if (camP1) camP1.rect = new Rect(0f, 0f, 0.5f, 1f);
        if (camP2) camP2.rect = new Rect(0.5f, 0f, 0.5f, 1f);

        if (startPanel) startPanel.SetActive(true);
        if (waitingPanel) waitingPanel.SetActive(false);
        if (gamePanel) gamePanel.SetActive(false);

        if (ipInput && string.IsNullOrEmpty(ipInput.text)) ipInput.text = GetLocalIPv4();

        if (poseText) poseText.text = "NEUTRAL";
        if (gyroText) gyroText.text = "GYRO WAITING";
        if (accelText) accelText.text = lastAccelStr;
        if (pedalText) pedalText.text = "";

        if (enterButton) enterButton.onClick.AddListener(OnPressEnterHost);
    }

    public void OnPressEnterHost()
    {
        if (hosting) return;

        try
        {
            string ip = ipInput ? ipInput.text.Trim() : "";
            if (!IPAddress.TryParse(ip, out IPAddress ipAddr))
            {
                ip = GetLocalIPv4();
                IPAddress.TryParse(ip, out ipAddr);
            }

            udp = new UdpClient(new IPEndPoint(ipAddr, listenPort));
            _ = ReceiveLoop();
            hosting = true;

            if (startPanel) startPanel.SetActive(false);
            if (waitingPanel) waitingPanel.SetActive(true);
            if (gamePanel) gamePanel.SetActive(false);

            Debug.Log($"UDP host listening on {ip}:{listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"UDP open failed {e.Message}");
        }
    }

    void Update()
    {
        if (hosting && gotFirstPacket && Time.realtimeSinceStartup - lastPacketTime > packetTimeout)
        {
            gotFirstPacket = false;
            gyroOk = false;
            lastPose = "N";
            connectedIds.Clear();
            if (poseText) poseText.text = "NEUTRAL";
            if (gyroText) gyroText.text = "GYRO WAITING";
        }

        if (gyroText) gyroText.text = gyroOk ? "GYRO WORKS" : "GYRO WAITING";
        if (accelText) accelText.text = lastAccelStr;

        if (poseText)
        {
            if (lastPose == "L") poseText.text = "LEFT";
            else if (lastPose == "R") poseText.text = "RIGHT";
            else poseText.text = "NEUTRAL";
        }

        if (pedalText && pedalMsgTimer > 0f)
        {
            pedalMsgTimer -= Time.deltaTime;
            if (pedalMsgTimer <= 0f) pedalText.text = "";
        }
    }

    async Task ReceiveLoop()
    {
        while (true)
        {
            UdpReceiveResult r;
            try { r = await udp.ReceiveAsync(); }
            catch { break; }

            string key = r.RemoteEndPoint.ToString();
            string msg = Encoding.UTF8.GetString(r.Buffer);

            if (msg.StartsWith("HELLO"))
            {
                EnsureAssignmentFor(key, r.RemoteEndPoint);
                CheckAdvanceToGame();
                continue;
            }

            var parts = msg.Split(',');
            if (parts.Length < 7) continue;

            int pid = GetOrAssignIdFor(key, r.RemoteEndPoint);

            if (pid != 0 && !connectedIds.Contains(pid))
            {
                connectedIds.Add(pid);
                CheckAdvanceToGame();
            }

            if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float yawBtn))
                ApplyYaw(pid, yawBtn);

            if (parts.Length >= 6 &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay) &&
                float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float az) &&
                float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float gz))
            {
                lastAccelStr = $"ax {ax:F2} ay {ay:F2} az {az:F2} gz {gz:F2}";

                axSmooth = Mathf.Lerp(axSmooth, ax, smooth);
                azSmooth = Mathf.Lerp(azSmooth, az, smooth);

                string newPose = DetectPose(axSmooth, azSmooth, lastPose);
                if (newPose != lastPose)
                {
                    if (newPose == "L" || newPose == "R")
                    {
                        OnPedalStep(pid, newPose[0]);
                    }
                    lastPose = newPose;
                }
            }

            gyroOk = parts[6] == "1";

            gotFirstPacket = true;
            lastPacketTime = Time.realtimeSinceStartup;
        }
    }

    void CheckAdvanceToGame()
    {
        if (!waitingPanel || !gamePanel) return;

        if (connectedIds.Count >= expectedPlayers)
        {
            waitingPanel.SetActive(false);
            gamePanel.SetActive(true);
            Debug.Log("Game ready");
        }
        else
        {
            waitingPanel.SetActive(true);
            gamePanel.SetActive(false);
        }
    }

    int GetOrAssignIdFor(string key, IPEndPoint ep)
    {
        if (addrToId.TryGetValue(key, out int id))
            return id;
        return EnsureAssignmentFor(key, ep);
    }

    int EnsureAssignmentFor(string key, IPEndPoint ep)
    {
        int id = 0;
        if (idToAddr[1] == null)
        {
            idToAddr[1] = key;
            addrToId[key] = 1;
            id = 1;
        }
        else if (idToAddr[2] == null)
        {
            idToAddr[2] = key;
            addrToId[key] = 2;
            id = 2;
        }

        if (id != 0)
        {
            byte[] ack = Encoding.UTF8.GetBytes($"ASSIGN,{id}");
            try { udp.Send(ack, ack.Length, ep); } catch { }
            Debug.Log($"Assigned player {id} to {key}");
        }
        else
        {
            Debug.Log("All player slots are full");
        }

        return id;
    }

    string DetectPose(float ax, float az, string current)
    {
        bool enterRight = ax >= axRightEnter;
        bool enterLeft = ax <= axLeftEnter;

        bool exitRight = ax <= axRightExit;
        bool exitLeft = ax >= axLeftExit;

        bool isNeutral = Mathf.Abs(ax) <= axNeutralAbs;

        if (current == "R")
        {
            if (exitRight) current = "N";
        }
        else if (current == "L")
        {
            if (exitLeft) current = "N";
        }
        else
        {
            if (enterRight) current = "R";
            else if (enterLeft) current = "L";
            else if (isNeutral) current = "N";
        }

        return current;
    }

    void OnPedalStep(int pid, char side)
    {
        float now = Time.realtimeSinceStartup;

        if (pid == 1 && mover1 != null)
        {
            if ((stepSideP1 == 'L' && side == 'R') || (stepSideP1 == 'R' && side == 'L'))
            {
                float dt = Mathf.Max(0.0001f, now - lastStepTimeP1);
                float dv = mover1.ApplyPedalPush(dt);
                FlashPedal($"Push {dv:F2}");
            }
            stepSideP1 = side;
            lastStepTimeP1 = now;
        }
        else if (pid == 2 && mover2 != null)
        {
            if ((stepSideP2 == 'L' && side == 'R') || (stepSideP2 == 'R' && side == 'L'))
            {
                float dt = Mathf.Max(0.0001f, now - lastStepTimeP2);
                float dv = mover2.ApplyPedalPush(dt);
                FlashPedal($"Push {dv:F2}");
            }
            stepSideP2 = side;
            lastStepTimeP2 = now;
        }
    }

    void FlashPedal(string s)
    {
        if (!pedalText) return;
        pedalText.text = s;
        pedalMsgTimer = pedalMsgHold;
    }

    void ApplyYaw(int playerId, float yaw)
    {
        if (playerId == 1 && player1) player1.SetYawInput(yaw);
        else if (playerId == 2 && player2) player2.SetYawInput(yaw);
    }

    string GetLocalIPv4()
    {
        string best = "127.0.0.1";
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                var props = ni.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string addr = ua.Address.ToString();
                        if (!addr.StartsWith("127.")) return addr;
                    }
                }
            }
        }
        catch { }
        return best;
    }

    void OnApplicationQuit()
    {
        udp?.Close();
    }
}
