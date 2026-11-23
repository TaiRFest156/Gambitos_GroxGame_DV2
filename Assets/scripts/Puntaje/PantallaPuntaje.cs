using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de resultados de TODOS los niveles:
// titulo, estrellas animadas, puntaje que cuenta hacia arriba, barra con las 3 metas, resumen,
// "nuevo record", guardar puntaje con tu nombre, ranking, y botones de navegacion.
// Uso:   PantallaPuntaje.Mostrar(datos);
// Estilo: fuentes, estrellas, botones y colores en el asset PuntajeConfig.
public class PantallaPuntaje : MonoBehaviour
{
    public static PantallaPuntaje Instance { get; private set; }

    public static void Mostrar(DatosResultado datos)
    {
        if (Instance != null) Destroy(Instance.gameObject);
        GameObject go = new GameObject("PantallaPuntaje");
        PantallaPuntaje p = go.AddComponent<PantallaPuntaje>();
        p.Iniciar(datos);
    }

    private class FilaUI
    {
        public FilaDetalle datos;
        public CanvasGroup grupo;
        public RectTransform rt;
        public TextMeshProUGUI etiqueta, valor;
    }

    private class AccionBoton
    {
        public string texto;
        public TipoBoton tipo;
        public UnityAction accion;
    }

    private PuntajeConfig cfg;
    private DatosResultado d;
    private int estrellas, mejorPrevio;
    private bool esRecord;
    private bool saltar, guardado;
    private float maxEscala;
    private string registroGuardadoId;

    private RectTransform canvasRT, area, tarjetaRaiz;
    private CanvasGroup tarjetaGrupo, botonesGrupo;
    private TextMeshProUGUI titulo, subtitulo, puntajeTxt, etiquetaPuntaje, insignia;
    private Image barraRelleno;
    private readonly Image[] estrellasImg = new Image[3];
    private readonly Image[] marcadores = new Image[3];
    private readonly bool[] estrellaLista = new bool[3];
    private readonly List<FilaUI> filasUI = new List<FilaUI>();

    private Button btnGuardar;
    private GameObject panelGuardar;
    private TMP_InputField campoNombre;
    private TextMeshProUGUI estadoGuardar;
    private Button btnConfirmarGuardar;
    private PanelRanking ranking;

    // ================================================================== inicio
    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            ControlesJugador.CursorLibreForzado = false;
        }
    }

    private void Iniciar(DatosResultado datos)
    {
        d = datos;
        cfg = PuntajeConfig.Actual;

        d.meta = Mathf.Max(1, d.meta);
        estrellas = d.Estrellas;
        mejorPrevio = RankingService.MejorPersonal(d.nivel);
        esRecord = estrellas >= 1 && d.puntaje > mejorPrevio;

        // Se guarda el progreso (estrellas y mejor puntaje personal) aunque el jugador no guarde su nombre
        if (estrellas >= 1)
        {
            LevelStars.Save(d.nivel, estrellas);
            RankingService.RegistrarIntento(d.nivel, d.puntaje);
        }

        float masAlto = d.umbrales.Length >= 3 ? d.umbrales[2] : 2f;
        maxEscala = d.meta * Mathf.Max(1f, masAlto) * 1.08f;

        if (d.congelarJuego) Time.timeScale = 0f;
        ControlesJugador.CursorLibreForzado = true;   // el raton / dedo se usa para esta pantalla

        AsegurarEventSystem();
        Construir();
        StartCoroutine(Secuencia());
    }

    private void AsegurarEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        UnityEngine.InputSystem.UI.InputSystemUIInputModule m = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        m.AssignDefaultActions();
    }

    // ================================================================== construccion
    private void Construir()
    {
        GameObject cgo = new GameObject("PuntajeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRT = (RectTransform)cgo.transform;

        Image dim = UIPuntaje.Img("Fondo", canvasRT, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.82f), null, false);
        dim.raycastTarget = true;

        area = UIPuntaje.Panel("Tarjeta", canvasRT, new Vector2(0.09f, 0.05f), new Vector2(0.91f, 0.95f));
        tarjetaRaiz = (RectTransform)UIPuntaje.RaizDe(area).transform;
        tarjetaGrupo = tarjetaRaiz.gameObject.AddComponent<CanvasGroup>();

        // Tocar la tarjeta salta la animacion
        Image saltarImg = UIPuntaje.Img("Saltar", area, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f), null, false);
        saltarImg.raycastTarget = true;
        Button saltarBtn = saltarImg.gameObject.AddComponent<Button>();
        saltarBtn.transition = Selectable.Transition.None;
        saltarBtn.onClick.AddListener(() => saltar = true);

        ConstruirEncabezado();
        ConstruirIzquierda();
        ConstruirResumen();
        ConstruirBotones();
        ConstruirPanelGuardar();

        // Ranking dentro de la tarjeta
        ranking = PanelRanking.Crear(area, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), null, "Volver");
    }

    private void ConstruirEncabezado()
    {
        titulo = UIPuntaje.Texto("Titulo", area, new Vector2(0.05f, 0.865f), new Vector2(0.95f, 0.985f), 30f, 80f,
                                 FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        subtitulo = UIPuntaje.Texto("Subtitulo", area, new Vector2(0.05f, 0.815f), new Vector2(0.95f, 0.87f), 14f, 34f,
                                    FontStyles.Normal, TextAlignmentOptions.Center, cfg.colorTexto);

        string t;
        switch (estrellas)
        {
            case 3: t = "¡PERFECTO!"; break;
            case 2: t = "¡MUY BIEN!"; break;
            case 1: t = "¡NIVEL COMPLETADO!"; break;
            default: t = "NIVEL NO SUPERADO"; break;
        }
        titulo.text = t;
        titulo.color = estrellas >= 1 ? cfg.colorTitulo : cfg.colorError;

        string sub = d.tituloNivel + "   -   Meta: " + d.meta.ToString("N0", CultureInfo.CurrentCulture) + " pts";
        if (estrellas == 0)
            sub = d.tituloNivel + "   -   Te faltaron " + Mathf.Max(0, d.meta - d.puntaje).ToString("N0", CultureInfo.CurrentCulture) + " puntos";
        subtitulo.text = sub;
    }

    private void ConstruirIzquierda()
    {
        // Estrellas (la del medio un poco mas alta)
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.04f + i * 0.155f;
            float y0 = i == 1 ? 0.585f : 0.555f;
            Image e = UIPuntaje.Img("Estrella" + i, area, new Vector2(x0, y0), new Vector2(x0 + 0.15f, y0 + 0.25f), Color.white, null, false);
            UIPuntaje.PonerEstrella(e, false);
            estrellasImg[i] = e;
        }

        etiquetaPuntaje = UIPuntaje.Texto("EtiquetaPuntaje", area, new Vector2(0.05f, 0.51f), new Vector2(0.50f, 0.555f), 12f, 28f,
                                          FontStyles.Bold, TextAlignmentOptions.Center,
                                          new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.7f));
        etiquetaPuntaje.text = "PUNTAJE";

        puntajeTxt = UIPuntaje.Texto("Puntaje", area, new Vector2(0.05f, 0.405f), new Vector2(0.50f, 0.51f), 30f, 110f,
                                     FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorOro, true);
        puntajeTxt.text = "0";

        // Barra con las metas de 1, 2 y 3 estrellas
        float bx0 = 0.07f, bx1 = 0.48f;
        RectTransform barra = UIPuntaje.Rect("Barra", area, new Vector2(bx0, 0.30f), new Vector2(bx1, 0.345f));
        Image fondoBarra = barra.gameObject.AddComponent<Image>();
        fondoBarra.sprite = UIPuntaje.RectRedondeado();
        fondoBarra.type = Image.Type.Sliced;
        fondoBarra.color = new Color(1f, 1f, 1f, 0.14f);
        fondoBarra.raycastTarget = false;

        barraRelleno = UIPuntaje.Img("Relleno", barra, Vector2.zero, new Vector2(0f, 1f), cfg.colorBorde, null, false);
        barraRelleno.rectTransform.offsetMin = new Vector2(4f, 4f);
        barraRelleno.rectTransform.offsetMax = new Vector2(0f, -4f);

        for (int i = 0; i < 3; i++)
        {
            float mult = d.umbrales.Length > i ? d.umbrales[i] : i + 1f;
            float frac = Mathf.Clamp01(d.meta * mult / maxEscala);
            float cx = bx0 + (bx1 - bx0) * frac;

            Image m = UIPuntaje.Img("Marcador" + i, area, new Vector2(cx - 0.016f, 0.352f), new Vector2(cx + 0.016f, 0.402f), Color.white, null, false);
            UIPuntaje.PonerEstrella(m, false);
            marcadores[i] = m;
        }

        insignia = UIPuntaje.Texto("Insignia", area, new Vector2(0.05f, 0.225f), new Vector2(0.50f, 0.29f), 14f, 40f,
                                   FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorOro);
        insignia.gameObject.SetActive(false);
    }

    private void ConstruirResumen()
    {
        RectTransform det = UIPuntaje.Panel("Resumen", area, new Vector2(0.53f, 0.225f), new Vector2(0.95f, 0.82f), 4f);

        TextMeshProUGUI cab = UIPuntaje.Texto("Cabecera", det, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.99f), 14f, 34f,
                                              FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorBorde, true);
        cab.text = "RESUMEN";

        int n = Mathf.Min(8, d.filas.Count);
        float top = 0.89f, bottom = 0.03f;
        float alto = (top - bottom) / 8f;
        for (int i = 0; i < n; i++)
        {
            float y1 = top - i * alto;
            float y0 = y1 - alto + 0.006f;

            FilaUI f = new FilaUI();
            f.datos = d.filas[i];
            Image fondo = UIPuntaje.Img("Fila" + i, det, new Vector2(0.03f, y0), new Vector2(0.97f, y1),
                                        i % 2 == 0 ? new Color(1f, 1f, 1f, 0.07f) : new Color(1f, 1f, 1f, 0.03f),
                                        UIPuntaje.RectRedondeado(), true);
            f.rt = fondo.rectTransform;
            f.grupo = fondo.gameObject.AddComponent<CanvasGroup>();
            f.grupo.alpha = 0f;

            f.etiqueta = UIPuntaje.Texto("Etiqueta", f.rt, new Vector2(0.03f, 0f), new Vector2(0.62f, 1f), 12f, 32f,
                                         FontStyles.Normal, TextAlignmentOptions.MidlineLeft, cfg.colorTexto);
            f.etiqueta.text = f.datos.etiqueta;
            f.valor = UIPuntaje.Texto("Valor", f.rt, new Vector2(0.60f, 0f), new Vector2(0.97f, 1f), 12f, 34f,
                                      FontStyles.Bold, TextAlignmentOptions.MidlineRight, cfg.colorOro);
            filasUI.Add(f);
        }
    }

    private void ConstruirBotones()
    {
        RectTransform zona = UIPuntaje.Rect("Botones", area, new Vector2(0f, 0f), new Vector2(1f, 0.215f));
        botonesGrupo = zona.gameObject.AddComponent<CanvasGroup>();
        botonesGrupo.alpha = 0f;
        botonesGrupo.interactable = false;
        botonesGrupo.blocksRaycasts = false;

        // Fila de arriba: guardar y ranking
        btnGuardar = UIPuntaje.Boton("BtnGuardar", "Guardar puntaje", zona, new Vector2(0.05f, 0.54f), new Vector2(0.49f, 0.98f), TipoBoton.Primario);
        btnGuardar.onClick.AddListener(AbrirGuardar);
        btnGuardar.gameObject.SetActive(d.permitirGuardar && estrellas >= 1);

        Button btnRank = UIPuntaje.Boton("BtnRanking", "Ver ranking", zona, new Vector2(0.51f, 0.54f), new Vector2(0.95f, 0.98f), TipoBoton.Secundario);
        btnRank.onClick.AddListener(() => ranking.Abrir(d.nivel, registroGuardadoId));

        if (!btnGuardar.gameObject.activeSelf)
        {
            // Sin boton de guardar, el ranking ocupa todo el ancho (sin huecos)
            RectTransform r = (RectTransform)btnRank.transform;
            r.anchorMin = new Vector2(0.25f, 0.54f);
            r.anchorMax = new Vector2(0.75f, 0.98f);
        }

        // Fila de abajo: navegacion
        List<AccionBoton> acciones = new List<AccionBoton>();
        acciones.Add(new AccionBoton { texto = "Reintentar", tipo = TipoBoton.Advertencia, accion = Reintentar });

        bool puedeContinuar = estrellas >= 1 && (d.alContinuar != null || !string.IsNullOrEmpty(d.escenaSiguiente));
        if (puedeContinuar)
            acciones.Add(new AccionBoton { texto = d.textoContinuar, tipo = TipoBoton.Primario, accion = Continuar });

        acciones.Add(new AccionBoton { texto = "Elegir nivel", tipo = TipoBoton.Secundario, accion = () => IrA(cfg.escenaSeleccion) });
        acciones.Add(new AccionBoton { texto = "Menú", tipo = TipoBoton.Secundario, accion = () => IrA(cfg.escenaMenu) });

        float ancho = 0.9f, hueco = 0.015f;
        float w = (ancho - hueco * (acciones.Count - 1)) / acciones.Count;
        for (int i = 0; i < acciones.Count; i++)
        {
            float x0 = 0.05f + i * (w + hueco);
            Button b = UIPuntaje.Boton("Btn" + acciones[i].texto, acciones[i].texto, zona, new Vector2(x0, 0.04f),
                                       new Vector2(x0 + w, 0.48f), acciones[i].tipo);
            b.onClick.AddListener(acciones[i].accion);
        }
    }

    private void ConstruirPanelGuardar()
    {
        RectTransform g = UIPuntaje.Panel("Guardar", area, new Vector2(0.2f, 0.14f), new Vector2(0.8f, 0.86f));
        panelGuardar = UIPuntaje.RaizDe(g);

        TextMeshProUGUI t = UIPuntaje.Texto("Titulo", g, new Vector2(0.08f, 0.82f), new Vector2(0.84f, 0.96f), 24f, 60f,
                                            FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        t.text = "GUARDAR PUNTAJE";

        Button x = UIPuntaje.BotonCerrar(g);
        x.onClick.AddListener(CerrarGuardar);

        TextMeshProUGUI resumen = UIPuntaje.Texto("Resumen", g, new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.80f), 14f, 40f,
                                                  FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorOro);
        resumen.text = d.tituloNivel + "   -   " + d.puntaje.ToString("N0", CultureInfo.CurrentCulture) + " pts   -   " + Estrellas(estrellas);

        TextMeshProUGUI et = UIPuntaje.Texto("Etiqueta", g, new Vector2(0.1f, 0.56f), new Vector2(0.9f, 0.65f), 12f, 32f,
                                             FontStyles.Normal, TextAlignmentOptions.MidlineLeft, cfg.colorTexto);
        et.text = "Tu nombre:";

        // Campo de texto (TMP_InputField armado por codigo)
        RectTransform campo = UIPuntaje.Rect("Campo", g, new Vector2(0.1f, 0.41f), new Vector2(0.9f, 0.56f));
        Image campoImg = campo.gameObject.AddComponent<Image>();
        if (cfg.fondoCampoTexto != null)
        {
            campoImg.sprite = cfg.fondoCampoTexto;
            campoImg.type = cfg.fondoCampoTexto.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            campoImg.color = Color.white;
        }
        else
        {
            campoImg.sprite = UIPuntaje.RectRedondeado();
            campoImg.type = Image.Type.Sliced;
            campoImg.color = new Color(1f, 1f, 1f, 0.14f);
        }

        RectTransform zonaTexto = UIPuntaje.Rect("ZonaTexto", campo, Vector2.zero, Vector2.one);
        zonaTexto.offsetMin = new Vector2(20f, 8f);
        zonaTexto.offsetMax = new Vector2(-20f, -8f);
        zonaTexto.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = UIPuntaje.Texto("Placeholder", zonaTexto, Vector2.zero, Vector2.one, 20f, 48f,
                                                      FontStyles.Italic, TextAlignmentOptions.MidlineLeft,
                                                      new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.45f));
        placeholder.enableAutoSizing = false;
        placeholder.fontSize = 44f;
        placeholder.text = "Escribe tu nombre...";

        TextMeshProUGUI texto = UIPuntaje.Texto("Texto", zonaTexto, Vector2.zero, Vector2.one, 20f, 48f,
                                                FontStyles.Normal, TextAlignmentOptions.MidlineLeft, cfg.colorTitulo);
        texto.enableAutoSizing = false;
        texto.fontSize = 44f;

        campoNombre = campo.gameObject.AddComponent<TMP_InputField>();
        campoNombre.targetGraphic = campoImg;
        campoNombre.textViewport = zonaTexto;
        campoNombre.textComponent = texto;
        campoNombre.placeholder = placeholder;
        campoNombre.characterLimit = Mathf.Max(3, cfg.maxCaracteresNombre);
        campoNombre.lineType = TMP_InputField.LineType.SingleLine;
        campoNombre.contentType = TMP_InputField.ContentType.Name;
        campoNombre.text = RankingService.UltimoNombre;

        estadoGuardar = UIPuntaje.Texto("Estado", g, new Vector2(0.1f, 0.31f), new Vector2(0.9f, 0.40f), 12f, 30f,
                                        FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorExito);
        estadoGuardar.text = "";

        btnConfirmarGuardar = UIPuntaje.Boton("Confirmar", "Guardar", g, new Vector2(0.1f, 0.08f), new Vector2(0.48f, 0.27f), TipoBoton.Primario);
        btnConfirmarGuardar.onClick.AddListener(ConfirmarGuardar);

        Button cancelar = UIPuntaje.Boton("Cancelar", "Cancelar", g, new Vector2(0.52f, 0.08f), new Vector2(0.9f, 0.27f), TipoBoton.Secundario);
        cancelar.onClick.AddListener(CerrarGuardar);

        panelGuardar.SetActive(false);
    }

    private static string Estrellas(int n)
    {
        return n + (n == 1 ? " estrella" : " estrellas");
    }

    // ================================================================== animacion
    private IEnumerator Esperar(float seg)
    {
        float t = 0f;
        while (t < seg && !saltar)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator Secuencia()
    {
        // Entrada de la tarjeta
        UIPuntaje.Sonar(cfg.sonidoPanel, ProceduralSfx.Whoosh(), 1.2f, 0.4f);
        float t = 0f;
        const float dur = 0.38f;
        tarjetaGrupo.alpha = 0f;
        while (t < dur && !saltar)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            tarjetaGrupo.alpha = k;
            tarjetaRaiz.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(k));
            yield return null;
        }
        tarjetaGrupo.alpha = 1f;
        tarjetaRaiz.localScale = Vector3.one;

        // Filas del resumen, una por una (con sus numeros contando)
        for (int i = 0; i < filasUI.Count && !saltar; i++)
        {
            yield return MostrarFila(filasUI[i]);
            yield return Esperar(0.08f);
        }

        // Puntaje total + barra + estrellas
        if (!saltar) yield return AnimarPuntaje();

        EstadoFinal();   // si se salto la animacion, deja todo como corresponde

        // Sonido final
        if (estrellas == 3) UIPuntaje.Sonar(null, ProceduralSfx.Exito(), 1f, 0.7f);
        else if (estrellas == 0) UIPuntaje.Sonar(null, ProceduralSfx.Error(), 1f, 0.45f);

        // Record / mejor puntaje
        yield return Esperar(0.25f);
        MostrarInsignia();

        // Botones
        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            botonesGrupo.alpha = Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        botonesGrupo.alpha = 1f;
        botonesGrupo.interactable = true;
        botonesGrupo.blocksRaycasts = true;
    }

    private IEnumerator MostrarFila(FilaUI f)
    {
        float t = 0f;
        const float dur = 0.22f;
        Vector2 pos0 = f.rt.anchoredPosition;
        while (t < dur && !saltar)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            f.grupo.alpha = k;
            f.rt.anchoredPosition = pos0 + new Vector2(Mathf.Lerp(40f, 0f, k), 0f);
            yield return null;
        }
        f.grupo.alpha = 1f;
        f.rt.anchoredPosition = pos0;

        if (!f.datos.esNumero)
        {
            f.valor.text = f.datos.texto;
            yield break;
        }

        // Numero que cuenta hacia arriba
        float cuenta = 0f;
        const float durCuenta = 0.5f;
        float proxTick = 0f;
        while (cuenta < durCuenta && !saltar)
        {
            cuenta += Time.unscaledDeltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(cuenta / durCuenta), 3f);
            PonerValorFila(f, Mathf.RoundToInt(f.datos.numero * e));
            if (cuenta >= proxTick)
            {
                proxTick = cuenta + 0.07f;
                UIPuntaje.Sonar(null, ProceduralSfx.Nota(5), 1.6f, 0.08f);
            }
            yield return null;
        }
        PonerValorFila(f, f.datos.numero);
    }

    private void PonerValorFila(FilaUI f, int v)
    {
        f.valor.text = f.datos.prefijo + v.ToString("N0", CultureInfo.CurrentCulture) + f.datos.sufijo;
        if (f.datos.conColor) f.valor.color = f.datos.color;
        else f.valor.color = f.datos.numero < 0 ? cfg.colorError : cfg.colorOro;
    }

    private IEnumerator AnimarPuntaje()
    {
        float dur = 1.4f + 0.4f * estrellas;
        float t = 0f;
        int siguiente = 0;
        float proxTick = 0f;

        while (t < dur && !saltar)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            int v = Mathf.RoundToInt(d.puntaje * e);
            MostrarPuntaje(v);

            while (siguiente < estrellas && v >= UmbralPuntos(siguiente))
            {
                StartCoroutine(PopEstrella(siguiente));
                siguiente++;
            }

            if (t >= proxTick)
            {
                proxTick = t + 0.06f;
                UIPuntaje.Sonar(null, ProceduralSfx.Nota(4), 1f + 0.5f * e, 0.07f);
            }
            yield return null;
        }

        MostrarPuntaje(d.puntaje);

        // Estrellas que quedaron pendientes por redondeo
        while (siguiente < estrellas && !saltar)
        {
            StartCoroutine(PopEstrella(siguiente));
            siguiente++;
            yield return Esperar(0.28f);
        }

        // Latido final del numero
        if (!saltar)
        {
            float p = 0f;
            RectTransform rt = puntajeTxt.rectTransform;
            while (p < 0.3f && !saltar)
            {
                p += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(Mathf.Clamp01(p / 0.3f) * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }
    }

    private int UmbralPuntos(int i)
    {
        float mult = d.umbrales.Length > i ? d.umbrales[i] : i + 1f;
        return Mathf.RoundToInt(d.meta * mult);
    }

    private void MostrarPuntaje(int v)
    {
        puntajeTxt.text = v.ToString("N0", CultureInfo.CurrentCulture);
        float frac = Mathf.Clamp01(v / maxEscala);
        barraRelleno.rectTransform.anchorMax = new Vector2(frac, 1f);
    }

    private IEnumerator PopEstrella(int i)
    {
        if (estrellaLista[i]) yield break;
        estrellaLista[i] = true;

        UIPuntaje.PonerEstrella(estrellasImg[i], true);
        UIPuntaje.PonerEstrella(marcadores[i], true);
        UIPuntaje.Sonar(cfg.sonidoEstrella, ProceduralSfx.Nota(Mathf.Min(i + 2, 5)), 1f, 0.9f);
        StartCoroutine(Destellos(estrellasImg[i].rectTransform));

        RectTransform rt = estrellasImg[i].rectTransform;
        float t = 0f;
        const float dur = 0.5f;
        while (t < dur && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(2.6f, 1f, EaseOutBack(k));
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 0f, k));
            yield return null;
        }
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }

    // Chispas doradas alrededor de la estrella
    private IEnumerator Destellos(RectTransform origen)
    {
        const int cantidad = 12;
        RectTransform[] parts = new RectTransform[cantidad];
        Vector2[] dirs = new Vector2[cantidad];
        for (int i = 0; i < cantidad; i++)
        {
            float ang = (i / (float)cantidad) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            dirs[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(140f, 260f);

            Image im = UIPuntaje.Img("Destello", area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), cfg.colorOro, UIPuntaje.Circulo(), false);
            RectTransform rt = im.rectTransform;
            rt.sizeDelta = new Vector2(18f, 18f);
            rt.position = origen.position;
            parts[i] = rt;
        }

        Vector3[] inicio = new Vector3[cantidad];
        for (int i = 0; i < cantidad; i++) inicio[i] = parts[i].position;

        float t = 0f;
        const float dur = 0.7f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float desp = 1f - Mathf.Pow(1f - k, 2f);
            for (int i = 0; i < cantidad; i++)
            {
                if (parts[i] == null) continue;
                parts[i].position = inicio[i] + (Vector3)(dirs[i] * desp * (canvasRT.lossyScale.x));
                parts[i].localScale = Vector3.one * (1f - k);
                Image im = parts[i].GetComponent<Image>();
                Color c = cfg.colorOro;
                c.a = 1f - k;
                im.color = c;
            }
            yield return null;
        }
        for (int i = 0; i < cantidad; i++) if (parts[i] != null) Destroy(parts[i].gameObject);
    }

    // Deja la pantalla en su estado final (tambien se usa al saltar la animacion)
    private void EstadoFinal()
    {
        foreach (FilaUI f in filasUI)
        {
            f.grupo.alpha = 1f;
            if (f.datos.esNumero) PonerValorFila(f, f.datos.numero);
            else f.valor.text = f.datos.texto;
        }

        MostrarPuntaje(d.puntaje);

        for (int i = 0; i < 3; i++)
        {
            if (i < estrellas)
            {
                estrellaLista[i] = true;
                UIPuntaje.PonerEstrella(estrellasImg[i], true);
                UIPuntaje.PonerEstrella(marcadores[i], true);
                estrellasImg[i].rectTransform.localScale = Vector3.one;
                estrellasImg[i].rectTransform.localRotation = Quaternion.identity;
            }
        }
        puntajeTxt.rectTransform.localScale = Vector3.one;
        tarjetaGrupo.alpha = 1f;
        tarjetaRaiz.localScale = Vector3.one;
    }

    private void MostrarInsignia()
    {
        if (estrellas == 0)
        {
            insignia.gameObject.SetActive(false);
            return;
        }

        insignia.gameObject.SetActive(true);
        if (esRecord)
        {
            insignia.text = mejorPrevio > 0 ? "¡NUEVO RÉCORD!" : "¡PRIMER RÉCORD!";
            insignia.color = cfg.colorOro;
            UIPuntaje.Sonar(cfg.sonidoRecord, ProceduralSfx.Exito(), 1.15f, 0.6f);
            StartCoroutine(Latido(insignia.rectTransform));
        }
        else
        {
            insignia.text = "Tu mejor: " + mejorPrevio.ToString("N0", CultureInfo.CurrentCulture);
            insignia.color = new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.75f);
        }
    }

    private IEnumerator Latido(RectTransform rt)
    {
        float t = 0f;
        while (rt != null)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * 5f));
            yield return null;
        }
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // ================================================================== guardar
    private void AbrirGuardar()
    {
        if (guardado) return;
        estadoGuardar.text = "";
        btnConfirmarGuardar.interactable = true;
        panelGuardar.SetActive(true);
        campoNombre.ActivateInputField();
    }

    private void CerrarGuardar()
    {
        panelGuardar.SetActive(false);
    }

    private void ConfirmarGuardar()
    {
        if (guardado) return;

        string nombre = campoNombre.text.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            estadoGuardar.color = cfg.colorError;
            estadoGuardar.text = "Escribe un nombre para guardar";
            UIPuntaje.Sonar(null, ProceduralSfx.Error(), 1f, 0.35f);
            return;
        }

        btnConfirmarGuardar.interactable = false;
        RegistroPuntaje r = RankingService.Nuevo(nombre, d.nivel, d.puntaje, estrellas);

        RankingService.Guardar(r, ok =>
        {
            if (this == null) return;
            if (ok)
            {
                guardado = true;
                registroGuardadoId = r.id;
                estadoGuardar.color = cfg.colorExito;
                estadoGuardar.text = "¡Puntaje guardado!";
                UIPuntaje.Sonar(cfg.sonidoGuardar, ProceduralSfx.Exito(), 1f, 0.6f);
                UIPuntaje.PonerTexto(btnGuardar, "Guardado");
                btnGuardar.interactable = false;
                StartCoroutine(IrAlRankingTrasGuardar());
            }
            else
            {
                btnConfirmarGuardar.interactable = true;
                estadoGuardar.color = cfg.colorError;
                estadoGuardar.text = "No se pudo guardar. Intenta de nuevo";
            }
        });
    }

    private IEnumerator IrAlRankingTrasGuardar()
    {
        yield return Esperar(0f);
        float t = 0f;
        while (t < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        panelGuardar.SetActive(false);
        ranking.Abrir(d.nivel, registroGuardadoId);   // se ve tu puesto resaltado
    }

    // ================================================================== navegacion
    private void Reintentar()
    {
        Cargar(SceneManager.GetActiveScene().buildIndex, null);
    }

    private void IrA(string escena)
    {
        Cargar(-1, escena);
    }

    private void Continuar()
    {
        if (d.alContinuar != null)
        {
            Time.timeScale = 1f;
            UnityAction accion = d.alContinuar.Invoke;
            Destroy(gameObject);   // la pantalla se cierra y el juego sigue
            accion();
        }
        else if (!string.IsNullOrEmpty(d.escenaSiguiente))
        {
            Cargar(-1, d.escenaSiguiente);
        }
    }

    private void Cargar(int indice, string escena)
    {
        Time.timeScale = 1f;
        if (indice >= 0) SceneManager.LoadScene(indice);
        else SceneManager.LoadScene(escena);
    }
}
