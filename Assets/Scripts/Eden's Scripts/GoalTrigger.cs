using UnityEngine;
using TMPro;

public class GoalTrigger : MonoBehaviour
{
    [Header("Refs")]
    public NetworkHost host;                 // drag your NetworkHost from the scene
    public GameObject winPanel;              // the UI panel that should pop up
    public TMP_Text winnerText;              // optional text to show who won
    public GameObject[] toDisableOnWin;      // drag KartP1 root, KartP2 root, anything else to disable

    private bool finished;

    void Awake()
    {
        if (winPanel) winPanel.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (finished) return;

        // find which kart touched
        KartController kart = other.GetComponentInParent<KartController>();
        if (!kart) return;

        string who = "Player";
        if (host)
        {
            if (kart == host.player1) who = "Player 1";
            else if (kart == host.player2) who = "Player 2";
        }

        finished = true;

        // deactivate karts and anything else you listed
        if (toDisableOnWin != null)
        {
            foreach (var go in toDisableOnWin)
            {
                if (go) go.SetActive(false);
            }
        }

        if (winPanel) winPanel.SetActive(true);
        if (winnerText) winnerText.text = who + " wins";

        // optional, pause the host from processing any more input
        if (host) host.enabled = false;
        Time.timeScale = 0f;   // freeze the scene so nothing drifts
    }

    // hook this to the Exit button OnClick
    public void OnPressExit()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
