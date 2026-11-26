using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================================
// GROX AUDIO - SISTEMA UNIVERSAL DE SONIDO
// Se crea solo al iniciar el juego (no hay que ponerlo en ninguna escena) y
// sobrevive entre escenas. Hace:
//   - Musica por escena (con cambio organico entre escenas)
//   - Efectos de sonido por codigo:  GroxAudio.Sfx(GroxSfx.Collect);
//   - Sonido de clic en todos los botones automaticamente
//   - Volumenes Master / Musica / Efectos guardados en PlayerPrefs
// Si falta el asset de configuracion solo avisa una vez y no rompe nada.
// ============================================================================
public class GroxAudio : MonoBehaviour
{
    public static GroxAudio Instance { get; private set; }
    public static GroxAudioConfig Config { get; private set; }
    public static MusicDirector Music { get; private set; }

    private const string KMaster = "grox_vol_master";
    private const string KMusic = "grox_vol_music";
    private const string KSfx = "grox_vol_sfx";

    private static float master = 1f, musicVol = 0.8f, sfxVol = 1f;

    // ------------------------------------------------------------------ arranque
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null; Config = null; Music = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GroxAudio");
        DontDestroyOnLoad(go);
        go.AddComponent<GroxAudio>();
    }

    // ------------------------------------------------------------------ volumenes (para tus sliders)
    public static float MasterVolume
    {
        get { return master; }
        set { master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KMaster, master); AudioListener.volume = master; }
    }

    public static float MusicVolume
    {
        get { return musicVol; }
        set { musicVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KMusic, musicVol); if (Music != null) Music.MusicVolume = musicVol; }
    }

    public static float SfxVolume
    {
        get { return sfxVol; }
        set { sfxVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KSfx, sfxVol); }
    }

    // ------------------------------------------------------------------ API estatica (segura: si no hay sistema, no hace nada)
    public static void Sfx(GroxSfx id, float volume = 1f, float pitchMultiplier = 1f)
    {
        if (Instance != null) Instance.PlaySfxInternal(id, volume, pitchMultiplier);
    }

    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (Instance != null && clip != null) Instance.PlayClip(clip, volume, pitch);
    }

    public static void PlayMusic(string trackName)
    {
        if (Config == null || Music == null) return;
        Music.Play(Config.FindTrack(trackName));
    }

    public static void StopMusic(float fadeSeconds = 1.5f) { if (Music != null) Music.Stop(fadeSeconds); }
    public static void SetIntensity(float v01) { if (Music != null) Music.SetIntensity(v01); }
    public static void GoToSection(string sectionName) { if (Music != null) Music.GoToSection(sectionName); }
    public static void DuckMusic(float amount01, float seconds) { if (Music != null) Music.Duck(amount01, seconds); }

    // ------------------------------------------------------------------ interno
    private AudioSource[] voices = new AudioSource[8];
    private int nextVoice;
    private readonly Dictionary<int, float> lastPlay = new Dictionary<int, float>();
    private readonly Dictionary<int, int> lastClip = new Dictionary<int, int>();

    private string handledScene;
    private float nextScan, nextProbe, nextWire;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Config = Resources.Load<GroxAudioConfig>("GroxAudioConfig");
        if (Config == null)
        {
            Debug.LogWarning("[GroxAudio] No encuentro Resources/GroxAudioConfig. Crealo en el menu Grox > Audio > 1. Crear configuracion. El juego funciona igual, solo sin el sistema de audio nuevo.");
        }

        master = PlayerPrefs.GetFloat(KMaster, Config != null ? Config.defaultMaster : 1f);
        musicVol = PlayerPrefs.GetFloat(KMusic, Config != null ? Config.defaultMusic : 0.8f);
        sfxVol = PlayerPrefs.GetFloat(KSfx, Config != null ? Config.defaultSfx : 1f);
        AudioListener.volume = master;

        Music = gameObject.AddComponent<MusicDirector>();
        Music.MusicVolume = musicVol;

        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
            voices[i].ignoreListenerPause = true;
        }
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void Start()
    {
        // Por si la primera escena ya estaba cargada
        Scene s = SceneManager.GetActiveScene();
        if (handledScene == null) ApplySceneMusic(s.name);
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; Music = null; }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        ApplySceneMusic(scene.name);
        GroxAudioLinks.ResetSceneCache();
        nextScan = 0f;
        nextWire = 0f;
    }

    private void ApplySceneMusic(string sceneName)
    {
        handledScene = sceneName;
        if (Config == null || Music == null) return;

        MusicTrack t = Config.FindTrackByScene(sceneName);
        if (t == null)
        {
            if (!Config.keepMusicIfSceneHasNone) Music.Stop(Config.crossfadeSeconds);
            return;
        }
        Music.Play(t);
    }

    private void Update()
    {
        if (Config == null) return;
        float now = Time.unscaledTime;

        if (Config.autoButtonSounds && now >= nextScan)
        {
            nextScan = now + 0.7f;
            ScanButtons();
        }

        if (Config.autoIntensityFromLevels && now >= nextProbe)
        {
            nextProbe = now + 0.15f;
            ProbeIntensity();
        }

        if (Config.autoWireExistingSounds && now >= nextWire)
        {
            nextWire = now + 2f;
            GroxAudioLinks.WireClips(Config);
        }
    }

    private void ProbeIntensity()
    {
        MusicTrack t = Music != null ? Music.CurrentTrack : null;
        if (t == null) return;

        if (GroxAudioLinks.ResultsScreenOpen())
        {
            Music.SetIntensity(0f);
            return;
        }

        float p = GroxAudioLinks.ReadIntensity();
        if (p >= 0f) Music.SetIntensity(Mathf.Max(t.baseIntensity, p));
    }

    // ------------------------------------------------------------------ botones
    private void ScanButtons()
    {
        Button[] bs = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < bs.Length; i++) Hook(bs[i]);

        Toggle[] ts = FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < ts.Length; i++) Hook(ts[i]);
    }

    private static void Hook(Selectable s)
    {
        GameObject go = s.gameObject;
        if (go.GetComponent<GroxButtonSfx>() != null) return;
        if (go.GetComponentInParent<GroxNoButtonSfx>() != null) return;
        if (GroxAudioLinks.ShouldSkipButton(s)) return;
        go.AddComponent<GroxButtonSfx>();
    }

    // ------------------------------------------------------------------ reproduccion
    private void PlaySfxInternal(GroxSfx id, float volume, float pitchMul)
    {
        if (Config == null) return;
        GroxSfxEntry e = Config.FindSfx(id);
        if (e == null || e.clips == null || e.clips.Length == 0) return;

        int key = (int)id;
        float now = Time.unscaledTime;
        float last;
        if (lastPlay.TryGetValue(key, out last) && now - last < e.cooldown) return;
        lastPlay[key] = now;

        // Elegir un clip distinto al anterior si hay varios
        int idx = 0;
        if (e.clips.Length > 1)
        {
            int prev;
            if (!lastClip.TryGetValue(key, out prev)) prev = -1;
            idx = Random.Range(0, e.clips.Length);
            if (idx == prev) idx = (idx + 1) % e.clips.Length;
            lastClip[key] = idx;
        }
        AudioClip c = e.clips[idx];
        if (c == null) return;

        float lo = Mathf.Min(e.pitch.x, e.pitch.y);
        float hi = Mathf.Max(e.pitch.x, e.pitch.y);
        PlayClip(c, e.volume * volume, Random.Range(lo, hi) * pitchMul);
    }

    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        AudioSource s = null;
        for (int i = 0; i < voices.Length; i++)
        {
            if (!voices[i].isPlaying) { s = voices[i]; break; }
        }
        if (s == null)
        {
            s = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
        }
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume * sfxVol);
        s.pitch = pitch;
        s.Play();
    }
}

// Marcador: ponlo en un boton (o en su padre) si NO quieres que suene el clic automatico
public class GroxNoButtonSfx : MonoBehaviour { }

// Lo agrega GroxAudio solo a cada Button y Toggle. Tambien puedes ponerlo a mano para elegir el sonido.
public class GroxButtonSfx : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    public GroxSfx sound = GroxSfx.UiClick;
    private Selectable sel;
    private bool resolved;

    private static readonly string[] BackWords = { "back", "atras", "volver", "regresar", "cerrar", "close", "home", "salir" };

    private void Resolve()
    {
        if (resolved) return;
        resolved = true;
        sel = GetComponent<Selectable>();
        if (sound != GroxSfx.UiClick) return;     // ya lo eligieron a mano
        if (sel is Toggle) { sound = GroxSfx.UiToggle; return; }
        string n = gameObject.name.ToLowerInvariant();
        for (int i = 0; i < BackWords.Length; i++)
            if (n.Contains(BackWords[i])) { sound = GroxSfx.UiBack; return; }
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        Fire();
    }

    public void OnSubmit(BaseEventData e) { Fire(); }

    private void Fire()
    {
        Resolve();
        if (sel != null && !sel.IsInteractable()) return;
        GroxAudio.Sfx(sound);
    }
}
