using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EstadoPuntero { Neutral, EnRango, FueraDeRango, SinEnergia }

// Interfaz del Nivel 1 (se construye por codigo): barra de contaminacion, barra de energia,
// combo, puntero con indicador de alcance, mensajes y textos flotantes.
public class Nivel1HUD : MonoBehaviour
{
    [Header("Posiciones: ancla (0 a 1) + desplazamiento en pixeles sobre 1920x1080")]
    public Vector2 anclaContaminacion = new Vector2(0.5f, 1f);
    public Vector2 offsetContaminacion = new Vector2(0f, -175f);
    public Vector2 anclaEnergia = new Vector2(0.5f, 0f);
    public Vector2 offsetEnergia = new Vector2(0f, 75f);
    public Vector2 anclaCombo = new Vector2(1f, 0.5f);
    public Vector2 offsetCombo = new Vector2(-230f, 150f);

    [Header("Puntero")]
    [Tooltip("Si ya tienes una mira en tu Canvas, arrastrala aqui y se usara esa (cambia de color y tamano). Vacio = se crea una.")]
    public Graphic punteroExistente;

    [Header("Aspecto")]
    public Color colorPanel = new Color32(24, 28, 52, 230);
    public Color colorBorde = new Color32(46, 230, 166, 255);
    public Color colorOro = new Color32(255, 214, 10, 255);
    public TMP_FontAsset fuente;

    private class Barra
    {
        public RectTransform raiz, relleno;
        public Image fondo, rellenoImg;
        public TextMeshProUGUI etiqueta;
    }

    private RectTransform hudRT, fondoRT;
    private Barra barraCont, barraEner;
    private Image vinetaImg, alertaImg;
    private RectTransform comboRT, comboTimer;
    private TextMeshProUGUI comboTxt, multTxt, toastTxt, lejosTxt;
    private CanvasGroup comboGroup, toastGroup;
    private RectTransform punteroRT, ringRT, dotRT, equisRT;
    private Image ringImg, dotImg, equisA, equisB;
    private Coroutine toastCo;

    private float contPct, contVisible, energiaPct = 1f;
    private float punchCont, punchEner, punchCombo, flashSinEnergia, flashRecarga;
    private EstadoPuntero estadoPuntero = EstadoPuntero.Neutral;
    private bool comboVisible;
    private float alertaPulso;

    private void Awake()
    {
        Construir();
    }

    // ================================================================== API (la usa Nivel1Sistema)
    public void SetContaminacion(float pct01, float visible01)
    {
        contPct = pct01;
        contVisible = visible01;
    }

    public void PunchContaminacion() { punchCont = 1f; }

    public void SetEnergia(float pct01) { energiaPct = pct01; }
    public void PunchEnergia() { punchEner = 1f; }
    public void FlashSinEnergia() { flashSinEnergia = 1f; }
    public void FlashRecarga() { flashRecarga = 1f; punchEner = 1f; }

    public void SetCombo(int combo, float multiplicador, float tiempo01)
    {
        bool mostrar = combo >= 2;
        comboVisible = mostrar;
        if (!mostrar) return;

        comboTxt.text = "COMBO x" + combo;
        multTxt.text = multiplicador > 1.001f ? "PUNTOS x" + multiplicador.ToString("0.##") : "¡sigue así!";
        comboTimer.anchorMax = new Vector2(Mathf.Clamp01(tiempo01), 1f);
    }

    public void ComboSube() { punchCombo = 1f; }

    public void SetPuntero(EstadoPuntero estado) { estadoPuntero = estado; }

    public void SetAmbiente(float alfaVineta, float alerta)
    {
        Color c = vinetaImg.color;
        c.a = alfaVineta;
        vinetaImg.color = c;
        alertaPulso = alerta;
    }

    public void SetColorAmbiente(Color color)
    {
        Color c = color;
        c.a = vinetaImg.color.a;
        vinetaImg.color = c;
    }

    public void Toast(string texto, Color color)
    {
        if (toastCo != null) StopCoroutine(toastCo);
        toastCo = StartCoroutine(ToastRutina(texto, color));
    }

    public void TextoMundo(string texto, Vector3 mundo, Color color)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(mundo);
        if (sp.z <= 0f) return;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRT, sp, null, out local);

        RectTransform rt = NuevoRect("Flotante", hudRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rt.sizeDelta = new Vector2(420f, 130f);
        rt.anchoredPosition = local;
        TextMeshProUGUI t = Texto("Texto", rt, Vector2.zero, Vector2.one, 24f, 70f, FontStyles.Bold, color);
        t.outlineWidth = 0.25f;
        t.outlineColor = new Color32(10, 10, 30, 255);
        t.text = texto;
        StartCoroutine(Flotar(rt, t));
    }

    // ================================================================== animacion por frame
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        punchCont = Mathf.Max(0f, punchCont - dt * 4f);
        punchEner = Mathf.Max(0f, punchEner - dt * 4f);
        punchCombo = Mathf.Max(0f, punchCombo - dt * 5f);
        flashSinEnergia = Mathf.Max(0f, flashSinEnergia - dt * 3f);
        flashRecarga = Mathf.Max(0f, flashRecarga - dt * 2.5f);

        ActualizarContaminacion();
        ActualizarEnergia();
        ActualizarCombo(dt);
        ActualizarPuntero(dt);

        Color a = alertaImg.color;
        a.a = alertaPulso * (0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 7f));
        alertaImg.color = a;
    }

    private void ActualizarContaminacion()
    {
        float p = contVisible;
        Color col = ColorContaminacion(p);
        barraCont.rellenoImg.color = col;
        barraCont.relleno.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
        barraCont.etiqueta.text = "CONTAMINACIÓN " + Mathf.RoundToInt(contPct * 100f) + "%";

        float latido = 0f;
        if (contPct >= 0.9f) latido = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
        else if (contPct >= 0.75f) latido = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

        barraCont.raiz.localScale = Vector3.one * (1f + 0.07f * punchCont + 0.03f * latido);
        Color fondo = Color.Lerp(colorPanel, new Color(0.6f, 0.05f, 0.05f, 0.95f), latido * 0.8f);
        barraCont.fondo.color = fondo;
    }

    private void ActualizarEnergia()
    {
        Color normal = new Color(0.2f, 0.85f, 1f);
        Color col = normal;
        if (energiaPct < 0.25f)
        {
            float parpadeo = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            col = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.2f, 0.2f), parpadeo);
        }
        col = Color.Lerp(col, Color.white, flashRecarga);
        if (flashSinEnergia > 0f) col = Color.Lerp(col, new Color(1f, 0.2f, 0.2f), flashSinEnergia);

        barraEner.rellenoImg.color = col;
        barraEner.relleno.anchorMax = new Vector2(Mathf.Clamp01(energiaPct), 1f);
        barraEner.etiqueta.text = energiaPct <= 0.001f ? "SIN ENERGÍA" : "ENERGÍA " + Mathf.RoundToInt(energiaPct * 100f) + "%";
        barraEner.raiz.localScale = Vector3.one * (1f + 0.08f * punchEner + 0.05f * flashSinEnergia);
    }

    private void ActualizarCombo(float dt)
    {
        float objetivo = comboVisible ? 1f : 0f;
        comboGroup.alpha = Mathf.MoveTowards(comboGroup.alpha, objetivo, dt * 6f);
        comboRT.localScale = Vector3.one * (1f + 0.35f * punchCombo);
    }

    private void ActualizarPuntero(float dt)
    {
        Color objetivo;
        float escala;
        bool mostrarX = false;

        switch (estadoPuntero)
        {
            case EstadoPuntero.EnRango:
                objetivo = new Color(0.35f, 1f, 0.5f, 1f);
                escala = 0.72f + 0.05f * Mathf.Sin(Time.unscaledTime * 12f);   // se "cierra" sobre el objetivo
                break;
            case EstadoPuntero.FueraDeRango:
                objetivo = new Color(1f, 0.55f, 0.2f, 0.95f);
                escala = 1.18f;
                mostrarX = true;
                break;
            case EstadoPuntero.SinEnergia:
                objetivo = new Color(0.6f, 0.6f, 0.65f, 0.45f);
                escala = 1f;
                break;
            default:
                objetivo = new Color(1f, 1f, 1f, 0.65f);
                escala = 1f;
                break;
        }

        float k = 1f - Mathf.Exp(-16f * dt);

        if (punteroExistente != null)
        {
            punteroExistente.color = Color.Lerp(punteroExistente.color, objetivo, k);
            punteroExistente.rectTransform.localScale = Vector3.Lerp(punteroExistente.rectTransform.localScale, Vector3.one * escala, k);
        }
        else
        {
            ringImg.color = Color.Lerp(ringImg.color, objetivo, k);
            dotImg.color = Color.Lerp(dotImg.color, objetivo, k);
            ringRT.localScale = Vector3.Lerp(ringRT.localScale, Vector3.one * escala, k);
            dotRT.localScale = Vector3.Lerp(dotRT.localScale, Vector3.one * (estadoPuntero == EstadoPuntero.EnRango ? 1.5f : 1f), k);

            Color x = equisA.color;
            x.r = objetivo.r; x.g = objetivo.g; x.b = objetivo.b;
            x.a = Mathf.MoveTowards(x.a, mostrarX ? 1f : 0f, dt * 10f);
            equisA.color = x;
            equisB.color = x;
        }

        Color l = lejosTxt.color;
        l.a = Mathf.MoveTowards(l.a, mostrarX ? 1f : 0f, dt * 8f);
        lejosTxt.color = l;
    }

    private static Color ColorContaminacion(float p)
    {
        Color verde = new Color(0.3f, 0.9f, 0.4f);
        Color amarillo = new Color(1f, 0.85f, 0.2f);
        Color naranja = new Color(1f, 0.5f, 0.15f);
        Color rojo = new Color(0.95f, 0.2f, 0.2f);
        if (p < 0.33f) return Color.Lerp(verde, amarillo, p / 0.33f);
        if (p < 0.66f) return Color.Lerp(amarillo, naranja, (p - 0.33f) / 0.33f);
        return Color.Lerp(naranja, rojo, (p - 0.66f) / 0.34f);
    }

    // ================================================================== animaciones con corutina
    private IEnumerator ToastRutina(string texto, Color color)
    {
        toastTxt.text = texto;
        toastTxt.color = color;
        RectTransform rt = toastTxt.rectTransform;
        toastGroup.alpha = 1f;

        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.08f, Mathf.Clamp01(t / 0.22f));
            yield return null;
        }
        rt.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(1.1f);

        t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - Mathf.Clamp01(t / 0.3f);
            yield return null;
        }
        toastGroup.alpha = 0f;
        toastCo = null;
    }

    private IEnumerator Flotar(RectTransform rt, TextMeshProUGUI t)
    {
        const float dur = 1f;
        Vector2 ini = rt.anchoredPosition;
        float tt = 0f;
        while (tt < dur && rt != null)
        {
            tt += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(tt / dur);
            rt.anchoredPosition = ini + new Vector2(0f, 150f * (1f - Mathf.Pow(1f - k, 2f)));
            rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f));
            t.alpha = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    // ================================================================== construccion
    private void Construir()
    {
        // Canvas de fondo (niebla en los bordes): por debajo de tu HUD
        fondoRT = CrearCanvas("Nivel1FX_Fondo", -50);
        vinetaImg = Img("Vineta", fondoRT, Vector2.zero, Vector2.one, new Color(0.35f, 0.4f, 0.2f, 0f), Nivel1Fx.Vineta());
        alertaImg = Img("AlertaRoja", fondoRT, Vector2.zero, Vector2.one, new Color(0.9f, 0.1f, 0.1f, 0f), Nivel1Fx.Vineta());

        // Canvas del HUD
        hudRT = CrearCanvas("Nivel1HUD_Canvas", 120);

        barraCont = NuevaBarra("Contaminacion", anclaContaminacion, offsetContaminacion, new Vector2(560f, 66f));
        barraEner = NuevaBarra("Energia", anclaEnergia, offsetEnergia, new Vector2(560f, 54f));

        ConstruirCombo();
        ConstruirPuntero();
        ConstruirToast();
    }

    private RectTransform CrearCanvas(string nombre, int orden)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = orden;
        CanvasScaler sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        return (RectTransform)go.transform;
    }

    private Barra NuevaBarra(string nombre, Vector2 ancla, Vector2 offset, Vector2 tam)
    {
        Barra b = new Barra();
        b.raiz = NuevoRect(nombre, hudRT, ancla, ancla);
        b.raiz.sizeDelta = tam;
        b.raiz.anchoredPosition = offset;

        // Marco (borde de color) + fondo
        Image marco = Img("Marco", b.raiz, Vector2.zero, Vector2.one, colorBorde, Nivel1Fx.Blob());
        marco.sprite = RectRedondeado();
        marco.type = Image.Type.Sliced;

        b.fondo = Img("Fondo", b.raiz, Vector2.zero, Vector2.one, colorPanel, RectRedondeado());
        b.fondo.type = Image.Type.Sliced;
        b.fondo.rectTransform.offsetMin = new Vector2(4f, 4f);
        b.fondo.rectTransform.offsetMax = new Vector2(-4f, -4f);

        RectTransform zona = NuevoRect("Zona", b.raiz, Vector2.zero, Vector2.one);
        zona.offsetMin = new Vector2(10f, 10f);
        zona.offsetMax = new Vector2(-10f, -10f);

        b.rellenoImg = Img("Relleno", zona, Vector2.zero, Vector2.one, Color.green, null);
        b.relleno = b.rellenoImg.rectTransform;
        b.relleno.anchorMax = new Vector2(0f, 1f);

        b.etiqueta = Texto("Etiqueta", b.raiz, Vector2.zero, Vector2.one, 16f, 32f, FontStyles.Bold, Color.white);
        b.etiqueta.outlineWidth = 0.22f;
        b.etiqueta.outlineColor = new Color32(10, 10, 30, 255);
        return b;
    }

    private void ConstruirCombo()
    {
        comboRT = NuevoRect("Combo", hudRT, anclaCombo, anclaCombo);
        comboRT.sizeDelta = new Vector2(440f, 170f);
        comboRT.anchoredPosition = offsetCombo;
        comboGroup = comboRT.gameObject.AddComponent<CanvasGroup>();
        comboGroup.alpha = 0f;
        comboGroup.blocksRaycasts = false;

        comboTxt = Texto("Combo", comboRT, new Vector2(0f, 0.45f), new Vector2(1f, 1f), 36f, 80f, FontStyles.Bold, colorOro);
        comboTxt.outlineWidth = 0.25f;
        comboTxt.outlineColor = new Color32(10, 10, 30, 255);
        multTxt = Texto("Mult", comboRT, new Vector2(0f, 0.15f), new Vector2(1f, 0.47f), 22f, 40f, FontStyles.Bold, Color.white);
        multTxt.outlineWidth = 0.22f;
        multTxt.outlineColor = new Color32(10, 10, 30, 255);

        RectTransform barra = NuevoRect("Tiempo", comboRT, new Vector2(0.12f, 0.02f), new Vector2(0.88f, 0.11f));
        Img("Fondo", barra, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.2f), null);
        comboTimer = Img("Relleno", barra, Vector2.zero, Vector2.one, colorOro, null).rectTransform;
    }

    private void ConstruirPuntero()
    {
        punteroRT = NuevoRect("Puntero", hudRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        punteroRT.sizeDelta = new Vector2(120f, 120f);

        lejosTxt = Texto("Lejos", punteroRT, new Vector2(-1f, -0.7f), new Vector2(2f, -0.2f), 20f, 34f,
                         FontStyles.Bold, new Color(1f, 0.55f, 0.2f, 0f));
        lejosTxt.outlineWidth = 0.25f;
        lejosTxt.outlineColor = new Color32(10, 10, 30, 255);
        lejosTxt.text = "MUY LEJOS: ACÉRCATE";

        if (punteroExistente != null) return;   // usa la mira que ya tienes

        ringImg = Img("Anillo", punteroRT, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.65f), Nivel1Fx.Anillo());
        ringRT = ringImg.rectTransform;

        dotImg = Img("Punto", punteroRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 1f, 1f, 0.65f), Nivel1Fx.Disco());
        dotRT = dotImg.rectTransform;
        dotRT.sizeDelta = new Vector2(10f, 10f);

        equisA = Img("EquisA", punteroRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 0.55f, 0.2f, 0f), null);
        equisA.rectTransform.sizeDelta = new Vector2(5f, 44f);
        equisA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        equisB = Img("EquisB", punteroRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 0.55f, 0.2f, 0f), null);
        equisB.rectTransform.sizeDelta = new Vector2(5f, 44f);
        equisB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        equisRT = equisA.rectTransform;
    }

    private void ConstruirToast()
    {
        RectTransform r = NuevoRect("Toast", hudRT, new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.8f));
        toastGroup = r.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        toastTxt = Texto("Texto", r, Vector2.zero, Vector2.one, 30f, 80f, FontStyles.Bold, Color.white);
        toastTxt.outlineWidth = 0.28f;
        toastTxt.outlineColor = new Color32(10, 10, 30, 255);
    }

    // ------------------------------------------------------------------ ayudas
    private static Sprite rectRedondeado;
    private static Sprite RectRedondeado()
    {
        if (rectRedondeado != null) return rectRedondeado;
        const int n = 64;
        const float radio = 20f;
        float m = n * 0.5f;
        Color32[] c = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - m) - (m - radio);
                float qy = Mathf.Abs(y + 0.5f - m) - (m - radio);
                float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                          + Mathf.Min(Mathf.Max(qx, qy), 0f) - radio;
                c[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(c);
        tex.Apply();
        rectRedondeado = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                                       SpriteMeshType.FullRect, new Vector4(radio, radio, radio, radio));
        return rectRedondeado;
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

    private static Image Img(string nombre, Transform padre, Vector2 min, Vector2 max, Color color, Sprite sprite)
    {
        RectTransform rt = NuevoRect(nombre, padre, min, max);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null) img.sprite = sprite;
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
