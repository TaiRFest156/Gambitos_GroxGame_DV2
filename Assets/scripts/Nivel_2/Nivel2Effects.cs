using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Efectos visuales y sonoros del Nivel 2:
//  - cuenta regresiva "3, 2, 1, ¡YA!"
//  - niebla de contaminacion progresiva (sube con los errores y con ir atrasado, se limpia al acertar)
//  - tiempo que pulsa en rojo, puntaje que cuenta, late, y cambia de color
//  - "+10 / -5" flotantes y distintivo de racha
// Lo crea y configura ResidueLevelManager (no hace falta ponerlo a mano).
// Depende de ProceduralSfx (del Nivel 3).
public class Nivel2Effects : MonoBehaviour
{
    [Header("Referencias (las completa ResidueLevelManager)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public ContainerFeedback[] contenedores;

    [Header("Niebla de contaminacion")]
    public bool nieblaActiva = true;
    public Color colorNiebla = new Color(0.45f, 0.5f, 0.38f);
    [Range(0.1f, 1f)] public float opacidadMaxima = 0.55f;
    [Range(4, 14)] public int cantidadNubes = 14;
    [Tooltip("Cuanto sube la contaminacion con cada error (0 a 1)")]
    public float subidaPorError = 0.22f;
    [Tooltip("Cuanto baja con cada acierto (se suma un extra por racha)")]
    public float bajadaPorAcierto = 0.12f;
    [Tooltip("Niebla maxima por ir atrasado respecto al ritmo para llegar a la meta")]
    [Range(0f, 0.6f)] public float maximoPorRitmo = 0.35f;
    [Tooltip("Con cuantos errores seguidos aparece el aviso rojo en los bordes")]
    public int erroresParaAdvertir = 3;

    [Header("Tiempo y puntaje")]
    public float segundosAlerta = 10f;
    public Color colorAlerta = new Color(1f, 0.2f, 0.2f);
    public Color colorMeta = new Color(1f, 0.84f, 0.1f);

    [Header("Racha (solo visual, no cambia los puntos)")]
    public int rachaMinima = 3;

    public TMP_FontAsset fuente;

    // ---- interfaz propia
    private RectTransform fxRT;
    private TextMeshProUGUI cuentaTxt, rachaTxt;
    private Coroutine cuentaCo, rachaCo;
    private AudioSource audioSrc;

    // ---- niebla
    private class Nube
    {
        public RectTransform rt;
        public Image img;
        public Vector2 ancla;
        public float fase, vel, tam, umbral;
    }
    private RectTransform nieblaRoot;
    private Image vinetaImg, alertaImg;
    private readonly List<Nube> nubes = new List<Nube>();
    private float eventos, ritmo, visible, alerta, alertaObjetivo;

    // ---- puntaje y tiempo
    private int objetivoPuntaje, ultimoMostrado = -1, metaActual = 1050;
    private float mostrado;
    private Color scoreBase = Color.white, timerBase = Color.white, flashColor;
    private Vector3 scoreEscala = Vector3.one, timerEscala = Vector3.one;
    private float flash, punch;
    private float restante = 999f;
    private bool enJuego;
    private int ultimoTick = -1;

    // ================================================================== inicio
    private void Awake()
    {
        GameObject cgo = new GameObject("Nivel2FX_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        Canvas c = cgo.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 150;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        fxRT = (RectTransform)cgo.transform;

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;

        // Cuenta regresiva
        RectTransform rc = NuevoRect("Cuenta", fxRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rc.sizeDelta = new Vector2(1200f, 500f);
        cuentaTxt = Texto("Texto", rc, Vector2.zero, Vector2.one, 100f, 320f, FontStyles.Bold, Color.white);
        cuentaTxt.outlineWidth = 0.3f;
        cuentaTxt.outlineColor = new Color32(10, 10, 30, 255);
        cuentaTxt.alpha = 0f;

        // Distintivo de racha
        RectTransform rr = NuevoRect("Racha", fxRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rr.sizeDelta = new Vector2(520f, 80f);
        rachaTxt = Texto("Texto", rr, Vector2.zero, Vector2.one, 24f, 60f, FontStyles.Bold, Color.white);
        rachaTxt.outlineWidth = 0.25f;
        rachaTxt.outlineColor = new Color32(10, 10, 30, 255);
        rachaTxt.alpha = 0f;
    }

    // Lo llama ResidueLevelManager
    public void Configurar(TextMeshProUGUI score, TextMeshProUGUI timer, ContainerFeedback[] conts)
    {
        scoreText = score;
        timerText = timer;
        contenedores = conts;

        if (scoreText != null)
        {
            scoreBase = scoreText.color;
            scoreEscala = scoreText.rectTransform.localScale;
            scoreText.text = "Puntaje: 0";
        }
        if (timerText != null)
        {
            timerBase = timerText.color;
            timerEscala = timerText.rectTransform.localScale;
        }

        ConstruirNiebla();
    }

    // ================================================================== cuenta regresiva
    // tono >= 0: numero (suena una nota distinta). tono < 0: "¡YA!"
    public void MostrarCuenta(string texto, int tono)
    {
        if (cuentaCo != null) StopCoroutine(cuentaCo);
        cuentaCo = StartCoroutine(CuentaRutina(texto, tono < 0));

        if (tono < 0) Sonido(ProceduralSfx.Ding(), 1f);
        else Sonido(ProceduralSfx.Nota(Mathf.Clamp(tono, 0, 4)), 1f);
    }

    private IEnumerator CuentaRutina(string texto, bool esYa)
    {
        cuentaTxt.text = texto;
        cuentaTxt.color = esYa ? new Color(0.35f, 1f, 0.55f) : Color.white;
        RectTransform rt = cuentaTxt.rectTransform;

        float dur = esYa ? 0.9f : 0.95f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);

            float escala;
            if (esYa) escala = Mathf.Lerp(0.8f, 1.7f, k);
            else escala = k < 0.25f ? Mathf.Lerp(2.4f, 1f, 1f - Mathf.Pow(1f - k / 0.25f, 3f)) : Mathf.Lerp(1f, 0.9f, (k - 0.25f) / 0.75f);

            rt.localScale = Vector3.one * escala;
            cuentaTxt.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            yield return null;
        }
        cuentaTxt.alpha = 0f;
        cuentaCo = null;
    }

    // ================================================================== resultado de cada lanzamiento
    public void OnResultado(bool acierto, int racha, int erroresSeguidos, int delta, RectTransform contenedor)
    {
        if (acierto)
        {
            eventos = Mathf.Max(0f, eventos - (bajadaPorAcierto + 0.02f * Mathf.Min(racha, 5)));
            alertaObjetivo = 0f;
            if (racha >= rachaMinima) MostrarRacha(racha);
        }
        else
        {
            eventos = Mathf.Min(1f, eventos + subidaPorError);
            alertaObjetivo = (erroresParaAdvertir > 0 && erroresSeguidos >= erroresParaAdvertir) ? 1f : 0f;
            OcultarRacha();
        }

        if (delta != 0)
        {
            Color col = delta > 0 ? new Color(0.35f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f);
            flashColor = col;
            flash = 1f;
            punch = 1f;
            if (contenedor != null) TextoFlotante(contenedor, (delta > 0 ? "+" : "") + delta, col);
        }
    }

    public void SetPuntaje(int valor)
    {
        objetivoPuntaje = valor;
    }

    // Se llama cada frame mientras se juega
    public void Tick(float tiempoRestante, float duracion, int score, int meta)
    {
        enJuego = true;
        restante = tiempoRestante;
        metaActual = meta;

        // Ritmo: cuanto falta respecto al puntaje que "toca" tener a esta altura del nivel
        float transcurrido = Mathf.Clamp01(1f - tiempoRestante / Mathf.Max(1f, duracion));
        float esperado = meta * transcurrido;
        float deficit = Mathf.Clamp01((esperado - score) / (meta * 0.5f));
        ritmo = deficit * maximoPorRitmo;

        ActualizarTemporizador();
    }

    public void FinDeNivel()
    {
        enJuego = false;
        if (timerText != null)
        {
            timerText.color = timerBase;
            timerText.rectTransform.localScale = timerEscala;
        }
    }

    // ================================================================== tiempo
    private void ActualizarTemporizador()
    {
        if (timerText == null) return;

        if (restante <= segundosAlerta && restante > 0f)
        {
            float hz = restante <= 5f ? 4f : 2f;
            float onda = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * 2f * Mathf.PI);
            timerText.color = Color.Lerp(timerBase, colorAlerta, 0.3f + 0.7f * onda);
            timerText.rectTransform.localScale = timerEscala * (1f + 0.12f * onda);

            int seg = Mathf.CeilToInt(restante);
            if (restante <= 5f && seg != ultimoTick)
            {
                ultimoTick = seg;
                Sonido(ProceduralSfx.Nota(3), 0.7f);
            }
        }
        else
        {
            timerText.color = timerBase;
            timerText.rectTransform.localScale = timerEscala;
        }
    }

    // ================================================================== puntaje + niebla (cada frame)
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        ActualizarPuntaje(dt);
        ActualizarNiebla(dt);
    }

    private void ActualizarPuntaje(float dt)
    {
        if (scoreText == null) return;

        if (!Mathf.Approximately(mostrado, objetivoPuntaje))
        {
            float vel = Mathf.Max(80f, Mathf.Abs(objetivoPuntaje - mostrado) * 6f);
            mostrado = Mathf.MoveTowards(mostrado, objetivoPuntaje, vel * dt);
        }

        int n = Mathf.RoundToInt(mostrado);
        if (n != ultimoMostrado)
        {
            scoreText.text = "Puntaje: " + n;
            ultimoMostrado = n;
        }

        bool metaAlcanzada = objetivoPuntaje >= metaActual;
        bool apuro = enJuego && restante <= segundosAlerta && restante > 0f && !metaAlcanzada;
        float onda = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

        Color col = scoreBase;
        if (metaAlcanzada) col = colorMeta;
        else if (apuro) col = Color.Lerp(scoreBase, colorAlerta, 0.35f + 0.65f * onda);

        if (flash > 0f)
        {
            flash -= dt * 3f;
            col = Color.Lerp(col, flashColor, Mathf.Clamp01(flash));
        }
        scoreText.color = col;

        punch = Mathf.Max(0f, punch - dt * 5f);
        float escala = 1f + 0.3f * punch + (apuro ? 0.06f * onda : 0f);
        scoreText.rectTransform.localScale = scoreEscala * escala;
    }

    // ================================================================== niebla
    private void ConstruirNiebla()
    {
        if (!nieblaActiva || nieblaRoot != null) return;

        // La niebla va DENTRO del Canvas del juego, justo detras de los elementos del HUD:
        // por encima del fondo pero por debajo del puntaje, el tiempo y los contenedores.
        Canvas hud = null;
        if (scoreText != null) hud = scoreText.canvas;
        else if (timerText != null) hud = timerText.canvas;

        Transform padre;
        int indice = -1;
        if (hud != null)
        {
            Canvas raiz = hud.rootCanvas;
            padre = raiz.transform;
            indice = MenorIndiceHUD(raiz.transform);
        }
        else
        {
            padre = fxRT;   // sin referencias: usa el canvas propio
        }

        nieblaRoot = NuevoRect("Niebla_Contaminacion", padre, Vector2.zero, Vector2.one);
        if (indice >= 0) nieblaRoot.SetSiblingIndex(indice);

        // Borde oscuro (viñeta) y aviso rojo
        vinetaImg = NuevaImagen("Vineta", nieblaRoot, Nivel2Sprites.Vineta(), new Color(0.12f, 0.12f, 0.06f, 0f));
        alertaImg = NuevaImagen("AlertaRoja", nieblaRoot, Nivel2Sprites.Vineta(), new Color(0.9f, 0.1f, 0.1f, 0f));

        // Nubes alrededor de los bordes (el centro queda libre para ver el residuo)
        Vector2[] anclas =
        {
            new Vector2(0.05f, 0.92f), new Vector2(0.30f, 0.97f), new Vector2(0.62f, 0.97f), new Vector2(0.93f, 0.92f),
            new Vector2(0.03f, 0.65f), new Vector2(0.02f, 0.35f), new Vector2(0.97f, 0.65f), new Vector2(0.98f, 0.35f),
            new Vector2(0.07f, 0.08f), new Vector2(0.32f, 0.03f), new Vector2(0.64f, 0.03f), new Vector2(0.93f, 0.08f),
            new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.50f)
        };

        int cuantas = Mathf.Min(cantidadNubes, anclas.Length);
        System.Random azar = new System.Random(11);
        for (int i = 0; i < cuantas; i++)
        {
            Nube n = new Nube();
            n.ancla = anclas[i];
            n.fase = (float)azar.NextDouble() * 6.28f;
            n.vel = 0.15f + (float)azar.NextDouble() * 0.2f;
            n.tam = 0.7f + (float)azar.NextDouble() * 0.5f;
            n.umbral = (i + 1f) / (cuantas + 1f);

            n.img = NuevaImagen("Nube" + i, nieblaRoot, Nivel2Sprites.Blob(), new Color(colorNiebla.r, colorNiebla.g, colorNiebla.b, 0f));
            n.rt = n.img.rectTransform;
            n.rt.anchorMin = n.ancla;
            n.rt.anchorMax = n.ancla;
            n.rt.pivot = new Vector2(0.5f, 0.5f);
            nubes.Add(n);
        }

        nieblaRoot.gameObject.SetActive(false);
    }

    private int MenorIndiceHUD(Transform raizCanvas)
    {
        int menor = int.MaxValue;
        List<Transform> refs = new List<Transform>();
        if (scoreText != null) refs.Add(scoreText.transform);
        if (timerText != null) refs.Add(timerText.transform);
        if (contenedores != null)
            foreach (ContainerFeedback c in contenedores)
                if (c != null) refs.Add(c.transform);

        foreach (Transform r in refs)
        {
            Transform t = r;
            while (t != null && t.parent != raizCanvas) t = t.parent;
            if (t != null) menor = Mathf.Min(menor, t.GetSiblingIndex());
        }
        return menor == int.MaxValue ? -1 : menor;
    }

    private void ActualizarNiebla(float dt)
    {
        if (nieblaRoot == null) return;

        float objetivo = Mathf.Clamp01(Mathf.Max(eventos, ritmo));
        // Se ensucia rapido, se limpia despacio
        visible = Mathf.MoveTowards(visible, objetivo, (objetivo > visible ? 0.9f : 0.22f) * dt);
        alerta = Mathf.MoveTowards(alerta, alertaObjetivo, dt * 3f);

        bool algo = visible > 0.005f || alerta > 0.01f;
        if (nieblaRoot.gameObject.activeSelf != algo) nieblaRoot.gameObject.SetActive(algo);
        if (!algo) return;

        float p = visible;
        float t = Time.unscaledTime;
        Rect r = nieblaRoot.rect;
        float lado = Mathf.Min(r.width, r.height);

        Color v = vinetaImg.color;
        v.a = Mathf.Clamp01((p - 0.15f) / 0.85f) * 0.7f * opacidadMaxima / 0.55f;
        vinetaImg.color = v;

        Color a = alertaImg.color;
        a.a = alerta * (0.28f + 0.2f * Mathf.Sin(t * 7f));
        alertaImg.color = a;

        foreach (Nube n in nubes)
        {
            // Las nubes van apareciendo una a una y creciendo a medida que empeora
            float aparece = Mathf.Clamp01((p - n.umbral * 0.8f) / 0.2f);
            float respira = 0.75f + 0.25f * Mathf.Sin(t * 0.6f + n.fase);

            Color c = colorNiebla;
            c.a = aparece * opacidadMaxima * respira;
            n.img.color = c;

            float tam = lado * 0.9f * n.tam * (0.6f + 0.6f * p);
            n.rt.sizeDelta = new Vector2(tam, tam);
            n.rt.anchoredPosition = new Vector2(
                Mathf.Sin(t * n.vel + n.fase) * 0.03f * r.width,
                Mathf.Cos(t * n.vel * 0.8f + n.fase) * 0.03f * r.height);
        }
    }

    // ================================================================== textos flotantes y racha
    public void TextoFlotante(RectTransform origen, string texto, Color color)
    {
        Vector2 local = LocalDesde(origen);

        RectTransform rt = NuevoRect("Flotante", fxRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rt.sizeDelta = new Vector2(360f, 110f);
        rt.anchoredPosition = local + new Vector2(0f, 60f);

        TextMeshProUGUI t = Texto("Texto", rt, Vector2.zero, Vector2.one, 30f, 90f, FontStyles.Bold, color);
        t.outlineWidth = 0.25f;
        t.outlineColor = new Color32(10, 10, 30, 255);
        t.text = texto;

        StartCoroutine(FlotarRutina(rt, t));
    }

    private IEnumerator FlotarRutina(RectTransform rt, TextMeshProUGUI t)
    {
        const float dur = 0.9f;
        Vector2 inicio = rt.anchoredPosition;
        float tt = 0f;
        while (tt < dur && rt != null)
        {
            tt += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(tt / dur);
            rt.anchoredPosition = inicio + new Vector2(0f, 130f * (1f - Mathf.Pow(1f - k, 2f)));
            rt.localScale = Vector3.one * (k < 0.2f ? Mathf.Lerp(0.6f, 1.2f, k / 0.2f) : Mathf.Lerp(1.2f, 1f, (k - 0.2f) / 0.8f));
            t.alpha = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    private void MostrarRacha(int racha)
    {
        // Debajo del puntaje
        if (scoreText != null)
        {
            Vector3[] esq = new Vector3[4];
            scoreText.rectTransform.GetWorldCorners(esq);
            Vector3 abajoCentro = (esq[0] + esq[3]) * 0.5f;
            Canvas c = scoreText.canvas;
            Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? c.worldCamera : null;
            Vector2 pantalla = RectTransformUtility.WorldToScreenPoint(cam, abajoCentro);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxRT, pantalla, null, out local);
            rachaTxt.rectTransform.parent.GetComponent<RectTransform>().anchoredPosition = local + new Vector2(0f, -45f);
        }

        Color col = racha >= 8 ? new Color(1f, 0.35f, 0.6f) : (racha >= 5 ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.9f, 0.4f));
        rachaTxt.color = col;
        rachaTxt.text = "RACHA x" + racha;

        if (rachaCo != null) StopCoroutine(rachaCo);
        rachaCo = StartCoroutine(RachaPulso());
    }

    private IEnumerator RachaPulso()
    {
        RectTransform rt = rachaTxt.rectTransform;
        rachaTxt.alpha = 1f;
        float t = 0f;
        while (t < 0.28f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t / 0.28f));
            yield return null;
        }
        rt.localScale = Vector3.one;
        rachaCo = null;
    }

    private void OcultarRacha()
    {
        if (rachaCo != null) { StopCoroutine(rachaCo); rachaCo = null; }
        StartCoroutine(RachaSale());
    }

    private IEnumerator RachaSale()
    {
        float a0 = rachaTxt.alpha;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            rachaTxt.alpha = Mathf.Lerp(a0, 0f, Mathf.Clamp01(t / 0.25f));
            yield return null;
        }
        rachaTxt.alpha = 0f;
    }

    // ================================================================== ayudas
    private Vector2 LocalDesde(RectTransform origen)
    {
        Vector3[] esq = new Vector3[4];
        origen.GetWorldCorners(esq);
        Vector3 centro = (esq[0] + esq[2]) * 0.5f;
        Canvas c = origen.GetComponentInParent<Canvas>();
        Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? c.worldCamera : null;
        Vector2 pantalla = RectTransformUtility.WorldToScreenPoint(cam, centro);
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(fxRT, pantalla, null, out local);
        return local;
    }

    private void Sonido(AudioClip clip, float pitch)
    {
        if (clip == null) return;
        audioSrc.pitch = pitch;
        audioSrc.PlayOneShot(clip);
    }

    private static RectTransform NuevoRect(string nombre, Transform padre, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Image NuevaImagen(string nombre, Transform padre, Sprite sprite, Color color)
    {
        RectTransform rt = NuevoRect(nombre, padre, Vector2.zero, Vector2.one);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI Texto(string nombre, Transform padre, Vector2 min, Vector2 max, float tMin, float tMax,
                                  FontStyles estilo, Color color)
    {
        RectTransform rt = NuevoRect(nombre, padre, min, max);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.enableAutoSizing = true;
        t.fontSizeMin = tMin;
        t.fontSizeMax = tMax;
        t.fontStyle = estilo;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }
}

// Sprites suaves generados por codigo (sin necesitar imagenes en el proyecto)
internal static class Nivel2Sprites
{
    private static Sprite blob, vineta;

    // Mancha suave: opaca al centro, transparente en el borde
    public static Sprite Blob()
    {
        if (blob != null) return blob;
        const int n = 128;
        Color32[] col = new Color32[n * n];
        float mitad = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - mitad) / mitad;
                float dy = (y + 0.5f - mitad) / mitad;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                col[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        blob = Crear(col, n);
        return blob;
    }

    // Borde oscuro: transparente en el centro, opaco hacia las esquinas
    public static Sprite Vineta()
    {
        if (vineta != null) return vineta;
        const int n = 128;
        Color32[] col = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01((r - 0.45f) / 0.55f);
                float a = t * t * (3f - 2f * t);
                col[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        vineta = Crear(col, n);
        return vineta;
    }

    private static Sprite Crear(Color32[] col, int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(col);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
