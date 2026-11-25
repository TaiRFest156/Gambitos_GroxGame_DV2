using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla para personalizar los controles tactiles con vista previa:
// arrastras el joystick y los botones adonde quieras, y ajustas tamano y transparencia.
// Lo que ves aqui es exactamente lo que aparece en el juego.
public class EditorControlesTactiles : MonoBehaviour
{
    public static void Abrir(Action alCerrar)
    {
        GameObject go = new GameObject("EditorControlesTactiles");
        EditorControlesTactiles e = go.AddComponent<EditorControlesTactiles>();
        e.alCerrar = alCerrar;
        e.Construir();
    }

    private Action alCerrar;
    private PuntajeConfig cfg;
    private RectTransform canvasRT, stickRT, knobRT, fuegoRT, saltoRT;
    private Image stickBase, stickKnob, fuegoFondo, saltoFondo;
    private Slider sTam, sOpac;
    private TextMeshProUGUI vTam, vOpac;

    // valores por defecto (iguales a los de ControlesJugador)
    private Vector2 defStick = new Vector2(-260f, 260f);
    private Vector2 defFuego = new Vector2(-260f, 590f);
    private Vector2 defSalto = new Vector2(-580f, 270f);
    private float radioStick = 130f, tamFuego = 190f, tamSalto = 160f;

    private void Construir()
    {
        cfg = PuntajeConfig.Actual;

        ControlesJugador cj = ControlesJugador.Instance;
        if (cj != null)
        {
            defStick = cj.posicionStick; defFuego = cj.posicionFuego; defSalto = cj.posicionSalto;
            radioStick = cj.radioStick; tamFuego = cj.tamanoBotonFuego; tamSalto = cj.tamanoBotonSalto;
        }

        GameObject cgo = new GameObject("EditorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 560;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRT = (RectTransform)cgo.transform;

        Image dim = UIPuntaje.Img("Fondo", canvasRT, Vector2.zero, Vector2.one, new Color(0.04f, 0.05f, 0.1f, 0.95f), null, false);
        dim.raycastTarget = true;

        TextMeshProUGUI titulo = UIPuntaje.Texto("Titulo", canvasRT, new Vector2(0.2f, 0.88f), new Vector2(0.8f, 0.98f), 30f, 70f,
                                                 FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        titulo.text = "PERSONALIZAR CONTROLES";
        TextMeshProUGUI ayuda = UIPuntaje.Texto("Ayuda", canvasRT, new Vector2(0.2f, 0.82f), new Vector2(0.8f, 0.88f), 14f, 34f,
                                                FontStyles.Normal, TextAlignmentOptions.Center, cfg.colorTexto);
        ayuda.text = "Arrastra el joystick y los botones adonde te queden más cómodos";

        // Lo que verias en el juego (el pulgar derecho)
        stickRT = Elemento("Joystick", 1f, out stickBase);
        stickBase.sprite = ControlesJugador.SpriteDisco();
        Image anillo = UIPuntaje.Img("Anillo", stickRT, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.6f), ControlesJugador.SpriteAnillo(), false);
        anillo.raycastTarget = false;
        knobRT = UIPuntaje.Rect("Knob", stickRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        stickKnob = knobRT.gameObject.AddComponent<Image>();
        stickKnob.sprite = ControlesJugador.SpriteDisco();
        stickKnob.raycastTarget = false;
        Etiqueta(stickRT, "CÁMARA");

        fuegoRT = Elemento("Fuego", 1f, out fuegoFondo);
        fuegoFondo.sprite = ControlesJugador.SpriteDisco();
        fuegoFondo.color = new Color(1f, 0.45f, 0.3f, 0.6f);
        IconoFuego(fuegoRT);
        Etiqueta(fuegoRT, "FUEGO");

        saltoRT = Elemento("Salto", 1f, out saltoFondo);
        saltoFondo.sprite = ControlesJugador.SpriteDisco();
        saltoFondo.color = new Color(0.2f, 0.9f, 0.65f, 0.6f);
        IconoSalto(saltoRT);
        Etiqueta(saltoRT, "SALTO");

        HacerArrastrable(stickRT, p => ControlSettings.GuardarPosStick(p));
        HacerArrastrable(fuegoRT, p => ControlSettings.GuardarPosFuego(p));
        HacerArrastrable(saltoRT, p => ControlSettings.GuardarPosSalto(p));

        PosicionarTodo();
        ConstruirPanelInferior();
    }

    private RectTransform Elemento(string nombre, float tam, out Image img)
    {
        RectTransform rt = UIPuntaje.Rect(nombre, canvasRT, new Vector2(1f, 0f), new Vector2(1f, 0f));
        rt.pivot = new Vector2(0.5f, 0.5f);
        img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = true;
        return rt;
    }

    private void IconoFuego(RectTransform rt)
    {
        Image ai = UIPuntaje.Img("Anillo", rt, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.85f), ControlesJugador.SpriteAnillo(), false);
        ai.raycastTarget = false;
        Image mira = UIPuntaje.Img("Mira", rt, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), Color.white, ControlesJugador.SpriteAnillo(), false);
        mira.raycastTarget = false;
        Image punto = UIPuntaje.Img("Punto", rt, new Vector2(0.44f, 0.44f), new Vector2(0.56f, 0.56f), Color.white, ControlesJugador.SpriteDisco(), false);
        punto.raycastTarget = false;
    }

    private void IconoSalto(RectTransform rt)
    {
        Image ai = UIPuntaje.Img("Anillo", rt, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.85f), ControlesJugador.SpriteAnillo(), false);
        ai.raycastTarget = false;
        Image f = UIPuntaje.Img("Flecha", rt, new Vector2(0.225f, 0.225f), new Vector2(0.775f, 0.775f), Color.white, ControlesJugador.SpriteTriangulo(), false);
        f.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        f.raycastTarget = false;
    }

    private void Etiqueta(RectTransform padre, string texto)
    {
        TextMeshProUGUI t = UIPuntaje.Texto("Etiqueta", padre, new Vector2(0.1f, -0.28f), new Vector2(0.9f, -0.02f), 12f, 28f,
                                            FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTexto);
        t.text = texto;
    }

    private void HacerArrastrable(RectTransform rt, Action<Vector2> guardar)
    {
        ArrastrableUI a = rt.gameObject.AddComponent<ArrastrableUI>();
        a.area = canvasRT;
        a.alMover = guardar;
    }

    // ------------------------------------------------------------------ tamano, transparencia y posiciones
    private void PosicionarTodo()
    {
        stickRT.anchoredPosition = ControlSettings.PosStick(defStick);
        fuegoRT.anchoredPosition = ControlSettings.PosFuego(defFuego);
        saltoRT.anchoredPosition = ControlSettings.PosSalto(defSalto);
        AplicarEstilo();
    }

    private void AplicarEstilo()
    {
        float esc = ControlSettings.EscalaControles;
        float a = ControlSettings.OpacidadControles;

        stickRT.sizeDelta = Vector2.one * radioStick * 2f * esc;
        knobRT.sizeDelta = stickRT.sizeDelta * 0.42f;
        knobRT.anchoredPosition = new Vector2(stickRT.sizeDelta.x * 0.1f, stickRT.sizeDelta.y * 0.1f);
        fuegoRT.sizeDelta = Vector2.one * tamFuego * esc;
        saltoRT.sizeDelta = Vector2.one * tamSalto * esc;

        stickBase.color = new Color(1f, 1f, 1f, a * 0.5f);
        stickKnob.color = new Color(1f, 1f, 1f, a);
        fuegoFondo.color = new Color(1f, 0.45f, 0.3f, a * 0.9f);
        saltoFondo.color = new Color(0.2f, 0.9f, 0.65f, a * 0.9f);
        SetAlfaHijos(fuegoRT, Mathf.Clamp01(a + 0.25f));
        SetAlfaHijos(saltoRT, Mathf.Clamp01(a + 0.25f));
    }

    private static void SetAlfaHijos(RectTransform rt, float alfa)
    {
        foreach (Image i in rt.GetComponentsInChildren<Image>())
        {
            if (i.name == "Etiqueta") continue;
            if (i.transform == rt) continue;
            Color c = i.color;
            c.a = alfa;
            i.color = c;
        }
    }

    private void ConstruirPanelInferior()
    {
        // Tamano y transparencia (abajo a la izquierda)
        RectTransform caja = UIPuntaje.Panel("Ajustes", canvasRT, new Vector2(0.03f, 0.04f), new Vector2(0.42f, 0.34f), 4f);

        TextMeshProUGUI e1 = UIPuntaje.Texto("E1", caja, new Vector2(0.04f, 0.68f), new Vector2(0.34f, 0.92f), 12f, 34f,
                                             FontStyles.Bold, TextAlignmentOptions.MidlineLeft, cfg.colorTexto);
        e1.text = "Tamaño";
        sTam = UIPuntaje.CrearSlider("SliderTam", caja, new Vector2(0.35f, 0.64f), new Vector2(0.82f, 0.96f), 0.6f, 1.6f, ControlSettings.EscalaControles);
        vTam = UIPuntaje.Texto("VTam", caja, new Vector2(0.84f, 0.68f), new Vector2(0.98f, 0.92f), 12f, 32f,
                               FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorOro);
        sTam.onValueChanged.AddListener(v => { ControlSettings.EscalaControles = v; AplicarEstilo(); Etiquetas(); });

        TextMeshProUGUI e2 = UIPuntaje.Texto("E2", caja, new Vector2(0.04f, 0.34f), new Vector2(0.34f, 0.58f), 12f, 34f,
                                             FontStyles.Bold, TextAlignmentOptions.MidlineLeft, cfg.colorTexto);
        e2.text = "Transparencia";
        sOpac = UIPuntaje.CrearSlider("SliderOpac", caja, new Vector2(0.35f, 0.30f), new Vector2(0.82f, 0.62f), 0.2f, 1f, ControlSettings.OpacidadControles);
        vOpac = UIPuntaje.Texto("VOpac", caja, new Vector2(0.84f, 0.34f), new Vector2(0.98f, 0.58f), 12f, 32f,
                                FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorOro);
        sOpac.onValueChanged.AddListener(v => { ControlSettings.OpacidadControles = v; AplicarEstilo(); Etiquetas(); });

        TextMeshProUGUI nota = UIPuntaje.Texto("Nota", caja, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.26f), 10f, 24f,
                                               FontStyles.Italic, TextAlignmentOptions.Center,
                                               new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.6f));
        nota.text = "Opaco = más visible. Transparente = menos estorba.";
        Etiquetas();

        // Botones (abajo, en el centro)
        Button reset = UIPuntaje.Boton("Restablecer", "Restablecer", canvasRT, new Vector2(0.45f, 0.04f), new Vector2(0.62f, 0.14f), TipoBoton.Advertencia);
        reset.onClick.AddListener(() =>
        {
            ControlSettings.RestablecerPosiciones();
            ControlSettings.EscalaControles = 1f;
            ControlSettings.OpacidadControles = 0.6f;
            sTam.SetValueWithoutNotify(1f);
            sOpac.SetValueWithoutNotify(0.6f);
            PosicionarTodo();
            Etiquetas();
        });

        Button listo = UIPuntaje.Boton("Listo", "Listo", canvasRT, new Vector2(0.64f, 0.04f), new Vector2(0.78f, 0.14f), TipoBoton.Primario);
        listo.onClick.AddListener(Cerrar);
    }

    private void Etiquetas()
    {
        vTam.text = Mathf.RoundToInt(ControlSettings.EscalaControles * 100f) + "%";
        vOpac.text = Mathf.RoundToInt(ControlSettings.OpacidadControles * 100f) + "%";
    }

    private void Cerrar()
    {
        OpcionesJuego.Guardar();
        if (alCerrar != null) alCerrar();
        Destroy(gameObject);
    }
}
