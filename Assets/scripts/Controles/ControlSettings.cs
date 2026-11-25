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

    // ------------------------------------------------------------------ controles tactiles
    public static float SensibilidadGiro
    {
        get { return PlayerPrefs.GetFloat("Controles_SensGiro", 1f); }
        set { PlayerPrefs.SetFloat("Controles_SensGiro", Mathf.Clamp(value, 0.3f, 3f)); }
    }

    // Tamano de joystick y botones (1 = normal)
    public static float EscalaControles
    {
        get { return PlayerPrefs.GetFloat("Controles_Escala", 1f); }
        set { PlayerPrefs.SetFloat("Controles_Escala", Mathf.Clamp(value, 0.6f, 1.6f)); if (AlCambiar != null) AlCambiar(); }
    }

    public static float OpacidadControles
    {
        get { return PlayerPrefs.GetFloat("Controles_Opacidad", 0.6f); }
        set { PlayerPrefs.SetFloat("Controles_Opacidad", Mathf.Clamp(value, 0.2f, 1f)); if (AlCambiar != null) AlCambiar(); }
    }

    // Posiciones: pixeles (sobre 1920x1080) desde la esquina inferior derecha hasta el centro del control
    public static Vector2 PosStick(Vector2 defecto) { return LeerPos("Stick", defecto); }
    public static Vector2 PosFuego(Vector2 defecto) { return LeerPos("Fuego", defecto); }
    public static Vector2 PosSalto(Vector2 defecto) { return LeerPos("Salto", defecto); }
    public static void GuardarPosStick(Vector2 p) { GuardarPos("Stick", p); }
    public static void GuardarPosFuego(Vector2 p) { GuardarPos("Fuego", p); }
    public static void GuardarPosSalto(Vector2 p) { GuardarPos("Salto", p); }

    private static Vector2 LeerPos(string n, Vector2 defecto)
    {
        if (!PlayerPrefs.HasKey("Controles_Pos" + n + "X")) return defecto;
        return new Vector2(PlayerPrefs.GetFloat("Controles_Pos" + n + "X"), PlayerPrefs.GetFloat("Controles_Pos" + n + "Y"));
    }

    private static void GuardarPos(string n, Vector2 p)
    {
        PlayerPrefs.SetFloat("Controles_Pos" + n + "X", p.x);
        PlayerPrefs.SetFloat("Controles_Pos" + n + "Y", p.y);
    }

    public static void RestablecerPosiciones()
    {
        foreach (string n in new[] { "Stick", "Fuego", "Salto" })
        {
            PlayerPrefs.DeleteKey("Controles_Pos" + n + "X");
            PlayerPrefs.DeleteKey("Controles_Pos" + n + "Y");
        }
        if (AlCambiar != null) AlCambiar();
    }

    public static void RestablecerTactil()
    {
        RestablecerPosiciones();
        PlayerPrefs.DeleteKey("Controles_Escala");
        PlayerPrefs.DeleteKey("Controles_Opacidad");
        PlayerPrefs.DeleteKey("Controles_SensGiro");
        PlayerPrefs.DeleteKey("Controles_SensStick");
        PlayerPrefs.DeleteKey("Controles_ModoCamara");
        if (AlCambiar != null) AlCambiar();
    }

    public static void RestablecerPC()
    {
        PlayerPrefs.DeleteKey("Controles_SensMouse");
        PlayerPrefs.DeleteKey("Controles_InvertirY");
        TeclasJuego.Restablecer();
        if (AlCambiar != null) AlCambiar();
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
