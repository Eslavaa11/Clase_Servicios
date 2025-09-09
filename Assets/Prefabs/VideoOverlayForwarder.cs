using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class VideoOverlayForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private VideoBubbleController ctrl;

    void Awake()
    {
        // Busca el controlador en este objeto o en padres
        ctrl = GetComponent<VideoBubbleController>();
        if (!ctrl) ctrl = GetComponentInParent<VideoBubbleController>();
        if (!ctrl)
            Debug.LogWarning("[VideoOverlayForwarder] No se encontró VideoBubbleController en padres.");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ctrl != null) ctrl.HoverEnter(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ctrl != null) ctrl.HoverExit(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (ctrl != null) ctrl.Toggle();
    }
}
