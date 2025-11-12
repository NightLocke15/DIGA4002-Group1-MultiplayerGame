using UnityEngine;
using TMPro;

public class RaceCountdown : MonoBehaviour
{
    public TMP_Text countdownText;
    public int seconds = 5;

    Rigidbody rb1;
    Rigidbody rb2;
    NetworkHost host;
    bool locking;
    bool started;

    public void StartCountdown(NetworkHost hostRef)
    {
        if (started) return;
        started = true;

        host = hostRef;

        rb1 = host.player1 ? host.player1.GetComponentInChildren<Rigidbody>() : null;
        rb2 = host.player2 ? host.player2.GetComponentInChildren<Rigidbody>() : null;

        if (countdownText) countdownText.gameObject.SetActive(true);

        host.controlsLocked = true;
        locking = true;
        StartCoroutine(DoCountdown());
        Debug.Log("Countdown started");
    }

    System.Collections.IEnumerator DoCountdown()
    {
        int s = Mathf.Max(1, seconds);
        while (s > 0)
        {
            if (countdownText) countdownText.text = s.ToString();
            yield return new WaitForSecondsRealtime(1f);
            s--;
        }

        if (countdownText) countdownText.gameObject.SetActive(false);

        locking = false;
        host.controlsLocked = false;
        Debug.Log("Countdown finished");
    }

    void FixedUpdate()
    {
        if (!locking) return;

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

    public void ResetCountdown()
    {
        started = false;
        locking = false;
        if (countdownText) countdownText.gameObject.SetActive(false);
    }
}
