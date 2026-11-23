using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Boton "Puntajes" del menu principal (o de cualquier escena): abre la tabla de puntajes.
// Uso: agrega este script a un objeto y conecta tu boton:  OnClick -> RankingMenu.Abrir
public class RankingMenu : MonoBehaviour
{
    [Tooltip("Nivel que se muestra al abrir (1, 2 o 3)")]
    public int nivelInicial = 1;

    private GameObject canvasAbierto;

    public void Abrir()
    {
        if (canvasAbierto != null) return;

        GameObject cgo = new GameObject("RankingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        PanelRanking panel = PanelRanking.Crear(cgo.transform, new Vector2(0.14f, 0.05f), new Vector2(0.86f, 0.95f),
                                                () => { Destroy(cgo); canvasAbierto = null; }, "Cerrar");
        panel.Abrir(Mathf.Clamp(nivelInicial, 1, 3));
    }
}
