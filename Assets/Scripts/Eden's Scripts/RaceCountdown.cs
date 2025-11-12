using UnityEngine;
using TMPro;

public class RaceCountdown : MonoBehaviour
{
    public NetworkHost host;
    public TMP_Text countdownText;
    public int seconds = 5;

    Rigidbody rb1;
    Rigidbody rb2;
    bool locking;

    void Start()
    {
        rb1 = host && host.player1 ? host.player1.GetComponent<Rigidbody>() : null;
        rb2 = host && host.player2 ? host.player2.GetComponent<Rigidbody>() : null;

        if (countdownText) countdownText.gameObject.SetActive(true);
        StartCoroutine(DoCountdown());
    }

    System.Collections.IEnumerator DoCountdown()
    {
        host.controlsLocked = true;
        locking = true;

        for (int s = seconds; s > 0; s--)
        {
            if (countdownText) countdownText.text = s.ToString();
            yield return new WaitForSeconds(1f);
        }

        if (countdownText) countdownText.gameObject.SetActive(false);

        locking = false;
        host.controlsLocked = false;
    }

    void FixedUpdate()
    {
        if (!locking) return;

        if (rb1) rb1.linearVelocity = Vector3.zero;
        if (rb2) rb2.linearVelocity = Vector3.zero;
    }
}
