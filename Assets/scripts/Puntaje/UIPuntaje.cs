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
        t.fontSizeMin = tMin;
        t.fontSizeMax = tMax;
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
            Image personal = Img(nombre, padre, min, max, Color.white, C.fondoPanel, true);
            personal.raycastTarget = true;   // tapa lo que hay detras
            return personal.rectTransform;
        }

        Image marco = Img(nombre + "_Marco", padre, min, max, C.colorBorde, RectRedondeado(), true);
        marco.raycastTarget = true;
        Image interior = Img(nombre, marco.transform, Vector2.zero, Vector2.one, C.colorPanel, RectRedondeado(), true);
        interior.rectTransform.offsetMin = new Vector2(grosor, grosor);
        interior.rectTransform.offsetMax = new Vector2(-grosor, -grosor);
        return interior.rectTransform;
    }

    // ================================================================== botones
    public static Button Boton(string nombre, string texto, Transform padre, Vector2 min, Vector2 max, TipoBoton tipo)
    {
        RectTransform rt = Rect(nombre, padre, min, max);
        Image img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = true;

        Color baseColor;
        Sprite personal;
        switch (tipo)
        {
            case TipoBoton.Secundario: baseColor = C.colorBotonSecundario; personal = C.fondoBotonSecundario; break;
            case TipoBoton.Advertencia: baseColor = C.colorBotonAdvertencia; personal = C.fondoBotonPrimario; break;
            default: baseColor = C.colorBotonPrimario; personal = C.fondoBotonPrimario; break;
        }

        Color colorTexto;
        if (personal != null)
        {
            img.sprite = personal;
            img.type = personal.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Color.white;                 // se ve tu imagen tal cual
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

    public static void SonarBoton()
    {
        Sonar(C.sonidoBoton, ProceduralSfx.Nota(3), 1.2f, 0.5f);
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
