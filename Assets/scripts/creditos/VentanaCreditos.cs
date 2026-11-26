using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ============================================================================
// VENTANA DE CREDITOS (la misma ventana del juego: esquinas cortadas, tu tipografia y colores de PuntajeConfig)
// Los nombres suben solos como en los creditos de una pelicula y, al terminar, vuelven a empezar.
//
// USO:
//  1. Agrega este script a un objeto vacio del menu (ej. "VentanaCreditos").
//  2. En el Inspector escribe quienes participaron (Secciones) y ajusta tamaños y velocidad.
//  3. En tu boton "Creditos":  OnClick -> VentanaCreditos.Abrir   (arrastra ESE objeto de la escena, no el archivo)
//
// Controles: mantener presionado/clic = acelerar. X, Esc o "atras" del celular = cerrar.
// ============================================================================
public class VentanaCreditos : MonoBehaviour
{
    [Serializable]
    public class Seccion
    {
        [Tooltip("Titulo de la seccion: Programacion, Arte, Musica...")]
        public string rol = "Rol";
        [TextArea(1, 8)]
        [Tooltip("Una persona por linea")]
        public string nombres = "Nombre Apellido";
    }

    [Header("Contenido")]
    public string titulo = "CRÉDITOS";
    public Seccion[] secciones =
    {
        new Seccion { rol = "Desarrollo",   nombres = "Nombre Apellido\nNombre Apellido" },
        new Seccion { rol = "Arte y diseño", nombres = "Nombre Apellido\nNombre Apellido" },
        new Seccion { rol = "Música y sonido", nombres = "Nombre Apellido" },
        new Seccion { rol = "Agradecimientos", nombres = "Docentes\nFamilia y amigos" }
    };
    [Tooltip("Frase final, antes de que se repita (vacio = ninguna)")]
    public string mensajeFinal = "GRACIAS POR JUGAR";

    [Header("Tamaño del texto (pixeles sobre 1920x1080)")]
    [Tooltip("Multiplica TODOS los tamaños de abajo. 1 = normal, 1.3 = 30% mas grande")]
    [Range(0.4f, 3f)] public float escalaGeneral = 1f;
    public float tamanoTitulo = 64f;
    public float tamanoRol = 34f;
    public float tamanoNombre = 44f;
    [Tooltip("Espacio entre secciones (1 = una linea de nombre)")]
    [Range(0.2f, 4f)] public float espacioEntreSecciones = 1.4f;

    [Header("Movimiento")]
    [Tooltip("Pixeles por segundo")]
    public float velocidad = 70f;
    public float multiplicadorAlPresionar = 4f;
    [Tooltip("Al terminar vuelve a empezar desde abajo")]
    public bool repetir = true;
    [Tooltip("Segundos de pausa antes de repetir")]
    public float pausaAlRepetir = 1.2f;
    [Tooltip("Si NO repite: cierra la ventana al terminar")]
    public bool cerrarAlTerminar = false;

    [Header("Ventana (posicion y tamaño en la pantalla, de 0 a 1)")]
    public Vector2 esquinaInferiorIzquierda = new Vector2(0.27f, 0.06f);
    public Vector2 esquinaSuperiorDerecha = new Vector2(0.73f, 0.94f);
    public bool mostrarAyuda = true;
    public string textoAyuda = "Mantén presionado para acelerar";

    private static GameObject abierta;

    // ------------------------------------------------------------------ abrir / cerrar
    public void Abrir()
    {
        if (abierta != null) return;
        PuntajeConfig cfg = PuntajeConfig.Actual;

        GameObject cgo = new GameObject("CreditosCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 650;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        abierta = cgo;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            UnityEngine.InputSystem.UI.InputSystemUIInputModule m = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            m.AssignDefaultActions();
        }

        Image dim = UIPuntaje.Img("Oscuro", cgo.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.82f), null, false);
        dim.raycastTarget = true;

        // La ventana: mismo marco que pausa y confirmaciones
        RectTransform p = UIPuntaje.Panel("Creditos", cgo.transform, esquinaInferiorIzquierda, esquinaSuperiorDerecha);
        GameObject raiz = UIPuntaje.RaizDe(p);
        CanvasGroup grupo = raiz.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;

        float e = Mathf.Max(0.1f, escalaGeneral);

        TextMeshProUGUI tit = UIPuntaje.Texto("Titulo", p, new Vector2(0.16f, 0.885f), new Vector2(0.84f, 0.975f),
                                              20f, tamanoTitulo * e, FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        tit.text = titulo;

        // Linea de acento bajo el titulo
        UIPuntaje.Img("Linea", p, new Vector2(0.14f, 0.872f), new Vector2(0.86f, 0.878f), cfg.colorBorde, null, false);

        // Zona donde se ven los nombres (recorta lo que se sale)
        RectTransform vista = UIPuntaje.Rect("Vista", p, new Vector2(0.05f, 0.095f), new Vector2(0.95f, 0.86f));
        vista.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI txt = UIPuntaje.Texto("Nombres", vista, Vector2.zero, Vector2.one,
                                              10f, 60f, FontStyles.Normal, TextAlignmentOptions.Top, cfg.colorTexto, true);
        txt.enableAutoSizing = false;
        txt.fontSize = tamanoNombre * e;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.textWrappingMode = TextWrappingModes.Normal;
        txt.margin = new Vector4(16f, 0f, 16f, 0f);
        txt.text = ConstruirTexto(cfg, e);

        RectTransform trt = txt.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        // Desvanecido arriba y abajo para que el texto entre y salga suave
        Color fondo = cfg.fondoPanel != null ? cfg.colorRellenoPanel : cfg.colorPanel;
        fondo.a = 1f;
        Image arriba = UIPuntaje.Img("FadeArriba", p, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.86f), fondo, Degradado(), false);
        arriba.rectTransform.localScale = new Vector3(1f, -1f, 1f);
        UIPuntaje.Img("FadeAbajo", p, new Vector2(0.05f, 0.095f), new Vector2(0.95f, 0.175f), fondo, Degradado(), false);

        if (mostrarAyuda)
        {
            TextMeshProUGUI ayuda = UIPuntaje.Texto("Ayuda", p, new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.085f),
                                                    12f, 26f * e, FontStyles.Normal, TextAlignmentOptions.Center,
                                                    new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.6f));
            ayuda.text = textoAyuda;
        }

        Button cerrar = UIPuntaje.BotonCerrar(p);
        cerrar.onClick.AddListener(Cerrar);

        StartCoroutine(Correr(grupo, raiz.transform as RectTransform, vista, trt, txt));
    }

    public static void Cerrar()
    {
        if (abierta != null) Destroy(abierta);
        abierta = null;
    }

    // ------------------------------------------------------------------ texto
    private string ConstruirTexto(PuntajeConfig cfg, float e)
    {
        string acento = ColorUtility.ToHtmlStringRGB(cfg.colorBorde);
        int sRol = Mathf.RoundToInt(tamanoRol * e);
        int sNom = Mathf.RoundToInt(tamanoNombre * e);
        int sEsp = Mathf.RoundToInt(tamanoNombre * e * espacioEntreSecciones);

        StringBuilder sb = new StringBuilder();
        if (secciones != null)
        {
            for (int i = 0; i < secciones.Length; i++)
            {
                Seccion s = secciones[i];
                if (s == null) continue;
                sb.Append("<color=#").Append(acento).Append("><size=").Append(sRol).Append(">[ ")
                  .Append((s.rol ?? "").ToUpperInvariant()).Append(" ]</size></color>\n");

                string[] lineas = (s.nombres ?? "").Split('\n');
                for (int k = 0; k < lineas.Length; k++)
                {
                    string n = lineas[k].Trim();
                    if (n.Length == 0) continue;
                    sb.Append("<size=").Append(sNom).Append(">").Append(n).Append("</size>\n");
                }
                sb.Append("<size=").Append(sEsp).Append("> </size>\n");
            }
        }

        if (!string.IsNullOrEmpty(mensajeFinal))
            sb.Append("<color=#").Append(acento).Append("><size=").Append(Mathf.RoundToInt(tamanoRol * e * 1.3f)).Append(">")
              .Append(mensajeFinal).Append("</size></color>\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ animacion
    private IEnumerator Correr(CanvasGroup grupo, RectTransform raiz, RectTransform vista, RectTransform contenido, TextMeshProUGUI txt)
    {
        GameObject mia = abierta;

        // Esperar un cuadro para que Unity calcule los tamaños reales
        yield return null;
        Canvas.ForceUpdateCanvases();

        float vh = vista.rect.height;
        float ancho = Mathf.Max(10f, vista.rect.width - 32f);
        float h = txt.GetPreferredValues(txt.text, ancho, 0f).y;
        contenido.sizeDelta = new Vector2(0f, h);

        float y = -vh;                 // el texto empieza justo debajo de la zona visible
        contenido.anchoredPosition = new Vector2(0f, y);

        // Aparicion suave
        float t = 0f;
        while (t < 0.3f && abierta == mia)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.3f);
            grupo.alpha = k;
            raiz.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
            yield return null;
        }
        grupo.alpha = 1f;
        raiz.localScale = Vector3.one;

        float mult = 1f;
        while (abierta == mia)
        {
            if (PidioCerrar()) { Cerrar(); yield break; }

            bool presionando = Pointer.current != null && Pointer.current.press.isPressed;
            mult = Mathf.Lerp(mult, presionando ? multiplicadorAlPresionar : 1f, 8f * Time.unscaledDeltaTime);
            y += velocidad * mult * Time.unscaledDeltaTime;
            contenido.anchoredPosition = new Vector2(0f, y);

            if (y >= h)   // el ultimo renglon ya salio por arriba
            {
                if (repetir)
                {
                    float espera = 0f;
                    while (espera < pausaAlRepetir && abierta == mia)
                    {
                        if (PidioCerrar()) { Cerrar(); yield break; }
                        espera += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    y = -vh;
                }
                else
                {
                    if (cerrarAlTerminar) { Cerrar(); yield break; }
                    y = h;
                }
            }
            yield return null;
        }
    }

    private static bool PidioCerrar()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        Gamepad gp = Gamepad.current;
        return gp != null && gp.buttonEast.wasPressedThisFrame;
    }

    // Degradado vertical: opaco abajo, transparente arriba
    private static Sprite degradado;
    private static Sprite Degradado()
    {
        if (degradado != null) return degradado;
        const int alto = 64;
        Texture2D tex = new Texture2D(4, alto, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < alto; y++)
        {
            float a = 1f - y / (alto - 1f);
            a = a * a;
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        degradado = Sprite.Create(tex, new Rect(0, 0, 4, alto), new Vector2(0.5f, 0.5f), 100f);
        return degradado;
    }
}
