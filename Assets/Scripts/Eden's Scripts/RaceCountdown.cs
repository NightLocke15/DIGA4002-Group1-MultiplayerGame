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
    bool active;

    public void StartCountdown(NetworkHost h)
    {
        host = h;
        remaining = Mathf.Max(1, seconds);
        active = true;

        if (countdownText) { countdownText.gameObject.SetActive(true); countdownText.text = Mathf.CeilToInt(remaining).ToString(); }

        if (host && host.player1) rb1 = host.player1.GetComponent<Rigidbody>();
        if (host && host.player2) rb2 = host.player2.GetComponent<Rigidbody>();

        host.controlsLocked = true;
        Time.timeScale = 1f;
    }

    public void ResetCountdown()
    {
        active = false;
        if (countdownText) countdownText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!active) return;

        remaining -= Time.unscaledDeltaTime;
        int display = Mathf.Clamp(Mathf.CeilToInt(remaining), 0, 99);
        if (countdownText) countdownText.text = display.ToString();

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

        if (remaining <= 0f)
        {
            if (countdownText) countdownText.gameObject.SetActive(false);
            active = false;

            if (host) host.UnlockControls();
        }
    }
}
