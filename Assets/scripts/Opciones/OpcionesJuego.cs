using System;
using UnityEngine;

// Ajustes del juego guardados en el dispositivo: sonido, brillo, grafica, pantalla, vibracion.
// Se aplican solos al abrir el juego (ver GestorOpciones). Los cambios se guardan al cerrar el panel de opciones.
public static class OpcionesJuego
{
    private const string KMusica = "Opc_VolMusica", KEfectos = "Opc_VolEfectos";
    private const string KMutMusica = "Opc_SilMusica", KMutEfectos = "Opc_SilEfectos";
    private const string KBrillo = "Opc_Brillo", KCalidad = "Opc_Calidad", KFps = "Opc_Fps";
    private const string KPantalla = "Opc_PantallaCompleta", KVibrar = "Opc_Vibrar";

    // Se dispara cuando cambia cualquier ajuste
    public static event Action AlCambiar;

    private static void Cambio() { if (AlCambiar != null) AlCambiar(); }

    // ------------------------------------------------------------------ sonido
    public static float VolumenMusica
    {
        get { return PlayerPrefs.GetFloat(KMusica, 0.8f); }
        set { PlayerPrefs.SetFloat(KMusica, Mathf.Clamp01(value)); Cambio(); }
    }

    public static float VolumenEfectos
    {
        get { return PlayerPrefs.GetFloat(KEfectos, 0.9f); }
        set { PlayerPrefs.SetFloat(KEfectos, Mathf.Clamp01(value)); Cambio(); }
    }

    public static bool SilencioMusica
    {
        get { return PlayerPrefs.GetInt(KMutMusica, 0) == 1; }
        set { PlayerPrefs.SetInt(KMutMusica, value ? 1 : 0); Cambio(); }
    }

    public static bool SilencioEfectos
    {
        get { return PlayerPrefs.GetInt(KMutEfectos, 0) == 1; }
        set { PlayerPrefs.SetInt(KMutEfectos, value ? 1 : 0); Cambio(); }
    }

    public static float MultiplicadorMusica { get { return SilencioMusica ? 0f : VolumenMusica; } }
    public static float MultiplicadorEfectos { get { return SilencioEfectos ? 0f : VolumenEfectos; } }

    // ------------------------------------------------------------------ pantalla
    // 0.6 (mas oscuro) a 1.4 (mas claro). 1 = normal
    public static float Brillo
    {
        get { return PlayerPrefs.GetFloat(KBrillo, 1f); }
        set { PlayerPrefs.SetFloat(KBrillo, Mathf.Clamp(value, 0.6f, 1.4f)); Cambio(); }
    }

    public static bool PantallaCompleta
    {
        get { return PlayerPrefs.GetInt(KPantalla, 1) == 1; }
        set { PlayerPrefs.SetInt(KPantalla, value ? 1 : 0); Aplicar(); Cambio(); }
    }

    // -1 = automatica, 0 = baja, 1 = media, 2 = alta
    public static int Calidad
    {
        get { return PlayerPrefs.GetInt(KCalidad, -1); }
        set { PlayerPrefs.SetInt(KCalidad, Mathf.Clamp(value, -1, 2)); Aplicar(); Cambio(); }
    }

    // 30, 60 o 0 (sin limite)
    public static int FpsLimite
    {
        get { return PlayerPrefs.GetInt(KFps, 60); }
        set { PlayerPrefs.SetInt(KFps, value); Aplicar(); Cambio(); }
    }

    public static bool Vibrar
    {
        get { return PlayerPrefs.GetInt(KVibrar, 1) == 1; }
        set { PlayerPrefs.SetInt(KVibrar, value ? 1 : 0); Cambio(); }
    }

    // ------------------------------------------------------------------ aplicar y guardar
    public static void Guardar()
    {
        PlayerPrefs.Save();
    }

    public static void RestablecerSonido()
    {
        PlayerPrefs.DeleteKey(KMusica); PlayerPrefs.DeleteKey(KEfectos);
        PlayerPrefs.DeleteKey(KMutMusica); PlayerPrefs.DeleteKey(KMutEfectos);
        Cambio();
    }

    public static void RestablecerPantalla()
    {
        PlayerPrefs.DeleteKey(KBrillo); PlayerPrefs.DeleteKey(KCalidad);
        PlayerPrefs.DeleteKey(KFps); PlayerPrefs.DeleteKey(KPantalla);
        Aplicar();
        Cambio();
    }

    public static void Aplicar()
    {
        // Frecuencia de cuadros (en celulares el valor por defecto de Unity es 30)
        Application.targetFrameRate = FpsLimite > 0 ? FpsLimite : -1;

        // Calidad grafica
        string[] nombres = QualitySettings.names;
        if (nombres.Length > 0)
        {
            int idx = IndiceCalidad(Calidad, nombres.Length);
            if (QualitySettings.GetQualityLevel() != idx) QualitySettings.SetQualityLevel(idx, true);
        }

        // Pantalla completa (solo PC)
        if (!Application.isMobilePlatform && !Application.isEditor)
            Screen.fullScreenMode = PantallaCompleta ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    // Convierte "automatica / baja / media / alta" en un nivel de calidad del proyecto
    public static int IndiceCalidad(int preset, int cantidad)
    {
        if (cantidad <= 1) return 0;
        if (preset < 0) preset = CalidadAutomatica();
        switch (preset)
        {
            case 0: return 0;
            case 1: return (cantidad - 1) / 2;
            default: return cantidad - 1;
        }
    }

    // Elige segun el equipo (memoria, procesador, video)
    public static int CalidadAutomatica()
    {
        int puntos = 0;
        int ram = SystemInfo.systemMemorySize;
        int cpu = SystemInfo.processorCount;
        int gpu = SystemInfo.graphicsMemorySize;

        if (ram >= 6000) puntos += 2; else if (ram >= 3500) puntos += 1;
        if (cpu >= 8) puntos += 2; else if (cpu >= 6) puntos += 1;
        if (gpu >= 3000) puntos += 2; else if (gpu >= 1500) puntos += 1;
        if (Application.isMobilePlatform) puntos -= 1;

        if (puntos <= 2) return 0;
        if (puntos <= 4) return 1;
        return 2;
    }

    public static string NombreCalidadActual()
    {
        string[] n = QualitySettings.names;
        if (n.Length == 0) return "-";
        return n[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, n.Length - 1)];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Iniciar()
    {
        Aplicar();
        GameObject go = new GameObject("GestorOpciones");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<GestorOpciones>();
    }
}
