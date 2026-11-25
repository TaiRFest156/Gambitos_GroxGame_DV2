using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Boton "Opciones" del menu principal: abre el mismo panel de opciones que la pausa de los niveles.
// Uso: agrega este script a un objeto y conecta tu boton:  OnClick -> OpcionesMenu.Abrir
public class OpcionesMenu : MonoBehaviour
{
    private GameObject canvasAbierto;

    public void Abrir()
    {
        if (canvasAbierto != null) return;

        GameObject cgo = new GameObject("OpcionesCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasAbierto = cgo;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            UnityEngine.InputSystem.UI.InputSystemUIInputModule m = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            m.AssignDefaultActions();
        }

        Image dim = UIPuntaje.Img("Fondo", cgo.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.82f), null, false);
        dim.raycastTarget = true;

        PanelOpciones panel = PanelOpciones.Crear(cgo.transform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f),
                                                  () => { Destroy(cgo); canvasAbierto = null; });
        panel.Abrir();
    }
}
