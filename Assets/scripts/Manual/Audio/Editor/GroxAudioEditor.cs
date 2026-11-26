using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Menus del sistema de audio:  Grox > Audio
public static class GroxAudioEditor
{
    private const string AssetPath = "Assets/Resources/GroxAudioConfig.asset";

    [MenuItem("Grox/Audio/1. Crear configuracion")]
    public static void CrearConfig()
    {
        GroxAudioConfig existente = AssetDatabase.LoadAssetAtPath<GroxAudioConfig>(AssetPath);
        if (existente != null)
        {
            Selection.activeObject = existente;
            EditorGUIUtility.PingObject(existente);
            EditorUtility.DisplayDialog("Grox Audio", "Ya existe la configuracion. La selecciono por ti.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        GroxAudioConfig cfg = ScriptableObject.CreateInstance<GroxAudioConfig>();

        cfg.music = new MusicTrack[]
        {
            Pista("Menu",     false, "Start_Menu", "LevelSelector"),
            Pista("Loading",  false, "LoadingScene"),
            Pista("Nivel 1",  true,  "Nivel_1", "Nivel1"),
            Pista("Nivel 2",  true,  "Nivel_2", "testNivel2"),
            Pista("Nivel 3",  true,  "Nivel_3"),
            Pista("Derrota",  false, "Derrota"),
            Pista("Creditos", false, "Creditos")
        };

        GroxSfx[] todos = (GroxSfx[])System.Enum.GetValues(typeof(GroxSfx));
        cfg.sfx = new GroxSfxEntry[todos.Length];
        for (int i = 0; i < todos.Length; i++)
        {
            GroxSfxEntry e = new GroxSfxEntry();
            e.id = todos[i];
            e.clips = new AudioClip[0];
            cfg.sfx[i] = e;
        }

        AssetDatabase.CreateAsset(cfg, AssetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);
        Debug.Log("[GroxAudio] Configuracion creada en " + AssetPath + ". Arrastra tus audios a cada escena y a cada efecto.");
    }

    private static MusicTrack Pista(string nombre, bool conCapas, params string[] escenas)
    {
        MusicTrack t = new MusicTrack();
        t.name = nombre;
        t.scenes = escenas;
        t.bpm = 120f;
        t.beatsPerBar = 4;

        MusicSection s = new MusicSection();
        s.name = "Main";
        if (conCapas)
        {
            s.layers = new MusicLayer[]
            {
                Capa("Base", 0f),
                Capa("Tension", 0.45f),
                Capa("Climax", 0.8f)
            };
        }
        else
        {
            s.layers = new MusicLayer[] { Capa("Base", 0f) };
        }
        t.sections = new MusicSection[] { s };
        return t;
    }

    private static MusicLayer Capa(string nombre, float entraEn)
    {
        MusicLayer l = new MusicLayer();
        l.name = nombre;
        l.enterAt = entraEn;
        return l;
    }

    [MenuItem("Grox/Audio/2. Optimizar import de la musica (movil)")]
    public static void OptimizarMusica()
    {
        GroxAudioConfig cfg = AssetDatabase.LoadAssetAtPath<GroxAudioConfig>(AssetPath);
        if (cfg == null)
        {
            EditorUtility.DisplayDialog("Grox Audio", "Primero crea la configuracion (Grox > Audio > 1).", "OK");
            return;
        }

        HashSet<string> rutas = new HashSet<string>();
        if (cfg.music != null)
        {
            for (int a = 0; a < cfg.music.Length; a++)
            {
                MusicTrack t = cfg.music[a];
                if (t == null || t.sections == null) continue;
                for (int b = 0; b < t.sections.Length; b++)
                {
                    MusicSection s = t.sections[b];
                    if (s == null || s.layers == null) continue;
                    for (int c = 0; c < s.layers.Length; c++)
                    {
                        if (s.layers[c] == null || s.layers[c].clip == null) continue;
                        rutas.Add(AssetDatabase.GetAssetPath(s.layers[c].clip));
                    }
                }
            }
        }

        int cambiados = 0;
        foreach (string ruta in rutas)
        {
            AudioImporter imp = AssetImporter.GetAtPath(ruta) as AudioImporter;
            if (imp == null) continue;

            AudioImporterSampleSettings st = imp.defaultSampleSettings;
            st.loadType = AudioClipLoadType.CompressedInMemory;
            st.compressionFormat = AudioCompressionFormat.Vorbis;
            st.quality = 0.6f;
            st.preloadAudioData = true;
            imp.defaultSampleSettings = st;
            imp.loadInBackground = false;
            imp.SaveAndReimport();
            cambiados++;
        }

        EditorUtility.DisplayDialog("Grox Audio", "Musica optimizada: " + cambiados + " clip(s).\nCompressed In Memory + Vorbis + Preload (ideal para Android y para que las capas queden sincronizadas).", "OK");
    }

    [MenuItem("Grox/Audio/3. Conectar sonidos al Puntaje")]
    public static void ConectarPuntaje()
    {
        GroxAudioConfig cfg = AssetDatabase.LoadAssetAtPath<GroxAudioConfig>(AssetPath);
        PuntajeConfig pc = AssetDatabase.LoadAssetAtPath<PuntajeConfig>("Assets/Resources/PuntajeConfig.asset");
        if (cfg == null || pc == null)
        {
            EditorUtility.DisplayDialog("Grox Audio", "Necesito que existan GroxAudioConfig (Grox > Audio > 1) y PuntajeConfig (Grox > Puntaje > Crear configuracion).", "OK");
            return;
        }

        Undo.RecordObject(pc, "Conectar sonidos de puntaje");
        if (pc.sonidoEstrella == null) pc.sonidoEstrella = Primero(cfg, GroxSfx.Star);
        if (pc.sonidoPanel == null) pc.sonidoPanel = Primero(cfg, GroxSfx.UiOpen);
        if (pc.sonidoBoton == null) pc.sonidoBoton = Primero(cfg, GroxSfx.UiClick);
        if (pc.sonidoGuardar == null) pc.sonidoGuardar = Primero(cfg, GroxSfx.Correct);
        if (pc.sonidoRecord == null) pc.sonidoRecord = Primero(cfg, GroxSfx.Success);
        EditorUtility.SetDirty(pc);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Grox Audio", "Listo. Solo rellene los sonidos del puntaje que estaban vacios.", "OK");
    }

    private static AudioClip Primero(GroxAudioConfig cfg, GroxSfx id)
    {
        GroxSfxEntry e = cfg.FindSfx(id);
        if (e == null || e.clips == null || e.clips.Length == 0) return null;
        return e.clips[0];
    }
}
