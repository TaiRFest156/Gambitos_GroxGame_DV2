using System;
using UnityEngine;

public enum ModoCamara
{
    Joystick = 0,    // segundo joystick tactil en la pantalla (por defecto en movil)
    Giroscopio = 1,  // girar el celular
    Arrastrar = 2    // arrastrar el dedo por la mitad derecha (tocar = disparar)
}

// Preferencias de control guardadas en el dispositivo (sobreviven al cerrar el juego).
// Pensado para conectarlo despues al menu de opciones / pausa:
//   ControlSettings.Modo = ModoCamara.Giroscopio;
public static class ControlSettings
{
    private const string KModo = "Controles_ModoCamara";
    private const string KStick = "Controles_SensStick";
    private const string KMouse = "Controles_SensMouse";
    private const string KInvertir = "Controles_InvertirY";

    // Se dispara cuando cambia el modo (los controles tactiles se actualizan solos)
    public static event Action AlCambiar;

    public static ModoCamara Modo
    {
        get { return (ModoCamara)PlayerPrefs.GetInt(KModo, (int)ModoCamara.Joystick); }
        set
        {
            PlayerPrefs.SetInt(KModo, (int)value);
            PlayerPrefs.Save();
            if (AlCambiar != null) AlCambiar();
        }
    }

    // Multiplicadores (1 = valor por defecto)
    public static float SensibilidadStick
    {
        get { return PlayerPrefs.GetFloat(KStick, 1f); }
        set { PlayerPrefs.SetFloat(KStick, Mathf.Clamp(value, 0.3f, 3f)); }
    }

    public static float SensibilidadMouse
    {
        get { return PlayerPrefs.GetFloat(KMouse, 1f); }
        set { PlayerPrefs.SetFloat(KMouse, Mathf.Clamp(value, 0.3f, 3f)); }
    }

    public static bool InvertirY
    {
        get { return PlayerPrefs.GetInt(KInvertir, 0) == 1; }
        set { PlayerPrefs.SetInt(KInvertir, value ? 1 : 0); }
    }
}
