using UnityEngine;
using TMPro;

public class RaceCountdown : MonoBehaviour
{
    public TMP_Text countdownText;
    public int seconds = 5;

    NetworkHost host;
    Rigidbody rb1;
    Rigidbody rb2;

    float remaining;
    bool activeLock;

    public void StartCountdown(NetworkHost hostRef)
    {
        host = hostRef;
        remaining = Mathf.Max(1, seconds);
        host.controlsLocked = true;
        activeLock = true;

        rb1 = host.player1 ? host.player1.GetComponentInChildren<Rigidbody>() : null;
        rb2 = host.player2 ? host.player2.GetComponentInChildren<Rigidbody>() : null;

        if (countdownText)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }

    public void ResetCountdown()
    {
        activeLock = false;
        remaining = 0f;
        if (countdownText) countdownText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!activeLock) return;

        remaining -= Time.unscaledDeltaTime;
        int display = Mathf.Clamp(Mathf.CeilToInt(remaining), 0, 99);
        if (countdownText) countdownText.text = display.ToString();

        if (remaining <= 0f)
        {
            activeLock = false;
            host.controlsLocked = false;
            if (countdownText) countdownText.gameObject.SetActive(false);
        }
    }

    void FixedUpdate()
    {
        if (!activeLock) return;

        if (rb1)
        {
            rb1.linearVelocity = Vector3.zero;
            rb1.angularVelocity = Vector3.zero;
        }
        if (rb2)
        {
            rb2.linearVelocity = Vector3.zero;
            rb2.angularVelocity = Vector3.zero;
        }
    }
}
