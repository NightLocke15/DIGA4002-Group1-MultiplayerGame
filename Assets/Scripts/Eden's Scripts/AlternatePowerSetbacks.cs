using UnityEngine;

public class AlternatePowerSetbacks : MonoBehaviour
{
    public GameObject groupA;      
    public GameObject groupB;     
    public float interval = 2f;   
    public bool startWithA = true; 
    public bool useUnscaledTime = true;

    bool showingA;
    Coroutine loop;

    void OnEnable()
    {
        showingA = startWithA;
        ApplyState();
        loop = StartCoroutine(FlipLoop());
    }

    void OnDisable()
    {
        if (loop != null) StopCoroutine(loop);
    }

    System.Collections.IEnumerator FlipLoop()
    {
        while (true)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(interval);
            else
                yield return new WaitForSeconds(interval);

            showingA = !showingA;
            ApplyState();
        }
    }

    void ApplyState()
    {
        if (groupA) groupA.SetActive(showingA);
        if (groupB) groupB.SetActive(!showingA);
    }
}
