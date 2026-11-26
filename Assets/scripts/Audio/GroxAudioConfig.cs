using System;
using UnityEngine;

// ============================================================================
// GROX AUDIO - CONFIGURACION
// Un solo asset (Assets/Resources/GroxAudioConfig.asset) con TODO el audio:
// musica por escena (con capas y secciones) y efectos de sonido.
// Se crea con el menu:  Grox > Audio > 1. Crear configuracion
// ============================================================================

public enum GroxSfx
{
    // Interfaz
    UiClick, UiBack, UiToggle, UiOpen, UiClose, UiPage, UiError,
    // Nivel 1
    Collect, Combo, ComboBreak, Shoot, Miss, TrashFall, PowerUp, NoEnergy, Reload,
    // Nivel 2 / 3
    Correct, Wrong, Teleport, Interact, Footstep,
    // Generales
    Alert, Countdown, Success, Defeat, Star
}

public enum MusicQuantize { NextBar, NextBeat, Immediate }

[Serializable]
public class GroxSfxEntry
{
    public GroxSfx id;
    [Tooltip("Si pones varios clips, se elige uno distinto cada vez")]
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Variacion aleatoria del tono (min, max). 1,1 = sin variar")]
    public Vector2 pitch = new Vector2(0.97f, 1.03f);
    [Tooltip("Segundos minimos entre dos reproducciones (evita que se apile)")]
    public float cooldown = 0.05f;
}

[Serializable]
public class MusicLayer
{
    [Tooltip("Nombre libre (Base, Ritmo, Tension...)")]
    public string name = "Base";
    [Tooltip("Tu audio (stem). Todos los stems de una seccion deben durar lo mismo, en compases completos")]
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("REMEZCLA VERTICAL: la capa entra cuando la intensidad (0-1) llega a este valor. 0 = siempre suena")]
    [Range(0f, 1f)] public float enterAt = 0f;
    [Tooltip("La capa sale cuando la intensidad pasa de este valor. 1 = nunca sale")]
    [Range(0f, 1f)] public float leaveAt = 1f;
    [Tooltip("Segundos que tarda en entrar o salir")]
    public float fadeSeconds = 1.5f;
}

[Serializable]
public class MusicSection
{
    public string name = "Main";
    [Tooltip("Capas que suenan juntas en esta seccion")]
    public MusicLayer[] layers;
    [Tooltip("RESECUENCIACION HORIZONTAL: esta seccion se elige cuando la intensidad esta entre Min y Max")]
    [Range(0f, 1f)] public float minIntensity = 0f;
    [Range(0f, 1f)] public float maxIntensity = 1f;
    [Tooltip("Cuantas veces se repite antes de pasar a otra seccion posible")]
    public int repeats = 1;
    [Tooltip("Intro: suena una sola vez al empezar la musica y no se vuelve a elegir")]
    public bool isIntro = false;

    public bool HasAudio()
    {
        if (layers == null) return false;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i] != null && layers[i].clip != null) return true;
        return false;
    }
}

[Serializable]
public class MusicTrack
{
    [Tooltip("Nombre para llamarla por codigo: GroxAudio.PlayMusic(\"nombre\")")]
    public string name = "Musica";
    [Tooltip("Escenas donde suena (nombres exactos). Varias escenas pueden compartir la misma musica sin cortarse")]
    public string[] scenes;
    [Tooltip("Tempo de tus audios. Sirve para que los cambios caigan en compas")]
    public float bpm = 120f;
    public int beatsPerBar = 4;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Intensidad inicial (0 calma - 1 maxima)")]
    [Range(0f, 1f)] public float baseIntensity = 0f;
    [Tooltip("Normalmente una seccion 'Main' con 1 a 4 capas. Agrega mas secciones para resecuenciar")]
    public MusicSection[] sections;

    public bool HasAudio()
    {
        if (sections == null) return false;
        for (int i = 0; i < sections.Length; i++)
            if (sections[i] != null && sections[i].HasAudio()) return true;
        return false;
    }

    public bool HasScene(string scene)
    {
        if (scenes == null) return false;
        for (int i = 0; i < scenes.Length; i++)
            if (string.Equals(scenes[i], scene, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

[CreateAssetMenu(fileName = "GroxAudioConfig", menuName = "Grox/Audio/Config")]
public class GroxAudioConfig : ScriptableObject
{
    [Header("MUSICA (una entrada por escena o grupo de escenas)")]
    public MusicTrack[] music;

    [Header("EFECTOS DE SONIDO")]
    public GroxSfxEntry[] sfx;

    [Header("SONIDOS EXTRA de tus scripts (se conectan solos)")]
    [Tooltip("Loop de ambiente que sube con la contaminacion del Nivel 1 (Nivel1Audio.ambienteContaminacion)")]
    public AudioClip ambienceLoop;
    [Tooltip("Sonido de intro del manual (ManualSystem.sonidoIntro)")]
    public AudioClip manualIntro;
    [Tooltip("Una nota por color del Simon del Nivel 3, en orden (SimonWall.notasPersonalizadas)")]
    public AudioClip[] simonNotes;

    [Header("Cambio de musica entre escenas")]
    [Tooltip("En que momento empieza la nueva musica: siguiente compas, siguiente tiempo o ya")]
    public MusicQuantize quantize = MusicQuantize.NextBar;
    [Tooltip("Duracion del cruce entre la musica que sale y la que entra")]
    public float crossfadeSeconds = 2.5f;
    [Tooltip("Si el siguiente compas queda mas lejos que esto, se usa el siguiente tiempo")]
    public float maxWaitSeconds = 2f;
    [Tooltip("Fade-in de la primera musica del juego")]
    public float firstFadeInSeconds = 1.2f;
    [Tooltip("Si una escena no tiene musica asignada, sigue sonando la anterior")]
    public bool keepMusicIfSceneHasNone = true;

    [Header("Comportamiento automatico")]
    [Tooltip("Baja la musica cuando el juego esta en pausa (timeScale = 0)")]
    public bool duckOnPause = true;
    [Range(0f, 1f)] public float pauseDuck = 0.45f;
    [Tooltip("Lee la contaminacion del Nivel 1 y el tiempo del Nivel 2 para mover la intensidad")]
    public bool autoIntensityFromLevels = true;
    [Tooltip("Sonido de clic en todos los botones y toggles, sin tocar tus escenas")]
    public bool autoButtonSounds = true;
    [Tooltip("Pone tus clips de abajo en los sonidos que ya existen (Nivel 1, 2, 3...) si los dejaste vacios")]
    public bool autoWireExistingSounds = true;

    [Header("Volumenes por defecto (el jugador los puede cambiar)")]
    [Range(0f, 1f)] public float defaultMaster = 1f;
    [Range(0f, 1f)] public float defaultMusic = 0.8f;
    [Range(0f, 1f)] public float defaultSfx = 1f;

    public GroxSfxEntry FindSfx(GroxSfx id)
    {
        if (sfx == null) return null;
        for (int i = 0; i < sfx.Length; i++)
            if (sfx[i] != null && sfx[i].id == id) return sfx[i];
        return null;
    }

    public MusicTrack FindTrackByScene(string scene)
    {
        if (music == null) return null;
        for (int i = 0; i < music.Length; i++)
            if (music[i] != null && music[i].HasScene(scene)) return music[i];
        return null;
    }

    public MusicTrack FindTrack(string name)
    {
        if (music == null) return null;
        for (int i = 0; i < music.Length; i++)
            if (music[i] != null && string.Equals(music[i].name, name, StringComparison.OrdinalIgnoreCase)) return music[i];
        return null;
    }
}
