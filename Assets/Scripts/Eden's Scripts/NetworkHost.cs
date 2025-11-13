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
    public RaceCountdown countdown;
    bool countdownRunning = false;

    public int listenPort = 7777;

    public KartController player1;
    public KartController player2;
    public PedalMover mover1;
    public PedalMover mover2;
    public Camera camP1;
    public Camera camP2;

    public GameObject startPanel;
    public GameObject waitingPanel;
    public GameObject instructionsPanel;
    public GameObject gamePanel;

    public TMP_InputField ipInput;
    public Button enterButton;

    [SerializeField] private int expectedPlayers = 2;

    public TMP_Text poseText;
    public TMP_Text accelText;
    public TMP_Text gyroText;
    public TMP_Text pedalText;
    public TMP_Text player1Text;
    public TMP_Text player2Text;

    public Image p1LeftText;
    public Image p1RightText;
    public Image p2LeftText;
    public Image p2RightText;

    [SerializeField] private float axRightEnter = 0.30f;
    [SerializeField] private float axLeftEnter = -0.30f;

    public float defaultMaxSpeed = 4f;
    public float boostMaxSpeed = 6f;
    public float powerDuration = 5f;
    public float setbackDuration = 5f;

    public bool overrideNewestEffect = false;

    public bool controlsLocked = false;

    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioClip bgMusic;
    public AudioClip winClip;

    UdpClient udp;
    bool hosting = false;

    readonly Dictionary<string, int> addrToId = new Dictionary<string, int>();
    readonly string[] idToAddr = new string[3];
    readonly IPEndPoint[] idToEndPoint = new IPEndPoint[3];
    readonly HashSet<int> connectedIds = new HashSet<int>();

    bool gotFirstPacket = false;
    float lastPacketTime = -999f;
    const float packetTimeout = 5.0f;

    string lastAccelStr = "ax 0.00 ay 0.00 az 0.00 gz 0.00";
    bool gyroOk = false;
    string lastPoseUI = "N";
    float pedalMsgTimer = 0f;
    const float pedalMsgHold = 0.6f;

    float boostEnd1 = 0f, boostEnd2 = 0f;
    float swapEnd1 = 0f, swapEnd2 = 0f;

    enum TutState { NeedLeft, NeedRight, Ready }
    TutState tutP1 = TutState.NeedLeft;
    TutState tutP2 = TutState.NeedLeft;

    char poseP1 = 'N';
    char poseP2 = 'N';
    char lastStepP1 = 'N';
    char lastStepP2 = 'N';
    float lastStepTimeP1 = 0f;
    float lastStepTimeP2 = 0f;

    void Start()
    {
        if (camP1) camP1.rect = new Rect(0f, 0f, 0.5f, 1f);
        if (camP2) camP2.rect = new Rect(0.5f, 0f, 0.5f, 1f);

        if (startPanel) startPanel.SetActive(true);
        if (waitingPanel) waitingPanel.SetActive(false);
        if (instructionsPanel) instructionsPanel.SetActive(false);
        if (gamePanel) gamePanel.SetActive(false);

        if (ipInput && string.IsNullOrEmpty(ipInput.text)) ipInput.text = GetLocalIPv4();

        if (poseText) poseText.text = "NEUTRAL";
        if (gyroText) gyroText.text = "GYRO WAITING";
        if (accelText) accelText.text = lastAccelStr;
        if (pedalText) pedalText.text = "";
        if (player1Text) player1Text.text = "";
        if (player2Text) player2Text.text = "";

        if (enterButton) enterButton.onClick.AddListener(OnPressEnterHost);

        if (mover1) mover1.maxForwardSpeed = defaultMaxSpeed;
        if (mover2) mover2.maxForwardSpeed = defaultMaxSpeed;

        if (musicSource && bgMusic)
        {
            musicSource.clip = bgMusic;
            musicSource.loop = true;
            musicSource.Play();
        }

        ResetInstructionUI();
        controlsLocked = true;
        Time.timeScale = 1f;
    }

    public void UnlockControls()
    {
        controlsLocked = false;
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
            if (instructionsPanel) instructionsPanel.SetActive(false);
            if (gamePanel) gamePanel.SetActive(false);
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
            lastPoseUI = "N";
            connectedIds.Clear();
            if (poseText) poseText.text = "NEUTRAL";
            if (gyroText) gyroText.text = "GYRO WAITING";
            if (waitingPanel && gamePanel && instructionsPanel)
            {
                waitingPanel.SetActive(true);
                instructionsPanel.SetActive(false);
                gamePanel.SetActive(false);
                controlsLocked = true;
                countdownRunning = false;
                ResetInstructionProgress();
                ResetInstructionUI();
            }
        }

        if (gyroText) gyroText.text = gyroOk ? "GYRO WORKS" : "GYRO WAITING";
        if (accelText) accelText.text = lastAccelStr;

        if (poseText)
        {
            if (lastPoseUI == "L") poseText.text = "LEFT";
            else if (lastPoseUI == "R") poseText.text = "RIGHT";
            else poseText.text = "NEUTRAL";
        }

        if (pedalText && pedalMsgTimer > 0f)
        {
            pedalMsgTimer -= Time.deltaTime;
            if (pedalMsgTimer <= 0f) pedalText.text = "";
        }

        float now = Time.time;
        if (mover1) mover1.maxForwardSpeed = now <= boostEnd1 ? boostMaxSpeed : defaultMaxSpeed;
        if (mover2) mover2.maxForwardSpeed = now <= boostEnd2 ? boostMaxSpeed : defaultMaxSpeed;

        if (player1Text)
        {
            float remBoost = boostEnd1 - now;
            float remSwap = swapEnd1 - now;
            if (remBoost > 0f)
            {
                int s = Mathf.CeilToInt(remBoost);
                player1Text.text = $"Koeksister: pedal boost for {s} seconds";
            }
            else if (remSwap > 0f)
            {
                int s = Mathf.CeilToInt(remSwap);
                player1Text.text = $"Brandy: steering buttons swapped for {s} seconds";
            }
            else player1Text.text = "";
        }

        if (player2Text)
        {
            float remBoost = boostEnd2 - now;
            float remSwap = swapEnd2 - now;
            if (remBoost > 0f)
            {
                int s = Mathf.CeilToInt(remBoost);
                player2Text.text = $"Koeksister: pedal boost for {s} seconds";
            }
            else if (remSwap > 0f)
            {
                int s = Mathf.CeilToInt(remSwap);
                player2Text.text = $"Brandy: steering buttons swapped for {s} seconds";
            }
            else player2Text.text = "";
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
                int pid = EnsureAssignmentFor(key, r.RemoteEndPoint);
                idToEndPoint[pid] = r.RemoteEndPoint;
                if (pid != 0 && !connectedIds.Contains(pid)) connectedIds.Add(pid);
                CheckAdvanceToNextPhase();
                continue;
            }

            var parts = msg.Split(',');
            if (parts.Length < 7) continue;

            int pid2 = GetOrAssignIdFor(key, r.RemoteEndPoint);
            idToEndPoint[pid2] = r.RemoteEndPoint;
            if (pid2 != 0 && !connectedIds.Contains(pid2))
            {
                connectedIds.Add(pid2);
                CheckAdvanceToNextPhase();
            }

            if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float yawBtn))
                ApplyYaw(pid2, yawBtn);

            if (parts.Length >= 6 &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay) &&
                float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float az) &&
                float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float gz))
            {
                lastAccelStr = $"ax {ax:F2} ay {ay:F2} az {az:F2} gz {gz:F2}";

                char pose = DetectPoseChar(ax);
                lastPoseUI = pose == 'L' ? "L" : pose == 'R' ? "R" : "N";

                if (instructionsPanel && instructionsPanel.activeSelf)
                {
                    OnInstructionPose(pid2, pose);
                }
                else if (gamePanel && gamePanel.activeSelf)
                {
                    OnGamePose(pid2, pose);
                }
            }

            gyroOk = parts[6] == "1";

            gotFirstPacket = true;
            lastPacketTime = Time.realtimeSinceStartup;
        }
    }

    void CheckAdvanceToNextPhase()
    {
        if (!waitingPanel || !instructionsPanel || !gamePanel) return;

        if (waitingPanel.activeSelf)
        {
            if (connectedIds.Count >= expectedPlayers)
            {
                waitingPanel.SetActive(false);
                instructionsPanel.SetActive(true);
                gamePanel.SetActive(false);
                controlsLocked = true;
                ResetInstructionProgress();
                ResetInstructionUI();
            }
            return;
        }

        if (instructionsPanel.activeSelf)
        {
            bool p1Needed = expectedPlayers >= 1;
            bool p2Needed = expectedPlayers >= 2;
            bool p1Ready = !p1Needed || tutP1 == TutState.Ready;
            bool p2Ready = !p2Needed || tutP2 == TutState.Ready;

            if (p1Ready && p2Ready)
            {
                instructionsPanel.SetActive(false);
                gamePanel.SetActive(true);
                Time.timeScale = 1f;

                if (!countdownRunning)
                {
                    var cd = countdown ? countdown : gamePanel.GetComponentInChildren<RaceCountdown>(true);
                    if (cd)
                    {
                        countdown = cd;
                        controlsLocked = true;
                        countdown.StartCountdown(this);
                        countdownRunning = true;
                    }
                }
            }
        }
    }

    void OnInstructionPose(int pid, char pose)
    {
        if (pid == 1)
        {
            if (tutP1 == TutState.NeedLeft && pose == 'L') { tutP1 = TutState.NeedRight; UpdateInstructionUI(); }
            else if (tutP1 == TutState.NeedRight && pose == 'R') { tutP1 = TutState.Ready; UpdateInstructionUI(); CheckAdvanceToNextPhase(); }
        }
        else if (pid == 2)
        {
            if (tutP2 == TutState.NeedLeft && pose == 'L') { tutP2 = TutState.NeedRight; UpdateInstructionUI(); }
            else if (tutP2 == TutState.NeedRight && pose == 'R') { tutP2 = TutState.Ready; UpdateInstructionUI(); CheckAdvanceToNextPhase(); }
        }
    }

    void OnGamePose(int pid, char pose)
    {
        if (controlsLocked) return;
        float now = Time.realtimeSinceStartup;

        if (pid == 1)
        {
            if (pose != poseP1)
            {
                if ((lastStepP1 == 'L' && pose == 'R') || (lastStepP1 == 'R' && pose == 'L'))
                {
                    float dt = Mathf.Max(0.0001f, now - lastStepTimeP1);
                    if (mover1) mover1.ApplyPedalPush(dt);
                    FlashPedal("Push");
                }
                if (pose == 'L' || pose == 'R')
                {
                    lastStepP1 = pose;
                    lastStepTimeP1 = now;
                }
                poseP1 = pose;
            }
        }
        else if (pid == 2)
        {
            if (pose != poseP2)
            {
                if ((lastStepP2 == 'L' && pose == 'R') || (lastStepP2 == 'R' && pose == 'L'))
                {
                    float dt = Mathf.Max(0.0001f, now - lastStepTimeP2);
                    if (mover2) mover2.ApplyPedalPush(dt);
                    FlashPedal("Push");
                }
                if (pose == 'L' || pose == 'R')
                {
                    lastStepP2 = pose;
                    lastStepTimeP2 = now;
                }
                poseP2 = pose;
            }
        }
    }

    void ResetInstructionProgress()
    {
        tutP1 = TutState.NeedLeft;
        tutP2 = TutState.NeedLeft;
    }

    void ResetInstructionUI()
    {
        if (expectedPlayers < 2)
        {
            if (p2LeftText) p2LeftText.gameObject.SetActive(false);
            if (p2RightText) p2RightText.gameObject.SetActive(false);
        }
        else
        {
            if (p2LeftText) p2LeftText.gameObject.SetActive(true);
            if (p2RightText) p2RightText.gameObject.SetActive(true);
        }
        SetInstrColors();
    }

    void UpdateInstructionUI()
    {
        SetInstrColors();
    }

    void SetInstrColors()
    {
        Color waitingColor = Color.gray;
        Color doneColor = Color.white;

        if (p1LeftText) p1LeftText.color = (tutP1 == TutState.NeedLeft) ? waitingColor : doneColor;
        if (p1RightText) p1RightText.color = (tutP1 == TutState.Ready) ? doneColor : (tutP1 == TutState.NeedRight ? waitingColor : doneColor);

        if (expectedPlayers >= 2)
        {
            if (p2LeftText) p2LeftText.color = (tutP2 == TutState.NeedLeft) ? waitingColor : doneColor;
            if (p2RightText) p2RightText.color = (tutP2 == TutState.Ready) ? doneColor : (tutP2 == TutState.NeedRight ? waitingColor : doneColor);
        }
    }

    char DetectPoseChar(float ax)
    {
        if (ax >= axRightEnter) return 'R';
        if (ax <= axLeftEnter) return 'L';
        return 'N';
    }

    void FlashPedal(string s)
    {
        if (!pedalText) return;
        pedalText.text = s;
        pedalMsgTimer = pedalMsgHold;
    }

    void ApplyYaw(int pid, float yaw)
    {
        if (controlsLocked) yaw = 0f;

        float now = Time.time;
        if (pid == 1 && now <= swapEnd1) yaw = -yaw;
        if (pid == 2 && now <= swapEnd2) yaw = -yaw;

        if (pid == 1 && player1) player1.SetYawInput(yaw);
        else if (pid == 2 && player2) player2.SetYawInput(yaw);
    }

    bool IsEffectActive(int pid)
    {
        float now = Time.time;
        if (pid == 1) return now < Mathf.Max(boostEnd1, swapEnd1);
        if (pid == 2) return now < Mathf.Max(boostEnd2, swapEnd2);
        return false;
    }

    void ClearEffects(int pid)
    {
        if (pid == 1) { boostEnd1 = 0f; swapEnd1 = 0f; }
        else if (pid == 2) { boostEnd2 = 0f; swapEnd2 = 0f; }
    }

    public bool ApplyPowerUp(int pid)
    {
        if (overrideNewestEffect) ClearEffects(pid);
        else if (IsEffectActive(pid)) return false;

        float until = Time.time + Mathf.Max(0.1f, powerDuration);
        if (pid == 1)
        {
            boostEnd1 = until;
            if (player1Text) player1Text.text = $"Koeksister: pedal boost for {powerDuration:F0} seconds";
        }
        else if (pid == 2)
        {
            boostEnd2 = until;
            if (player2Text) player2Text.text = $"Koeksister: pedal boost for {powerDuration:F0} seconds";
        }
        NotifyPhoneSound(pid, "BOOST");
        return true;
    }

    public bool ApplySetBack(int pid)
    {
        if (overrideNewestEffect) ClearEffects(pid);
        else if (IsEffectActive(pid)) return false;

        float until = Time.time + Mathf.Max(0.1f, setbackDuration);
        if (pid == 1)
        {
            swapEnd1 = until;
            if (player1Text) player1Text.text = $"Brandy: steering buttons swapped for {setbackDuration:F0} seconds";
        }
        else if (pid == 2)
        {
            swapEnd2 = until;
            if (player2Text) player2Text.text = $"Brandy: steering buttons swapped for {setbackDuration:F0} seconds";
        }
        NotifyPhoneSound(pid, "SETBACK");
        return true;
    }

    public void OnPlayerWin(int pid)
    {
        if (sfxSource && winClip) sfxSource.PlayOneShot(winClip);
    }

    void NotifyPhoneSound(int pid, string type)
    {
        var ep = idToEndPoint[pid];
        if (ep == null) return;

        string msg = "SOUND " + type;
        byte[] data = Encoding.UTF8.GetBytes(msg);

        try
        {
            using (var sendClient = new UdpClient())
            {
                sendClient.Send(data, data.Length, ep);
            }
        }
        catch { }
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
            idToEndPoint[id] = ep;
            byte[] ack = Encoding.UTF8.GetBytes($"ASSIGN,{id}");
            try { udp.Send(ack, ack.Length, ep); } catch { }
            Debug.Log($"Assigned player {id} to {key}");
        }

        return id;
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
