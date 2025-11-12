using UnityEngine;

public class PowerUpSetBackTriggers : MonoBehaviour
{
    public NetworkHost host;
    public string powerTag = "Power Up 1";
    public string setbackTag = "Set Back 1";

    void OnTriggerEnter(Collider other)
    {
        if (!host) return;

        var kart = other.GetComponentInParent<KartController>();
        if (!kart) return;

        int pid = 0;
        if (kart == host.player1) pid = 1;
        else if (kart == host.player2) pid = 2;
        if (pid == 0) return;

        string t = gameObject.tag;
        if (t == powerTag) host.ApplyPowerUp(pid);
        else if (t == setbackTag) host.ApplySetBack(pid);
    }
}
