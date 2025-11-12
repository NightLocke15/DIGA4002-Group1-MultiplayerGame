using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform button;
    private Vector3 initialScale;

    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject tutPanel;

    private void Start()
    {
        initialScale = button.localScale;
        menuPanel.SetActive(true);
        tutPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        button.localScale = initialScale * 1.1f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        button.localScale = initialScale;
    }

    public void Play()
    {
        menuPanel.SetActive(false);
    }

    public void HowTo()
    {
        menuPanel.SetActive(false);
        tutPanel.SetActive(true);
    }

    public void Quit()
    {
        Application.Quit();
    }

    public void Back()
    {
        menuPanel.SetActive(true);
        tutPanel.SetActive(false);
    }
}
