using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Feedback de un contenedor (imagen del Canvas): rebote y brillo al acertar,
// temblor y destello rojo al fallar, y "pista" parpadeante en el contenedor correcto.
// ResidueLevelManager lo agrega solo a cada contenedor.
public class ContainerFeedback : MonoBehaviour
{
    public Color colorAcierto = new Color(0.25f, 0.9f, 0.45f);
    public Color colorError = new Color(1f, 0.25f, 0.25f);
    [Header("Sonidos opcionales (si no, se generan solos)")]
    public AudioClip sonidoAcierto;
    public AudioClip sonidoError;

    // Lo pone el manager: el contenedor se ilumina suavemente mientras el jugador se inclina hacia el
    [HideInInspector] public bool previsualizando;

    public RectTransform Rect { get; private set; }

    private Graphic grafico;
    private RectTransform halo;
    private Image haloImg;
    private AudioSource fuenteAudio;

    private Vector3 escalaBase;
    private Vector2 posBase;
    private Color colorBase;

    private Coroutine anim, pista;
    private float preview;
    private float haloAlphaAnim;
    private Color haloColor;

    private void Awake()
    {
        Rect = (RectTransform)transform;
        grafico = GetComponent<Graphic>();
        escalaBase = Rect.localScale;
        posBase = Rect.anchoredPosition;
        if (grafico != null) colorBase = grafico.color;

        fuenteAudio = gameObject.AddComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.spatialBlend = 0f;

        CrearHalo();
    }

    private void CrearHalo()
    {
        GameObject go = new GameObject("HaloContenedor", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        halo = (RectTransform)go.transform;
        halo.SetParent(Rect.parent, false);
        halo.anchorMin = Rect.anchorMin;
        halo.anchorMax = Rect.anchorMax;
        halo.pivot = Rect.pivot;
        halo.sizeDelta = Rect.sizeDelta;
        halo.anchoredPosition = Rect.anchoredPosition;
        halo.SetSiblingIndex(Rect.GetSiblingIndex());   // justo detras del contenedor

        haloImg = go.GetComponent<Image>();
        haloImg.sprite = Nivel2Sprites.Blob();
        haloImg.raycastTarget = false;
        haloImg.color = new Color(1f, 1f, 1f, 0f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        preview = Mathf.MoveTowards(preview, previsualizando ? 1f : 0f, dt * 8f);

        float pulso = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 10f);
        float alfa = Mathf.Max(haloAlphaAnim, preview * 0.55f * pulso);
        Color c = haloAlphaAnim > 0.02f ? haloColor : new Color(1f, 0.95f, 0.6f);
        c.a = alfa;
        haloImg.color = c;

        halo.anchoredPosition = Rect.anchoredPosition;
        halo.localScale = Rect.localScale * 1.9f;
    }

    private void OnEnable()
    {
        if (haloImg != null) haloImg.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        Restaurar();
        if (haloImg != null) haloImg.gameObject.SetActive(false);   // contenedor oculto: sin resplandor
    }

    // ------------------------------------------------------------------ API
    public void Acierto(int racha)
    {
        Detener();
        Sonar(sonidoAcierto != null ? sonidoAcierto : ProceduralSfx.Ding(), 1f + 0.06f * Mathf.Min(racha, 6));
        anim = StartCoroutine(RutinaAcierto());
    }

    public void Error()
    {
        Detener();
        Sonar(sonidoError != null ? sonidoError : ProceduralSfx.Error(), 1f);
        anim = StartCoroutine(RutinaError());
    }

    // Parpadeo verde: "este era el contenedor correcto"
    public void Pista()
    {
        if (pista != null) StopCoroutine(pista);
        pista = StartCoroutine(RutinaPista());
    }

    // ------------------------------------------------------------------ animaciones
    private IEnumerator RutinaAcierto()
    {
        const float dur = 0.38f;
        float t = 0f;
        haloColor = colorAcierto;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float onda = Mathf.Sin(k * Mathf.PI);
            Rect.localScale = escalaBase * (1f + 0.22f * onda);
            haloAlphaAnim = 0.9f * (1f - k);
            if (grafico != null) grafico.color = Color.Lerp(colorBase, colorAcierto, onda * 0.55f);
            yield return null;
        }
        Restaurar();
    }

    private IEnumerator RutinaError()
    {
        const float dur = 0.45f;
        float t = 0f;
        haloColor = colorError;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float temblor = Mathf.Sin(t * 55f) * 22f * (1f - k);
            Rect.anchoredPosition = posBase + new Vector2(temblor, 0f);
            haloAlphaAnim = 0.8f * (1f - k);
            if (grafico != null) grafico.color = Color.Lerp(colorBase, colorError, 0.65f * (1f - k));
            yield return null;
        }
        Restaurar();
    }

    private IEnumerator RutinaPista()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        const float dur = 0.8f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            haloColor = colorAcierto;
            haloAlphaAnim = 0.9f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k * 0.3f);
            yield return null;
        }
        haloAlphaAnim = 0f;
        pista = null;
    }

    // ------------------------------------------------------------------ utilidades
    private void Detener()
    {
        if (anim != null) { StopCoroutine(anim); anim = null; }
        Restaurar();
    }

    private void Restaurar()
    {
        if (Rect == null) return;
        Rect.localScale = escalaBase;
        Rect.anchoredPosition = posBase;
        if (grafico != null) grafico.color = colorBase;
        haloAlphaAnim = 0f;
    }

    private void Sonar(AudioClip clip, float pitch)
    {
        if (clip == null || fuenteAudio == null) return;
        fuenteAudio.pitch = pitch;   // cada contenedor tiene su propia fuente: no afecta a los demas
        fuenteAudio.PlayOneShot(clip);
    }
}
