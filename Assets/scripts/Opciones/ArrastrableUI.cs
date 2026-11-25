using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Elemento de interfaz que se puede arrastrar (lo usa el editor de controles tactiles).
// Guarda la posicion como pixeles (sobre 1920x1080) desde la esquina inferior derecha.
public class ArrastrableUI : MonoBehaviour, IDragHandler
{
    public RectTransform area;           // el canvas
    public Action<Vector2> alMover;

    public void OnDrag(PointerEventData e)
    {
        if (area == null) return;
        RectTransform rt = (RectTransform)transform;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, null, out local);

        Rect r = area.rect;
        Vector2 offset = new Vector2(local.x - r.width * 0.5f, local.y + r.height * 0.5f);   // desde abajo a la derecha

        float mx = rt.sizeDelta.x * 0.5f, my = rt.sizeDelta.y * 0.5f;
        offset.x = Mathf.Clamp(offset.x, -(r.width - mx), -mx);
        offset.y = Mathf.Clamp(offset.y, my, r.height - my);

        rt.anchoredPosition = offset;
        if (alMover != null) alMover(offset);
    }
}
