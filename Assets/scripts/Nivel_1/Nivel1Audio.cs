using UnityEngine;

public enum SonidoN1
{
    Disparo, Captura, Fallo, FueraDeRango, SinEnergia, Recarga,
    Suelo, Alerta, Combo, ComboRoto, OrbeAparece
}

// Sonidos del Nivel 1. Cada evento usa TU clip si lo pones aqui;
// si lo dejas vacio, usa uno generado por codigo (asi siempre suena algo).
// Se crea solo, pero puedes agregarlo a un objeto de la escena para asignar tus audios.
public class Nivel1Audio : MonoBehaviour
{
    public static Nivel1Audio Instance { get; private set; }

    [Header("Tus propios sonidos (vacio = sonido generado)")]
    public AudioClip disparo;
    public AudioClip captura;
    public AudioClip fallo;
    public AudioClip fueraDeRango;
    public AudioClip sinEnergia;
    public AudioClip recarga;
    public AudioClip suelo;
    public AudioClip alerta;
    public AudioClip combo;
    public AudioClip comboRoto;
    public AudioClip orbeAparece;
    [Tooltip("Sonido continuo que se hace mas fuerte a medida que sube la contaminacion")]
    public AudioClip ambienteContaminacion;

    [Header("Volumen")]
    [Range(0f, 1f)] public float volumen = 0.8f;
    [Range(0f, 1f)] public float volumenAmbienteMaximo = 0.35f;

    private const int Rate = 44100;
    private AudioSource[] voces = new AudioSource[4];
    private int siguienteVoz;
    private AudioSource ambiente;
    private readonly AudioClip[] generados = new AudioClip[11];
    private AudioClip ambienteGenerado;

    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;

        for (int i = 0; i < voces.Length; i++)
        {
            voces[i] = gameObject.AddComponent<AudioSource>();
            voces[i].playOnAwake = false;
            voces[i].spatialBlend = 0f;
        }

        ambiente = gameObject.AddComponent<AudioSource>();
        ambiente.playOnAwake = false;
        ambiente.loop = true;
        ambiente.spatialBlend = 0f;
        ambiente.volume = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------ API estatica
    public static void Reproducir(SonidoN1 s, float pitch = 1f, float vol = 1f)
    {
        if (Instance != null && Instance.enabled) Instance.Play(s, pitch, vol);
    }

    // nivel01: 0 = silencio, 1 = contaminacion total
    public static void SetAmbiente(float nivel01)
    {
        if (Instance != null && Instance.enabled) Instance.AjustarAmbiente(nivel01);
    }

    private void Play(SonidoN1 s, float pitch, float vol)
    {
        AudioClip clip = Propio(s);
        if (clip == null) clip = Generado(s);
        if (clip == null) return;

        AudioSource v = voces[siguienteVoz];
        siguienteVoz = (siguienteVoz + 1) % voces.Length;
        v.pitch = pitch;
        v.PlayOneShot(clip, vol * volumen);
    }

    private void AjustarAmbiente(float n)
    {
        if (ambiente.clip == null)
        {
            if (ambienteContaminacion != null) ambiente.clip = ambienteContaminacion;
            else
            {
                if (ambienteGenerado == null) ambienteGenerado = GenerarAmbiente();
                ambiente.clip = ambienteGenerado;
            }
        }

        float v = Mathf.Clamp01(n) * volumenAmbienteMaximo;
        ambiente.volume = v;
        if (v > 0.01f && !ambiente.isPlaying) ambiente.Play();
        else if (v <= 0.01f && ambiente.isPlaying) ambiente.Stop();
    }

    private AudioClip Propio(SonidoN1 s)
    {
        switch (s)
        {
            case SonidoN1.Disparo: return disparo;
            case SonidoN1.Captura: return captura;
            case SonidoN1.Fallo: return fallo;
            case SonidoN1.FueraDeRango: return fueraDeRango;
            case SonidoN1.SinEnergia: return sinEnergia;
            case SonidoN1.Recarga: return recarga;
            case SonidoN1.Suelo: return suelo;
            case SonidoN1.Alerta: return alerta;
            case SonidoN1.Combo: return combo;
            case SonidoN1.ComboRoto: return comboRoto;
            default: return orbeAparece;
        }
    }

    // ================================================================== sonidos generados
    private AudioClip Generado(SonidoN1 s)
    {
        int i = (int)s;
        if (generados[i] != null) return generados[i];

        float[] d;
        switch (s)
        {
            case SonidoN1.Disparo: d = Barrido(0.16f, 1100f, 180f, 0.3f, 5f, 0.25f); break;
            case SonidoN1.Captura: d = Barrido(0.18f, 500f, 1100f, 0.3f, 6f, 0.05f); break;
            case SonidoN1.Fallo: d = Barrido(0.14f, 200f, 80f, 0.3f, 7f, 0.2f); break;
            case SonidoN1.FueraDeRango: d = DosTonos(620f, 440f); break;
            case SonidoN1.SinEnergia: d = Clic(); break;
            case SonidoN1.Recarga: d = Arpegio(new float[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, 0.3f); break;
            case SonidoN1.Suelo: d = Siseo(); break;
            case SonidoN1.Alerta: d = Sirena(); break;
            case SonidoN1.Combo: d = Arpegio(new float[] { 1318.5f, 1760f }, 0.07f, 0.2f); break;
            case SonidoN1.ComboRoto: d = Barrido(0.35f, 480f, 140f, 0.3f, 4f, 0f); break;
            default: d = Destello(); break;
        }

        generados[i] = AudioClip.Create("n1_" + s, d.Length, 1, Rate, false);
        generados[i].SetData(d, 0);
        return generados[i];
    }

    private static float[] Buf(float seg) { return new float[(int)(Rate * seg)]; }

    private static float[] Barrido(float dur, float f0, float f1, float vol, float caida, float ruido)
    {
        float[] d = Buf(dur);
        System.Random r = new System.Random(3);
        float fase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float k = i / (float)d.Length;
            float f = f0 * Mathf.Pow(f1 / f0, k);
            fase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Min(1f, i / (Rate * 0.004f)) * Mathf.Exp(-caida * k);
            float n = (float)(r.NextDouble() * 2.0 - 1.0) * ruido * Mathf.Exp(-30f * k);
            d[i] = (Mathf.Sin(fase) + 0.3f * Mathf.Sin(fase * 2f) + n) * vol * env / 1.3f;
        }
        return d;
    }

    private static float[] DosTonos(float a, float b)
    {
        float[] d = Buf(0.22f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = t < 0.09f ? a : b;
            float local = t < 0.09f ? t : t - 0.1f;
            float env = (t > 0.09f && t < 0.1f) ? 0f : Mathf.Exp(-12f * Mathf.Max(0f, local));
            d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.25f * env;
        }
        return d;
    }

    private static float[] Clic()
    {
        float[] d = Buf(0.2f);
        System.Random r = new System.Random(5);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float c1 = t < 0.015f ? (float)(r.NextDouble() * 2.0 - 1.0) * (1f - t / 0.015f) : 0f;
            float t2 = t - 0.09f;
            float c2 = (t2 > 0f && t2 < 0.015f) ? (float)(r.NextDouble() * 2.0 - 1.0) * (1f - t2 / 0.015f) : 0f;
            float thump = Mathf.Sin(2f * Mathf.PI * 150f * t) * Mathf.Exp(-30f * t) * 0.6f;
            d[i] = (c1 + c2) * 0.4f + thump * 0.4f;
        }
        return d;
    }

    private static float[] Arpegio(float[] notas, float porNota, float vol)
    {
        int paso = (int)(Rate * porNota);
        float[] d = new float[paso * notas.Length + (int)(Rate * 0.35f)];
        for (int k = 0; k < notas.Length; k++)
        {
            int ini = k * paso;
            for (int i = 0; ini + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, i / (Rate * 0.006f)) * Mathf.Exp(-5f * t);
                d[ini + i] += vol * 0.7f * env * Mathf.Sin(2f * Mathf.PI * notas[k] * t);
            }
        }
        return d;
    }

    // Siseo de contaminacion que se escapa + un "bloop" grave
    private static float[] Siseo()
    {
        float[] d = Buf(0.55f);
        System.Random r = new System.Random(9);
        float lp = 0f;
        float fase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float k = i / (float)d.Length;
            float ruido = (float)(r.NextDouble() * 2.0 - 1.0);
            lp += (ruido - lp) * 0.55f;
            float hiss = (ruido - lp) * Mathf.Sin(k * Mathf.PI) * Mathf.Exp(-2f * k);
            fase += 2f * Mathf.PI * Mathf.Lerp(220f, 100f, k) / Rate;
            float bloop = Mathf.Sin(fase) * Mathf.Exp(-9f * k);
            d[i] = hiss * 0.28f + bloop * 0.22f;
        }
        return d;
    }

    private static float[] Sirena()
    {
        float[] d = Buf(0.45f);
        float fase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float f = (t % 0.22f) < 0.11f ? 520f : 390f;
            fase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Min(1f, i / (Rate * 0.01f)) * Mathf.Min(1f, (d.Length - i) / (Rate * 0.05f));
            d[i] = Mathf.Sin(fase) * 0.28f * env;
        }
        return d;
    }

    private static float[] Destello()
    {
        float[] d = Buf(0.35f);
        float fase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float k = i / (float)d.Length;
            fase += 2f * Mathf.PI * Mathf.Lerp(700f, 1400f, k) / Rate;
            float trem = 0.6f + 0.4f * Mathf.Sin(k * 60f);
            d[i] = Mathf.Sin(fase) * 0.2f * trem * Mathf.Sin(k * Mathf.PI);
        }
        return d;
    }

    // Zumbido grave de 2 s que se repite sin saltos (frecuencias multiplos de 0.5 Hz)
    private static AudioClip GenerarAmbiente()
    {
        float[] d = Buf(2f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float s = Mathf.Sin(2f * Mathf.PI * 55f * t)
                    + 0.6f * Mathf.Sin(2f * Mathf.PI * 82.5f * t)
                    + 0.35f * Mathf.Sin(2f * Mathf.PI * 110f * t);
            float lfo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            d[i] = s * 0.25f * lfo;
        }
        AudioClip c = AudioClip.Create("n1_ambiente", d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }
}
