using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum TipoBoton { Primario, Secundario, Advertencia }

// Constructores de interfaz compartidos por la pantalla de puntaje y el ranking.
// Todo respeta el estilo de PuntajeConfig (fuentes, colores, imagenes y sonidos propios).
public static class UIPuntaje
{
    private static PuntajeConfig C { get { return PuntajeConfig.Actual; } }
    public static PuntajeConfig Config { get { return PuntajeConfig.Actual; } }

    // Panel() devuelve el area interior; esto devuelve el objeto completo (marco incluido) para mostrarlo/ocultarlo
    public static GameObject RaizDe(RectTransform interior)
    {
        Transform p = interior.parent;
        return (p != null && p.name == interior.name + "_Marco") ? p.gameObject : interior.gameObject;
    }

    private static Sprite sRect, sCirculo, sEstrella;
    private static AudioSource audioSrc;

    // ================================================================== estructura
    public static RectTransform Rect(string nombre, Transform padre, Vector2 min, Vector2 max)
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

    public static Image Img(string nombre, Transform padre, Vector2 min, Vector2 max, Color color, Sprite sprite, bool sliced)
    {
        RectTransform rt = Rect(nombre, padre, min, max);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = (sliced && sprite.border.sqrMagnitude > 0f) ? Image.Type.Sliced : Image.Type.Simple;
        }
        return img;
    }

    public static TextMeshProUGUI Texto(string nombre, Transform padre, Vector2 min, Vector2 max, float tMin, float tMax,
                                        FontStyles estilo, TextAlignmentOptions alin, Color color, bool titulo = false)
    {
        RectTransform rt = Rect(nombre, padre, min, max);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = titulo ? C.fuenteTitulos : C.fuenteTextos;
        if (f == null) f = titulo ? C.fuenteTextos : C.fuenteTitulos;
        if (f != null) t.font = f;
        t.enableAutoSizing = true;
        float escala = Mathf.Max(0.1f, titulo ? C.escalaTitulos : C.escalaTextos);
        t.fontSizeMin = tMin * escala;
        t.fontSizeMax = tMax * escala;
        t.fontStyle = estilo;
        t.alignment = alin;
        t.color = color;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // Panel con borde. Devuelve el area interior donde se colocan los contenidos.
    public static RectTransform Panel(string nombre, Transform padre, Vector2 min, Vector2 max, float grosor = 6f)
    {
        if (C.fondoPanel != null)
        {
            if (!C.rellenoOpacoPaneles)
            {
                Image sinRelleno = Img(nombre, padre, min, max, Color.white, C.fondoPanel, true);
                sinRelleno.raycastTarget = true;   // tapa lo que hay detras
                return sinRelleno.rectTransform;
            }

            // El nombre "_Marco" hace que RaizDe() oculte todo junto (relleno + tu imagen)
            RectTransform raiz = Rect(nombre + "_Marco", padre, min, max);

            Image relleno = Img("Relleno", raiz, Vector2.zero, Vector2.one,
                                new Color(C.colorRellenoPanel.r, C.colorRellenoPanel.g, C.colorRellenoPanel.b, 1f),
                                FormaVentana(C.margenRellenoPanel), true);
            relleno.raycastTarget = true;   // tapa los toques de lo que hay detras
            relleno.rectTransform.offsetMin = new Vector2(C.margenRellenoPanel, C.margenRellenoPanel);
            relleno.rectTransform.offsetMax = new Vector2(-C.margenRellenoPanel, -C.margenRellenoPanel);

            Image personal = Img(nombre, raiz, Vector2.zero, Vector2.one, Color.white, C.fondoPanel, true);
            personal.raycastTarget = false;
            return personal.rectTransform;
        }

        Image marco = Img(nombre + "_Marco", padre, min, max, C.colorBorde, FormaVentana(0f), true);
        marco.raycastTarget = true;
        Image interior = Img(nombre, marco.transform, Vector2.zero, Vector2.one, C.colorPanel, FormaVentana(grosor), true);
        interior.rectTransform.offsetMin = new Vector2(grosor, grosor);
        interior.rectTransform.offsetMax = new Vector2(-grosor, -grosor);
        return interior.rectTransform;
    }

    // ================================================================== botones
    public static Button Boton(string nombre, string texto, Transform padre, Vector2 min, Vector2 max, TipoBoton tipo)
    {
        RectTransform rt = Rect(nombre, padre, min, max);
        rt.gameObject.AddComponent<GroxNoButtonSfx>();   // este boton ya hace su propio clic (SonarBoton)
        Image img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = true;

        Color baseColor;
        Sprite personal;
        switch (tipo)
        {
            case TipoBoton.Secundario: baseColor = C.colorBotonSecundario; personal = C.fondoBotonSecundario; break;
            case TipoBoton.Advertencia:
                baseColor = C.colorBotonAdvertencia;
                personal = C.fondoBotonAdvertencia != null ? C.fondoBotonAdvertencia : C.fondoBotonPrimario;
                break;
            default: baseColor = C.colorBotonPrimario; personal = C.fondoBotonPrimario; break;
        }

        Color colorTexto;
        if (personal != null)
        {
            img.sprite = personal;
            img.type = personal.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            // Tu imagen se ve tal cual (blanco = sin tinte). Cambia estos colores en PuntajeConfig si quieres teñirla.
            img.color = tipo == TipoBoton.Advertencia ? C.tinteImagenAdvertencia
                      : tipo == TipoBoton.Secundario ? C.tinteImagenSecundario : C.tinteImagenPrimario;
            colorTexto = C.colorTextoBoton;
        }
        else
        {
            img.sprite = RectRedondeado();
            img.type = Image.Type.Sliced;
            img.color = baseColor;
            float lum = 0.299f * baseColor.r + 0.587f * baseColor.g + 0.114f * baseColor.b;
            colorTexto = lum > 0.55f ? C.colorTextoBoton : Color.white;   // contraste automatico
        }

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
        b.colors = cb;
        b.onClick.AddListener(SonarBoton);

        TextMeshProUGUI t = Texto("Texto", rt, Vector2.zero, Vector2.one, 16f, 40f, FontStyles.Bold,
                                  TextAlignmentOptions.Center, colorTexto, true);
        t.rectTransform.offsetMin = new Vector2(10f, 4f);
        t.rectTransform.offsetMax = new Vector2(-10f, -4f);
        t.fontSizeMin = 16f * Mathf.Max(0.1f, C.escalaBotones);   // el texto del boton usa su propia escala
        t.fontSizeMax = 40f * Mathf.Max(0.1f, C.escalaBotones);
        t.text = texto;
        return b;
    }

    public static void PonerTexto(Button b, string texto)
    {
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.text = texto;
    }

    // Boton (X) para cerrar, arriba a la derecha del panel
    public static Button BotonCerrar(Transform padre)
    {
        RectTransform rt = Rect("Cerrar", padre, new Vector2(1f, 1f), new Vector2(1f, 1f));
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(84f, 84f);
        rt.anchoredPosition = new Vector2(-14f, -14f);
        rt.gameObject.AddComponent<GroxNoButtonSfx>();

        Image img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = true;
        if (C.fondoBotonCerrar != null)
        {
            img.sprite = C.fondoBotonCerrar;
            img.color = Color.white;
        }
        else
        {
            img.sprite = Circulo();
            img.color = C.colorError;
        }

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.selectedColor = Color.white;
        b.colors = cb;
        b.onClick.AddListener(SonarBoton);

        if (C.fondoBotonCerrar == null)
        {
            TextMeshProUGUI x = Texto("X", rt, Vector2.zero, Vector2.one, 20f, 60f, FontStyles.Bold,
                                      TextAlignmentOptions.Center, Color.white, true);
            x.text = "X";
        }
        return b;
    }

    // ================================================================== sliders, casillas e iconos
    public static Slider CrearSlider(string nombre, Transform padre, Vector2 min, Vector2 max, float vmin, float vmax, float valor)
    {
        RectTransform rt = Rect(nombre, padre, min, max);

        // Toda la fila recibe el toque
        Image toque = rt.gameObject.AddComponent<Image>();
        toque.color = new Color(0f, 0f, 0f, 0f);
        toque.raycastTarget = true;
        Slider s = rt.gameObject.AddComponent<Slider>();

        bool propioFondo = C.fondoSlider != null;
        Image fondo = Img("Fondo", rt, new Vector2(0f, 0.32f), new Vector2(1f, 0.68f),
                          propioFondo ? Color.white : new Color(1f, 1f, 1f, 0.2f),
                          propioFondo ? C.fondoSlider : RectRedondeado(), true);

        RectTransform areaRelleno = Rect("AreaRelleno", rt, new Vector2(0f, 0.32f), new Vector2(1f, 0.68f));
        areaRelleno.offsetMin = new Vector2(26f, 0f);
        areaRelleno.offsetMax = new Vector2(-26f, 0f);
        bool propioRelleno = C.rellenoSlider != null;
        Image relleno = Img("Relleno", areaRelleno, Vector2.zero, new Vector2(0f, 1f),
                            propioRelleno ? Color.white : C.colorBorde, propioRelleno ? C.rellenoSlider : null, true);
        relleno.rectTransform.anchorMin = new Vector2(0f, 0f);
        relleno.rectTransform.anchorMax = new Vector2(0f, 1f);
        relleno.rectTransform.sizeDelta = new Vector2(10f, 0f);

        RectTransform areaPerilla = Rect("AreaPerilla", rt, Vector2.zero, Vector2.one);
        areaPerilla.offsetMin = new Vector2(26f, 0f);
        areaPerilla.offsetMax = new Vector2(-26f, 0f);
        bool propiaPerilla = C.perillaSlider != null;
        Image perilla = Img("Perilla", areaPerilla, Vector2.zero, new Vector2(0f, 1f),
                            propiaPerilla ? Color.white : Color.white, propiaPerilla ? C.perillaSlider : Circulo(), false);
        perilla.rectTransform.anchorMin = new Vector2(0f, 0.12f);
        perilla.rectTransform.anchorMax = new Vector2(0f, 0.88f);
        perilla.rectTransform.sizeDelta = new Vector2(54f, 0f);
        perilla.preserveAspect = true;
        perilla.raycastTarget = true;

        s.fillRect = relleno.rectTransform;
        s.handleRect = perilla.rectTransform;
        s.targetGraphic = perilla;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = vmin;
        s.maxValue = vmax;
        s.wholeNumbers = false;
        s.SetValueWithoutNotify(valor);
        return s;
    }

    public static Toggle CrearCasilla(string nombre, Transform padre, Vector2 min, Vector2 max, bool valor)
    {
        RectTransform rt = Rect(nombre, padre, min, max);
        rt.gameObject.AddComponent<GroxNoButtonSfx>();
        Image caja = rt.gameObject.AddComponent<Image>();
        bool propia = C.fondoCasilla != null;
        caja.sprite = propia ? C.fondoCasilla : RectRedondeado();
        caja.type = (caja.sprite != null && caja.sprite.border.sqrMagnitude > 0f) ? Image.Type.Sliced : Image.Type.Simple;
        caja.color = propia ? Color.white : new Color(1f, 1f, 1f, 0.2f);
        caja.raycastTarget = true;

        bool propiaMarca = C.marcaCasilla != null;
        Image marca = Img("Marca", rt, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f),
                          propiaMarca ? Color.white : C.colorBorde, propiaMarca ? C.marcaCasilla : RectRedondeado(), !propiaMarca);

        Toggle t = rt.gameObject.AddComponent<Toggle>();
        t.targetGraphic = caja;
        t.graphic = marca;
        t.SetIsOnWithoutNotify(valor);
        t.onValueChanged.AddListener(v => SonarBoton());
        return t;
    }

    // Boton cuadrado con un icono adentro (ej. altavoz para silenciar)
    public static Button BotonIcono(string nombre, Transform padre, Vector2 min, Vector2 max, out Image icono)
    {
        RectTransform rt = Rect(nombre, padre, min, max);
        rt.gameObject.AddComponent<GroxNoButtonSfx>();
        Image fondo = rt.gameObject.AddComponent<Image>();
        fondo.sprite = RectRedondeado();
        fondo.type = Image.Type.Sliced;
        fondo.color = new Color(1f, 1f, 1f, 0.16f);
        fondo.raycastTarget = true;

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = fondo;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cb.selectedColor = Color.white;
        b.colors = cb;
        b.onClick.AddListener(SonarBoton);

        icono = Img("Icono", rt, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f), Color.white, null, false);
        icono.preserveAspect = true;
        return b;
    }

    public static void PonerIconoAltavoz(Image img, bool activo)
    {
        Sprite propio = activo ? C.iconoAltavozActivo : C.iconoAltavozSilenciado;
        if (propio != null) { img.sprite = propio; img.color = Color.white; }
        else
        {
            img.sprite = activo ? AltavozActivo() : AltavozSilenciado();
            img.color = activo ? Color.white : new Color(1f, 0.45f, 0.45f, 1f);
        }
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    private static Sprite sAltavozOn, sAltavozOff;

    private static Sprite AltavozActivo()
    {
        if (sAltavozOn == null) sAltavozOn = DibujarAltavoz(true);
        return sAltavozOn;
    }

    private static Sprite AltavozSilenciado()
    {
        if (sAltavozOff == null) sAltavozOff = DibujarAltavoz(false);
        return sAltavozOff;
    }

    // Altavoz dibujado por codigo: cuerpo + cono, y ondas (activo) o una X (silenciado)
    private static Sprite DibujarAltavoz(bool activo)
    {
        const int n = 128;
        Color32[] col = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        Vector2 p = new Vector2((x + (sx + 0.5f) / 2f) / n, (y + (sy + 0.5f) / 2f) / n);
                        if (PuntoAltavoz(p, activo)) hits++;
                    }
                col[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
            }
        return Crear(col, n, Vector4.zero);
    }

    private static bool PuntoAltavoz(Vector2 p, bool activo)
    {
        // cuerpo
        if (p.x >= 0.10f && p.x <= 0.28f && p.y >= 0.38f && p.y <= 0.62f) return true;
        // cono (trapecio)
        if (p.x > 0.28f && p.x <= 0.50f)
        {
            float t = (p.x - 0.28f) / 0.22f;
            float mitad = Mathf.Lerp(0.12f, 0.30f, t);
            if (Mathf.Abs(p.y - 0.5f) <= mitad) return true;
        }

        if (activo)
        {
            Vector2 d = p - new Vector2(0.50f, 0.5f);
            float r = d.magnitude;
            float ang = Mathf.Abs(Mathf.Atan2(d.y, d.x)) * Mathf.Rad2Deg;
            if (d.x > 0f && ang < 48f)
            {
                if (Mathf.Abs(r - 0.22f) < 0.03f) return true;
                if (Mathf.Abs(r - 0.36f) < 0.03f) return true;
            }
        }
        else
        {
            if (DistSegmento(p, new Vector2(0.62f, 0.34f), new Vector2(0.90f, 0.66f)) < 0.035f) return true;
            if (DistSegmento(p, new Vector2(0.62f, 0.66f), new Vector2(0.90f, 0.34f)) < 0.035f) return true;
        }
        return false;
    }

    private static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    // ================================================================== estrellas
    public static void PonerEstrella(Image img, bool llena)
    {
        if (llena)
        {
            if (C.estrellaLlena != null) { img.sprite = C.estrellaLlena; img.color = Color.white; }
            else { img.sprite = Estrella(); img.color = C.colorOro; }
        }
        else
        {
            if (C.estrellaVacia != null) { img.sprite = C.estrellaVacia; img.color = Color.white; }
            else { img.sprite = Estrella(); img.color = new Color(0.28f, 0.3f, 0.4f, 1f); }
        }
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    // ================================================================== sonido
    public static void Sonar(AudioClip propio, AudioClip generado, float pitch = 1f, float vol = 1f)
    {
        AudioClip clip = propio != null ? propio : generado;
        if (clip == null) return;

        if (audioSrc == null)
        {
            GameObject go = new GameObject("SfxPuntaje");
            audioSrc = go.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0f;
        }
        audioSrc.pitch = pitch;
        audioSrc.PlayOneShot(clip, vol);
    }

    // Clic de los botones. Orden: tu sonido en PuntajeConfig.sonidoBoton > el sonido UiClick de GroxAudioConfig > clic generado.
    public static void SonarBoton()
    {
        if (C.sonidoBoton != null) { Sonar(C.sonidoBoton, null, 1f, 0.5f); return; }
        if (GroxAudio.Instance != null) { GroxAudio.Sfx(GroxSfx.UiClick); return; }
        Sonar(null, ProceduralSfx.Nota(3), 1.2f, 0.5f);
    }

    // ================================================================== forma de las ventanas
    // Forma de TODAS las ventanas (pausa, opciones, puntaje, confirmacion) cuando no usas imagen propia.
    // Se cambia en PuntajeConfig > "Forma de las ventanas". 'inset' = lo que se mete hacia adentro
    // (para el interior del borde), asi el grosor del borde queda parejo tambien en las diagonales.
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> formas =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    public static Sprite FormaVentana(float inset)
    {
        PuntajeConfig c = C;
        if (c.esquinasRedondeadas) return RectRedondeado();

        // Una diagonal metida 'inset' px hacia adentro necesita un corte (2 - raiz2) * inset mas chico
        float corte = Mathf.Max(0f, c.tamanoCorte - (2f - Mathf.Sqrt(2f)) * inset);
        int k = Mathf.RoundToInt(corte);
        bool tl = c.cortarArribaIzquierda, tr = c.cortarArribaDerecha;
        bool bl = c.cortarAbajoIzquierda, br = c.cortarAbajoDerecha;

        string clave = k + "|" + tl + tr + bl + br;
        Sprite hecho;
        if (formas.TryGetValue(clave, out hecho) && hecho != null) return hecho;

        int n = Mathf.Max(16, 2 * k + 8);
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float xl = x + 0.5f;            // distancia al borde izquierdo
                float xr = n - (x + 0.5f);      // ... derecho
                float yb = y + 0.5f;            // ... inferior (en Unity y=0 es abajo)
                float yt = n - (y + 0.5f);      // ... superior
                float a = 1f;
                if (tl) a = Mathf.Min(a, Mathf.Clamp01((xl + yt - k) / 1.4142f + 0.5f));
                if (tr) a = Mathf.Min(a, Mathf.Clamp01((xr + yt - k) / 1.4142f + 0.5f));
                if (bl) a = Mathf.Min(a, Mathf.Clamp01((xl + yb - k) / 1.4142f + 0.5f));
                if (br) a = Mathf.Min(a, Mathf.Clamp01((xr + yb - k) / 1.4142f + 0.5f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

        Sprite sp = Crear(px, n, new Vector4(k, k, k, k));
        formas[clave] = sp;
        return sp;
    }

    // ================================================================== sprites dibujados por codigo
    public static Sprite RectRedondeado()
    {
        if (sRect != null) return sRect;
        const int n = 64;
        const float radio = 20f;
        float m = n * 0.5f;
        Color32[] c = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - m) - (m - radio);
                float qy = Mathf.Abs(y + 0.5f - m) - (m - radio);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radio;
                c[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
        sRect = Crear(c, n, new Vector4(radio, radio, radio, radio));
        return sRect;
    }

    public static Sprite Circulo()
    {
        if (sCirculo != null) return sCirculo;
        const int n = 128;
        float m = n * 0.5f;
        Color32[] c = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - m, dy = y + 0.5f - m;
                float a = Mathf.Clamp01(m - 1f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                c[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        sCirculo = Crear(c, n, Vector4.zero);
        return sCirculo;
    }

    public static Sprite Estrella()
    {
        if (sEstrella != null) return sEstrella;

        const int n = 128;
        Vector2[] poly = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
            float r = (i % 2 == 0) ? 0.47f : 0.2f;
            poly[i] = new Vector2(0.5f + r * Mathf.Cos(ang), 0.5f + r * Mathf.Sin(ang));
        }

        Color32[] col = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int aciertos = 0;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                        if (Dentro(poly, new Vector2((x + (sx + 0.5f) / 3f) / n, (y + (sy + 0.5f) / 3f) / n))) aciertos++;
                col[y * n + x] = new Color32(255, 255, 255, (byte)(aciertos * 255 / 9));
            }
        sEstrella = Crear(col, n, Vector4.zero);
        return sEstrella;
    }

    private static bool Dentro(Vector2[] poly, Vector2 p)
    {
        bool dentro = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                dentro = !dentro;
        }
        return dentro;
    }

    private static Sprite Crear(Color32[] c, int n, Vector4 borde)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, borde);
    }
}
