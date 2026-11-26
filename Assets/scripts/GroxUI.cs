using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// GroxUI: ajuste automatico de la interfaz a cualquier pantalla.
//  - GroxCanvasFit: se crea SOLO al abrir el juego (no hay que ponerlo en ninguna escena).
//    Mantiene el area de referencia 1920x1080 siempre completa en pantalla:
//      * pantalla mas ancha que 16:9 (celulares largos) -> escala por ALTO
//      * pantalla mas cuadrada (tablet, ventana del editor) -> escala por ANCHO
//    Asi nada se sale ni se encima, en ningun aspecto.
//  - GroxSafeOffset: aparta un elemento de las muescas (notch) y bordes redondeados.
// ============================================================================
public class GroxCanvasFit : MonoBehaviour
{
    private static GroxCanvasFit instancia;
    private float proximo;
    private int ancho, alto;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (instancia != null) return;
        GameObject go = new GameObject("GroxCanvasFit");
        DontDestroyOnLoad(go);
        instancia = go.AddComponent<GroxCanvasFit>();
    }

    private void Update()
    {
        bool cambioPantalla = Screen.width != ancho || Screen.height != alto;
        if (!cambioPantalla && Time.unscaledTime < proximo) return;

        proximo = Time.unscaledTime + 0.5f;   // tambien revisa canvases creados despues (los de por codigo)
        ancho = Screen.width;
        alto = Screen.height;

        float aspecto = alto > 0 ? (float)ancho / alto : 16f / 9f;
        float match = aspecto >= 1920f / 1080f ? 1f : 0f;

        CanvasScaler[] scalers = FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None);
        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler s = scalers[i];
            if (s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            if (Mathf.Abs(s.referenceResolution.x - 1920f) > 1f || Mathf.Abs(s.referenceResolution.y - 1080f) > 1f) continue;

            if (s.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            if (!Mathf.Approximately(s.matchWidthOrHeight, match))
                s.matchWidthOrHeight = match;
        }
    }
}

// Ponlo en un elemento anclado a una esquina o borde: lo desplaza hacia adentro lo que
// ocupen las muescas / bordes redondeados (Screen.safeArea). En PC no hace nada.
[DisallowMultipleComponent]
public class GroxSafeOffset : MonoBehaviour
{
    private RectTransform rt;
    private Vector2 basePos;
    private bool listo;
    private Rect ultima = new Rect(-1f, -1f, -1f, -1f);
    private float ultimaEscala = -1f;

    private void LateUpdate()
    {
        if (rt == null) rt = transform as RectTransform;
        if (rt == null) return;

        Canvas c = GetComponentInParent<Canvas>();
        if (c == null) return;
        Canvas raiz = c.rootCanvas;
        float k = raiz.scaleFactor > 0f ? raiz.scaleFactor : 1f;

        Rect sa = Screen.safeArea;
        if (listo && sa == ultima && Mathf.Approximately(k, ultimaEscala)) return;

        if (!listo) { basePos = rt.anchoredPosition; listo = true; }
        ultima = sa;
        ultimaEscala = k;

        float izq = sa.xMin / k;
        float der = (Screen.width - sa.xMax) / k;
        float arr = (Screen.height - sa.yMax) / k;
        float abj = sa.yMin / k;

        float dx = rt.anchorMin.x >= 0.5f ? -der : izq;
        float dy = rt.anchorMin.y >= 0.5f ? -arr : abj;
        rt.anchoredPosition = basePos + new Vector2(dx, dy);
    }
}
