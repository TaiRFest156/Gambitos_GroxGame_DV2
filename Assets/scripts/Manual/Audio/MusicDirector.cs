using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// GROX AUDIO - DIRECTOR DE MUSICA ADAPTATIVA
//  * Remezcla VERTICAL: capas (stems) que entran/salen segun la intensidad 0-1.
//  * Resecuenciacion HORIZONTAL: secciones que se encadenan al final de cada
//    seccion segun la intensidad (o por GoToSection).
//  * Cambios entre musicas "organicos": la nueva entra en el siguiente compas
//    (o tiempo) de la que suena, con cruce suave. Todo programado con el reloj
//    de audio (dspTime), asi que no se desfasa aunque el juego se trabe cargando.
// Lo crea GroxAudio solo. No necesitas ponerlo en ninguna escena.
// ============================================================================
public class MusicDirector : MonoBehaviour
{
    private class PooledSource { public AudioSource src; public bool busy; }

    private class Voice
    {
        public MusicTrack track;
        public MusicSection section;
        public PooledSource[] srcs;
        public float[] layerGain;
        public bool[] layerOn;
        public double start, end;
        public bool loop;
        public int repeatIndex;

        public float fadeFrom = 1f, fadeTo = 1f;
        public double fadeStart, fadeDur;
        public bool dying;
        public bool useFrozen;
        public float frozenIntensity;

        public float Gain(double now)
        {
            if (fadeDur <= 0.0001) return now >= fadeStart ? fadeTo : fadeFrom;
            if (now <= fadeStart) return fadeFrom;
            if (now >= fadeStart + fadeDur) return fadeTo;
            float t = (float)((now - fadeStart) / fadeDur);
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(fadeFrom, fadeTo, t);
        }
    }

    private const double Lookahead = 3.0;

    private readonly List<PooledSource> pool = new List<PooledSource>();
    private readonly List<Voice> retiring = new List<Voice>();
    private readonly List<MusicSection> tmp = new List<MusicSection>();
    private Voice current, next;
    private MusicTrack currentTrack;

    private float intensity, intensityTarget;
    private float intensitySmooth = 1.0f;       // segundos para recorrer 0 -> 1
    private string pendingSection;

    private float duck = 1f, manualDuck = 1f, manualDuckUntil;
    private float musicVolume = 0.8f;

    private float opcMusica = 1f;   // volumen de MUSICA del panel de Opciones (0 si esta silenciada)

    private void Awake()
    {
        OpcionesJuego.AlCambiar += RefrescarOpciones;
        RefrescarOpciones();
    }

    private void OnDestroy() { OpcionesJuego.AlCambiar -= RefrescarOpciones; }

    private void RefrescarOpciones() { opcMusica = OpcionesJuego.MultiplicadorMusica; }

    public MusicTrack CurrentTrack { get { return currentTrack; } }
    public float Intensity { get { return intensityTarget; } }
    public float MusicVolume { get { return musicVolume; } set { musicVolume = Mathf.Clamp01(value); } }

    // ------------------------------------------------------------------ API
    public void SetIntensity(float v) { intensityTarget = Mathf.Clamp01(v); }

    public void GoToSection(string sectionName) { pendingSection = sectionName; }

    public void Duck(float amount01, float seconds)
    {
        manualDuck = Mathf.Clamp01(amount01);
        manualDuckUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
    }

    public void Play(MusicTrack track)
    {
        if (track == null || !track.HasAudio()) return;
        if (track == currentTrack && current != null && !current.dying) return;

        GroxAudioConfig cfg = GroxAudio.Config;
        double now = AudioSettings.dspTime;
        float xf = cfg != null ? Mathf.Max(0.05f, cfg.crossfadeSeconds) : 2.5f;
        float firstFade = cfg != null ? Mathf.Max(0.05f, cfg.firstFadeInSeconds) : 1f;

        bool audible = current != null && !current.dying;
        double t;
        float fadeIn, fadeOut;

        if (!audible && retiring.Count == 0)
        {
            t = now + 0.15;
            fadeIn = firstFade;
            fadeOut = xf;
        }
        else
        {
            bool pendingIncoming = current != null && current.start > now + 0.02;
            t = pendingIncoming ? now + 0.1 : NextTransitionTime(now);
            fadeIn = xf;
            fadeOut = xf;
        }

        // Lo que suena ahora se despide a partir de t
        if (current != null)
        {
            if (current.start > now + 0.02) Release(current);
            else Retire(current, t, fadeOut, now);
        }
        if (next != null)
        {
            if (next.start >= t) Release(next);
            else Retire(next, t, fadeOut, now);
        }
        for (int i = 0; i < retiring.Count; i++) Retire(retiring[i], t, fadeOut, now);

        current = null;
        next = null;
        currentTrack = track;

        intensity = track.baseIntensity;
        intensityTarget = track.baseIntensity;
        pendingSection = null;

        MusicSection first = ChooseStart(track);
        if (first == null) return;
        Voice v = Schedule(track, first, t, IsLoopSection(track, first), null);
        v.fadeFrom = 0f; v.fadeTo = 1f; v.fadeStart = t; v.fadeDur = fadeIn;
        current = v;
    }

    public void Stop(float fadeSeconds)
    {
        double now = AudioSettings.dspTime;
        double t = now + 0.05;
        float f = Mathf.Max(0.05f, fadeSeconds);
        if (current != null) Retire(current, t, f, now);
        if (next != null) Retire(next, t, f, now);
        current = null; next = null; currentTrack = null;
    }

    // ------------------------------------------------------------------ bucle
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        double now = AudioSettings.dspTime;
        GroxAudioConfig cfg = GroxAudio.Config;

        intensity = Mathf.MoveTowards(intensity, intensityTarget, dt / Mathf.Max(0.05f, intensitySmooth));

        float duckTarget = 1f;
        if (cfg != null && cfg.duckOnPause && Time.timeScale == 0f) duckTarget = cfg.pauseDuck;
        if (Time.unscaledTime < manualDuckUntil) duckTarget = Mathf.Min(duckTarget, manualDuck);
        duck = Mathf.MoveTowards(duck, duckTarget, dt * 2.5f);

        Advance(now);

        if (current != null) ApplyVoice(current, now, dt);
        if (next != null) ApplyVoice(next, now, dt);

        for (int i = retiring.Count - 1; i >= 0; i--)
        {
            Voice v = retiring[i];
            if (now >= v.fadeStart + v.fadeDur + 0.05)
            {
                Release(v);
                retiring.RemoveAt(i);
            }
            else ApplyVoice(v, now, dt);
        }
    }

    private void Advance(double now)
    {
        if (current == null) return;

        // La siguiente seccion ya empezo: la anterior termino sola
        if (next != null && now >= next.start)
        {
            Release(current);
            current = next;
            next = null;
        }

        // Programar la siguiente seccion con tiempo de sobra
        if (next == null && !current.loop && !current.dying && current.end - now < Lookahead)
        {
            MusicSection sec = ChooseNext(current);
            if (sec != null)
            {
                double start = Math.Max(current.end, now + 0.05);
                next = Schedule(current.track, sec, start, IsLoopSection(current.track, sec), current);
                next.repeatIndex = (sec == current.section) ? current.repeatIndex + 1 : 0;
            }
        }
    }

    private void ApplyVoice(Voice v, double now, float dt)
    {
        if (v.srcs == null) return;
        float g = v.Gain(now);
        float baseVol = v.track.volume * musicVolume * opcMusica * duck * g;
        float inten = v.useFrozen ? v.frozenIntensity : intensity;

        for (int k = 0; k < v.srcs.Length; k++)
        {
            PooledSource ps = v.srcs[k];
            if (ps == null) continue;
            MusicLayer L = v.section.layers[k];

            bool want = LayerWanted(L, inten, v.layerOn[k]);
            v.layerOn[k] = want;
            float fadeT = Mathf.Max(0.05f, L.fadeSeconds);
            v.layerGain[k] = Mathf.MoveTowards(v.layerGain[k], want ? 1f : 0f, dt / fadeT);
            float lg = v.layerGain[k];
            lg = lg * lg * (3f - 2f * lg);

            ps.src.volume = baseVol * L.volume * lg;
        }
    }

    private static bool LayerWanted(MusicLayer L, float inten, bool wasOn)
    {
        float h = wasOn ? 0.04f : 0f;   // histeresis: evita parpadeo cerca del umbral
        return inten >= L.enterAt - h && inten <= L.leaveAt + h;
    }

    // ------------------------------------------------------------------ elegir secciones
    private MusicSection ChooseStart(MusicTrack t)
    {
        if (t.sections == null) return null;
        for (int i = 0; i < t.sections.Length; i++)
        {
            MusicSection s = t.sections[i];
            if (s != null && s.isIntro && s.HasAudio()) return s;
        }
        return PickByIntensity(t, null);
    }

    private MusicSection PickByIntensity(MusicTrack t, MusicSection after)
    {
        tmp.Clear();
        for (int i = 0; i < t.sections.Length; i++)
        {
            MusicSection s = t.sections[i];
            if (s == null || s.isIntro || !s.HasAudio()) continue;
            if (intensity >= s.minIntensity - 0.001f && intensity <= s.maxIntensity + 0.001f) tmp.Add(s);
        }
        if (tmp.Count == 0)
        {
            for (int i = 0; i < t.sections.Length; i++)
            {
                MusicSection s = t.sections[i];
                if (s != null && !s.isIntro && s.HasAudio()) tmp.Add(s);
            }
        }
        if (tmp.Count == 0) return null;
        if (after == null || tmp.Count == 1) return tmp[0];
        int idx = tmp.IndexOf(after);
        return tmp[(idx + 1) % tmp.Count];
    }

    private MusicSection ChooseNext(Voice cur)
    {
        MusicTrack t = cur.track;
        if (t.sections == null || t.sections.Length == 0) return null;

        // Pedido manual: GoToSection
        if (!string.IsNullOrEmpty(pendingSection))
        {
            string want = pendingSection;
            pendingSection = null;
            for (int i = 0; i < t.sections.Length; i++)
            {
                MusicSection s = t.sections[i];
                if (s != null && s.HasAudio() && string.Equals(s.name, want, StringComparison.OrdinalIgnoreCase)) return s;
            }
        }

        // Repetir la actual si le faltan repeticiones y sigue siendo valida
        MusicSection c = cur.section;
        if (!c.isIntro && c.HasAudio() && cur.repeatIndex + 1 < Mathf.Max(1, c.repeats)
            && intensity >= c.minIntensity - 0.001f && intensity <= c.maxIntensity + 0.001f)
            return c;

        MusicSection pick = PickByIntensity(t, c.isIntro ? null : c);
        return pick != null ? pick : (c.HasAudio() ? c : null);
    }

    // Una pista con una sola seccion (no intro) se repite en bucle infinito: lo mas robusto
    private static bool IsLoopSection(MusicTrack t, MusicSection s)
    {
        if (s.isIntro) return false;
        int n = 0;
        for (int i = 0; i < t.sections.Length; i++)
            if (t.sections[i] != null && !t.sections[i].isIntro && t.sections[i].HasAudio()) n++;
        return n == 1;
    }

    // ------------------------------------------------------------------ tiempo musical
    private double NextTransitionTime(double now)
    {
        GroxAudioConfig cfg = GroxAudio.Config;
        double t = now + 0.1;
        Voice v = current;
        if (cfg == null || v == null || v.track.bpm < 1f || v.start > now) return t;
        if (cfg.quantize == MusicQuantize.Immediate) return t;

        double beat = 60.0 / v.track.bpm;
        double bar = beat * Math.Max(1, v.track.beatsPerBar);
        double grid = cfg.quantize == MusicQuantize.NextBar ? bar : beat;

        double elapsed = t - v.start;
        double cand = v.start + Math.Ceiling(elapsed / grid) * grid;
        if (cand - now > cfg.maxWaitSeconds)
            cand = v.start + Math.Ceiling(elapsed / beat) * beat;
        return Math.Max(cand, t);
    }

    // ------------------------------------------------------------------ voces
    private Voice Schedule(MusicTrack tr, MusicSection sec, double start, bool loop, Voice inherit)
    {
        Voice v = new Voice();
        v.track = tr; v.section = sec; v.start = start; v.loop = loop;

        int n = sec.layers != null ? sec.layers.Length : 0;
        v.srcs = new PooledSource[n];
        v.layerGain = new float[n];
        v.layerOn = new bool[n];

        double len = 0;
        for (int i = 0; i < n; i++)
        {
            AudioClip c = sec.layers[i] != null ? sec.layers[i].clip : null;
            if (c != null) len = Math.Max(len, c.samples / (double)c.frequency);
        }
        v.end = loop ? double.MaxValue : start + len;

        bool sameLayout = inherit != null && inherit.layerGain != null && inherit.layerGain.Length == n;

        for (int i = 0; i < n; i++)
        {
            MusicLayer L = sec.layers[i];
            if (L == null || L.clip == null) continue;
            if (L.clip.loadState == AudioDataLoadState.Unloaded) L.clip.LoadAudioData();

            PooledSource ps = Acquire();
            ps.src.clip = L.clip;
            ps.src.loop = loop;
            ps.src.pitch = 1f;
            ps.src.volume = 0f;
            ps.src.PlayScheduled(start);
            v.srcs[i] = ps;

            if (sameLayout) { v.layerGain[i] = inherit.layerGain[i]; v.layerOn[i] = inherit.layerOn[i]; }
            else
            {
                bool on = LayerWanted(L, intensity, false);
                v.layerOn[i] = on;
                v.layerGain[i] = on ? 1f : 0f;
            }
        }

        if (inherit != null)
        {
            v.fadeFrom = inherit.fadeFrom; v.fadeTo = inherit.fadeTo;
            v.fadeStart = inherit.fadeStart; v.fadeDur = inherit.fadeDur;
        }
        return v;
    }

    private void Retire(Voice v, double t, float dur, double now)
    {
        if (v.dying && v.fadeStart <= t) return;
        float g = v.Gain(now);
        v.fadeFrom = g; v.fadeTo = 0f; v.fadeStart = t; v.fadeDur = dur;
        v.dying = true;
        if (!v.useFrozen) { v.useFrozen = true; v.frozenIntensity = intensity; }
        if (!retiring.Contains(v)) retiring.Add(v);
    }

    private void Release(Voice v)
    {
        if (v == null || v.srcs == null) return;
        for (int i = 0; i < v.srcs.Length; i++)
        {
            PooledSource ps = v.srcs[i];
            if (ps == null) continue;
            ps.src.Stop();
            ps.src.clip = null;
            ps.busy = false;
            v.srcs[i] = null;
        }
    }

    private PooledSource Acquire()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].busy) { pool[i].busy = true; return pool[i]; }
        }
        PooledSource ps = new PooledSource();
        ps.src = gameObject.AddComponent<AudioSource>();
        ps.src.playOnAwake = false;
        ps.src.spatialBlend = 0f;
        ps.src.priority = 32;
        ps.src.ignoreListenerPause = true;
        ps.busy = true;
        pool.Add(ps);
        return ps;
    }
}
