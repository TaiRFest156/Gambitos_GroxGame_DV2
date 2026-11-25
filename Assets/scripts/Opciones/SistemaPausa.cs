using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Boton y menu de pausa, IGUALES en los 3 niveles.
//  - Boton arriba a la derecha (personalizable con tu propia imagen en PuntajeConfig).
//  - "EN PAUSA": Continuar, Reiniciar nivel, Opciones, Salir al menu principal.
//  - Congela el juego, oscurece el fondo y oculta el boton mientras esta en pausa.
//  - PC: tecla de pausa (Esc por defecto, se puede cambiar en Opciones) o Start del mando.
// Se crea solo (lo piden ControlesJugador y ResidueLevelManager).
public class SistemaPausa : MonoBehaviour
{
    public static SistemaPausa Instance { get; private set; }
    public static bool EnPausa { get; private set; }

    public static SistemaPausa Asegurar()
    {
        if (Instance != null) return Instance;
        SistemaPausa ya = FindFirstObjectByType<SistemaPausa>();
        if (ya != null) return ya;
        return new GameObject("SistemaPausa").AddComponent<SistemaPausa>();
    }

    [Header("Boton de pausa (pixeles sobre 1920x1080, desde la esquina superior derecha)")]
    public Vector2 posicionBoton = new Vector2(-80f, -80f);
    public float tamanoBoton = 110f;

    [Header("Tu pausa anterior: estos objetos se ocultan al empezar")]
    public GameObject[] ocultarAlIniciar;

    private PuntajeConfig cfg;
    private RectTransform canvasRT;
    private GameObject botonGO, oscuroGO, panelGO, confirmarGO;
    private Image oscuroImg;
    private CanvasGroup panelGrupo;
    private TextMeshProUGUI confirmarTitulo, confirmarMensaje;
    private PanelOpciones opciones;
    private bool opcionesAbiertas, confirmando;
    private float escalaAntes = 1f;
    private Action accionConfirmada;

    // ================================================================== inicio
    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        cfg = PuntajeConfig.Actual;

        Construir();

        if (ocultarAlIniciar != null)
            foreach (GameObject g in ocultarAlIniciar)
                if (g != null) g.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            EnPausa = false;
        }
    }

    // ================================================================== bucle
    private void Update()
    {
        // Seguridad: si algun script devolvio la velocidad del juego mientras estamos en pausa
        if (EnPausa && Time.timeScale != 0f) Time.timeScale = 0f;

        bool bloqueado = ManualSystem.EstaAbierto || PantallaPuntaje.Instance != null || ControlesJugador.CursorLibreForzado;

        // El boton solo se ve cuando se puede pausar
        bool verBoton = !EnPausa && !bloqueado && Time.timeScale != 0f;
        if (botonGO.activeSelf != verBoton) botonGO.SetActive(verBoton);

        // Tecla de pausa (PC) / Start del mando
        Gamepad gp = Gamepad.current;
        bool pedido = TeclasJuego.PulsadaEsteFrame(AccionTecla.Pausa) || (gp != null && gp.startButton.wasPressedThisFrame);
        if (pedido && !ManualSystem.EstaAbierto && PantallaPuntaje.Instance == null && !PanelOpciones.Capturando)
            Alternar();
    }

    // ================================================================== acciones
    public void Alternar()
    {
        if (!EnPausa) { Pausar(); return; }

        if (confirmando) CerrarConfirmacion();
        else if (opcionesAbiertas) opciones.Cerrar();
        else Continuar();
    }

    public void Pausar()
    {
        if (EnPausa || Time.timeScale == 0f) return;   // si ya esta congelado (manual, intro) no se pausa

        escalaAntes = Time.timeScale;
        Time.timeScale = 0f;
        EnPausa = true;

        oscuroGO.SetActive(true);
        panelGO.SetActive(true);
        confirmarGO.SetActive(false);
        confirmando = false;
        StartCoroutine(Aparecer());

        UIPuntaje.Sonar(cfg.sonidoPanel, ProceduralSfx.Nota(2), 0.9f, 0.5f);
    }

    public void Continuar()
    {
        if (!EnPausa) return;

        EnPausa = false;
        Time.timeScale = escalaAntes > 0f ? escalaAntes : 1f;

        oscuroGO.SetActive(false);
        panelGO.SetActive(false);
        confirmarGO.SetActive(false);
        confirmando = false;
        opcionesAbiertas = false;
        OpcionesJuego.Guardar();
    }

    private void AbrirOpciones()
    {
        opcionesAbiertas = true;
        panelGO.SetActive(false);
        opciones.Abrir();
    }

    private void AlCerrarOpciones()
    {
        opcionesAbiertas = false;
        if (EnPausa) panelGO.SetActive(true);
    }

    private void Confirmar(string titulo, string mensaje, Action accion)
    {
        confirmarTitulo.text = titulo;
        confirmarMensaje.text = mensaje;
        accionConfirmada = accion;
        confirmando = true;
        panelGO.SetActive(false);
        confirmarGO.SetActive(true);
    }

    private void CerrarConfirmacion()
    {
        confirmando = false;
        confirmarGO.SetActive(false);
        if (EnPausa) panelGO.SetActive(true);
    }

    private void Reiniciar()
    {
        Salir(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
    }

    private void IrAlMenu()
    {
        Salir(() => SceneManager.LoadScene(cfg.escenaMenu));
    }

    private void Salir(Action cargar)
    {
        EnPausa = false;
        Time.timeScale = 1f;
        OpcionesJuego.Guardar();
        cargar();
    }

    private IEnumerator Aparecer()
    {
        float t = 0f;
        const float dur = 0.22f;
        RectTransform rt = (RectTransform)panelGO.transform;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            oscuroImg.color = new Color(0f, 0f, 0f, 0.7f * k);
            panelGrupo.alpha = k;
            rt.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        oscuroImg.color = new Color(0f, 0f, 0f, 0.7f);
        panelGrupo.alpha = 1f;
        rt.localScale = Vector3.one;
    }

    // ================================================================== construccion
    private void Construir()
    {
        GameObject cgo = new GameObject("PausaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 520;   // por encima del HUD, del manual y de la pantalla de puntaje
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRT = (RectTransform)cgo.transform;

        ConstruirBoton();

        // Fondo oscuro
        oscuroImg = UIPuntaje.Img("Oscuro", canvasRT, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f), null, false);
        oscuroImg.raycastTarget = true;
        oscuroGO = oscuroImg.gameObject;
        oscuroGO.SetActive(false);

        ConstruirPanel();
        ConstruirConfirmacion();

        // Opciones (el mismo panel del menu principal)
        opciones = PanelOpciones.Crear(canvasRT, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f), AlCerrarOpciones);
    }

    private void ConstruirBoton()
    {
        RectTransform rt = UIPuntaje.Rect("BotonPausa", canvasRT, new Vector2(1f, 1f), new Vector2(1f, 1f));
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tamanoBoton, tamanoBoton);
        rt.anchoredPosition = posicionBoton;
        botonGO = rt.gameObject;

        Image img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = true;

        if (cfg.iconoBotonPausa != null)
        {
            // Tu propia imagen
            img.sprite = cfg.iconoBotonPausa;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
        {
            // Por defecto: circulo con borde y el simbolo de pausa
            img.sprite = UIPuntaje.Circulo();
            img.color = cfg.colorBorde;

            Image interior = UIPuntaje.Img("Interior", rt, Vector2.zero, Vector2.one, cfg.colorPanel, UIPuntaje.Circulo(), false);
            interior.rectTransform.offsetMin = new Vector2(6f, 6f);
            interior.rectTransform.offsetMax = new Vector2(-6f, -6f);

            UIPuntaje.Img("BarraIzq", rt, new Vector2(0.34f, 0.28f), new Vector2(0.46f, 0.72f), cfg.colorTitulo, UIPuntaje.RectRedondeado(), true);
            UIPuntaje.Img("BarraDer", rt, new Vector2(0.54f, 0.28f), new Vector2(0.66f, 0.72f), cfg.colorTitulo, UIPuntaje.RectRedondeado(), true);
        }

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = Color.white;
        b.colors = cb;
        b.onClick.AddListener(() => { UIPuntaje.SonarBoton(); Pausar(); });
    }

    private void ConstruirPanel()
    {
        RectTransform p = UIPuntaje.Panel("Pausa", canvasRT, new Vector2(0.33f, 0.08f), new Vector2(0.67f, 0.92f));
        panelGO = UIPuntaje.RaizDe(p);
        panelGrupo = panelGO.AddComponent<CanvasGroup>();

        TextMeshProUGUI titulo = UIPuntaje.Texto("Titulo", p, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), 30f, 80f,
                                                 FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        titulo.text = cfg.tituloPausa;

        Boton(p, cfg.textoContinuar, 0.63f, TipoBoton.Primario, Continuar);
        Boton(p, cfg.textoReiniciar, 0.45f, TipoBoton.Advertencia,
              () => Confirmar("¿REINICIAR?", "Perderás el progreso de esta partida.", Reiniciar));
        Boton(p, cfg.textoOpciones, 0.27f, TipoBoton.Secundario, AbrirOpciones);
        Boton(p, cfg.textoSalirMenu, 0.09f, TipoBoton.Secundario,
              () => Confirmar("¿SALIR AL MENÚ?", "Perderás el progreso de esta partida.", IrAlMenu));

        panelGO.SetActive(false);
    }

    private void Boton(RectTransform padre, string texto, float y, TipoBoton tipo, Action accion)
    {
        Button b = UIPuntaje.Boton("Btn" + texto, texto, padre, new Vector2(0.1f, y), new Vector2(0.9f, y + 0.15f), tipo);
        b.onClick.AddListener(() => accion());
    }

    private void ConstruirConfirmacion()
    {
        RectTransform c = UIPuntaje.Panel("Confirmar", canvasRT, new Vector2(0.30f, 0.28f), new Vector2(0.70f, 0.72f));
        confirmarGO = UIPuntaje.RaizDe(c);

        confirmarTitulo = UIPuntaje.Texto("Titulo", c, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.92f), 24f, 64f,
                                          FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        confirmarMensaje = UIPuntaje.Texto("Mensaje", c, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.66f), 16f, 40f,
                                           FontStyles.Normal, TextAlignmentOptions.Center, cfg.colorTexto);

        Button si = UIPuntaje.Boton("Si", "Sí", c, new Vector2(0.08f, 0.1f), new Vector2(0.48f, 0.36f), TipoBoton.Advertencia);
        si.onClick.AddListener(() => { if (accionConfirmada != null) accionConfirmada(); });

        Button no = UIPuntaje.Boton("No", "No", c, new Vector2(0.52f, 0.1f), new Vector2(0.92f, 0.36f), TipoBoton.Primario);
        no.onClick.AddListener(CerrarConfirmacion);

        confirmarGO.SetActive(false);
    }
}
