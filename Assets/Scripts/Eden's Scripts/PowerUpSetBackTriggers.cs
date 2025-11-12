using UnityEngine;

public class PowerUpSetBackTriggers : MonoBehaviour
{
    public enum PickupKind { PowerUp, SetBack }

    public NetworkHost host;
    public bool detectKindFromTag = true;
    public PickupKind kind = PickupKind.PowerUp;
    public bool deactivateOnPickup = false;   // uncheck to keep cubes visible

    void Awake()
    {
        if (detectKindFromTag)
        {
            string t = gameObject.tag;
            if (!string.IsNullOrEmpty(t))
            {
                if (t.ToLower().Contains("power")) kind = PickupKind.PowerUp;
                else if (t.ToLower().Contains("set")) kind = PickupKind.SetBack;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!host) return;

        int pid = 0;
        var kc = other.GetComponent<KartController>();
        if (kc == host.player1) pid = 1;
        else if (kc == host.player2) pid = 2;
        if (pid == 0) return;

        bool applied = false;
        if (kind == PickupKind.PowerUp) applied = host.ApplyPowerUp(pid);
        else applied = host.ApplySetBack(pid);

        if (applied && deactivateOnPickup) gameObject.SetActive(false);
    }
}
