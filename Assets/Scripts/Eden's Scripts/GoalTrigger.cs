using UnityEngine;
using TMPro;

public class GoalTrigger : MonoBehaviour
{
    public NetworkHost host;
    public GameObject winPanel;
    public GameObject playerOneWin;
    public GameObject playerTwoWin;
    public GameObject[] toDisableOnWin;

    bool finished;

    void Awake()
    {
        if (winPanel) winPanel.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (finished) return;

        KartController kart = other.GetComponentInParent<KartController>();
        if (!kart) return;

        string who = "Player";
        if (host)
        {
            if (kart == host.player1) who = "Player 1";
            else if (kart == host.player2) who = "Player 2";
        }

        finished = true;

        if (toDisableOnWin != null)
        {
            foreach (var go in toDisableOnWin)
            {
                if (go) go.SetActive(false);
            }
        }

        if (winPanel) winPanel.SetActive(true);
        if (who == "Player 1")
        {
            playerOneWin.SetActive(true);
            playerTwoWin.SetActive(false);
        }
        else
        {
            playerOneWin.SetActive(false);
            playerTwoWin.SetActive(true);
        }

        if (host) host.enabled = false;
        Time.timeScale = 0f;
    }

    public void OnPressExit()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }
}
