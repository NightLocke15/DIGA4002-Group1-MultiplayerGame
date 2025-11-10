using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using UnityEngine;
using TMPro;

public class NetworkHost : MonoBehaviour
{
    public int listenPort = 7777;

    public KartController player1;
    public KartController player2;
    public PedalMover mover1;
    public PedalMover mover2;

    public Camera camP1;
    public Camera camP2;

    public TMP_Text connectionText;
    public TMP_Text poseText;
    public TMP_Text accelText;
    public TMP_Text gyroText;
    public TMP_Text pedalText;

    [Header("Tilt pose detection")]
    [SerializeField] private float smooth = 0.10f;
    [SerializeField] private float axRightEnter = 0.40f;  
    [SerializeField] private float axRightExit = 0.30f;
    [SerializeField] private float axLeftEnter = -0.40f;
    [SerializeField] private float axLeftExit = -0.30f;
    [SerializeField] private float axNeutralAbs = 0.20f;

    UdpClient udp;
    readonly Dictionary<string, int> addrToId = new Dictionary<string, int>();
    readonly string[] idToAddr = new string[3];

    bool gotFirstPacket = false;
    float lastPacketTime = -999f;
    const float packetTimeout = 1.0f;

    string lastAccelStr = "ax 0.00  ay 0.00  az 0.00  gz 0.00";
    bool gyroOk = false;

    string lastPose = "N";   
    float axSmooth = 0f;
    float azSmooth = 0f;

    char stepSideP1 = 'N';
    float lastStepTimeP1 = 0f;

    char stepSideP2 = 'N';
    float lastStepTimeP2 = 0f;

    float pedalMsgTimer = 0f;
    const float pedalMsgHold = 0.6f;

    void Start()
    {
        if (!player1 || !player2 || !camP1 || !camP2 || !mover1 || !mover2)
        {
            Debug.LogError("Assign rotators, movers, and cameras");
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

        if (connectionText) connectionText.text = "HOST WAITING";
        if (gyroText) gyroText.text = "GYRO WAITING";
        if (accelText) accelText.text = lastAccelStr;
        if (poseText) poseText.text = "NEUTRAL";
        if (pedalText) pedalText.text = "";
    }

    void Update()
    {
        if (gotFirstPacket && Time.realtimeSinceStartup - lastPacketTime > packetTimeout)
        {
            gotFirstPacket = false;
            gyroOk = false;
            lastPose = "N";
            if (connectionText) connectionText.text = "HOST WAITING";
            if (gyroText) gyroText.text = "GYRO WAITING";
            if (poseText) poseText.text = "NEUTRAL";
        }

        if (gotFirstPacket && connectionText) connectionText.text = "CONNECTED";
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

            string msg = Encoding.UTF8.GetString(r.Buffer);
            var parts = msg.Split(',');
            if (parts.Length < 7) continue;

            if (!int.TryParse(parts[0], out int pid)) continue;

            if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float yawBtn))
                ApplyYaw(pid, yawBtn);

            if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax) &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay) &&
                float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float az) &&
                float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float gz))
            {
                lastAccelStr = $"ax {ax:F2}  ay {ay:F2}  az {az:F2}  gz {gz:F2}";

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

        if (pid == 1)
        {
            if ((stepSideP1 == 'L' && side == 'R') || (stepSideP1 == 'R' && side == 'L'))
            {
                float dt = Mathf.Max(0.0001f, now - lastStepTimeP1);
                float impulse = mover1.ApplyPedalPush(dt);

                if (pedalText)
                {
                    pedalText.text = $"Push {impulse:F2}";
                    pedalMsgTimer = pedalMsgHold;
                }
            }
            stepSideP1 = side;
            lastStepTimeP1 = now;
        }
        else if (pid == 2)
        {
            if ((stepSideP2 == 'L' && side == 'R') || (stepSideP2 == 'R' && side == 'L'))
            {
                float dt = Mathf.Max(0.0001f, now - lastStepTimeP2);
                float impulse = mover2.ApplyPedalPush(dt);

                if (pedalText)
                {
                    pedalText.text = $"Push {impulse:F2}";
                    pedalMsgTimer = pedalMsgHold;
                }
            }
            stepSideP2 = side;
            lastStepTimeP2 = now;
        }
    }

    void ApplyYaw(int playerId, float yaw)
    {
        if (playerId == 1 && player1) player1.SetYawInput(yaw);
        else if (playerId == 2 && player2) player2.SetYawInput(yaw);
    }

    void OnApplicationQuit()
    {
        udp?.Close();
    }
}
