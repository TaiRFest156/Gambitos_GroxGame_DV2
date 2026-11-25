using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public enum AccionTecla
{
    Adelante, Atras, Izquierda, Derecha, Saltar, Correr, Interactuar, Disparar,
    Pausa, Manual,
    ContenedorIzquierda, ContenedorArriba, ContenedorDerecha
}

// Teclas y botones del raton reasignables (PC). Se guardan en el dispositivo.
// Codigos: numero positivo = tecla (Key de Input System); -1 / -2 / -3 = clic izquierdo / derecho / medio.
// Uso:  if (TeclasJuego.PulsadaEsteFrame(AccionTecla.Saltar)) ...
public static class TeclasJuego
{
    public static bool Bloqueado;   // true mientras se esta asignando una tecla (el juego no reacciona)

    private static readonly int[] defecto = new int[13];

    static TeclasJuego()
    {
        defecto[(int)AccionTecla.Adelante] = (int)Key.W;
        defecto[(int)AccionTecla.Atras] = (int)Key.S;
        defecto[(int)AccionTecla.Izquierda] = (int)Key.A;
        defecto[(int)AccionTecla.Derecha] = (int)Key.D;
        defecto[(int)AccionTecla.Saltar] = (int)Key.Space;
        defecto[(int)AccionTecla.Correr] = (int)Key.LeftShift;
        defecto[(int)AccionTecla.Interactuar] = (int)Key.E;
        defecto[(int)AccionTecla.Disparar] = -1;
        defecto[(int)AccionTecla.Pausa] = (int)Key.Escape;
        defecto[(int)AccionTecla.Manual] = (int)Key.M;
        defecto[(int)AccionTecla.ContenedorIzquierda] = (int)Key.A;
        defecto[(int)AccionTecla.ContenedorArriba] = (int)Key.W;
        defecto[(int)AccionTecla.ContenedorDerecha] = (int)Key.D;
    }

    // ------------------------------------------------------------------ asignacion
    public static int Obtener(AccionTecla a)
    {
        return PlayerPrefs.GetInt("Tecla_" + a, defecto[(int)a]);
    }

    // Si otra accion del mismo grupo ya usaba esa tecla, se intercambian
    public static void Asignar(AccionTecla a, int codigo)
    {
        int anterior = Obtener(a);
        foreach (AccionTecla otra in (AccionTecla[])Enum.GetValues(typeof(AccionTecla)))
        {
            if (otra == a || Obtener(otra) != codigo || !Choca(a, otra)) continue;
            PlayerPrefs.SetInt("Tecla_" + otra, anterior);
        }
        PlayerPrefs.SetInt("Tecla_" + a, codigo);
    }

    public static void Restablecer()
    {
        foreach (AccionTecla a in (AccionTecla[])Enum.GetValues(typeof(AccionTecla)))
            PlayerPrefs.DeleteKey("Tecla_" + a);
    }

    // Movimiento / disparo (grupo 0), nivel 2 (grupo 2), globales (grupo 1: pausa y manual chocan con todo)
    private static int Grupo(AccionTecla a)
    {
        if (a == AccionTecla.Pausa || a == AccionTecla.Manual) return 1;
        if (a == AccionTecla.ContenedorIzquierda || a == AccionTecla.ContenedorArriba || a == AccionTecla.ContenedorDerecha) return 2;
        return 0;
    }

    private static bool Choca(AccionTecla a, AccionTecla b)
    {
        int ga = Grupo(a), gb = Grupo(b);
        return ga == gb || ga == 1 || gb == 1;
    }

    // ------------------------------------------------------------------ lectura
    public static bool Presionada(AccionTecla a) { return !Bloqueado && Leer(Obtener(a), 0); }
    public static bool PulsadaEsteFrame(AccionTecla a) { return !Bloqueado && Leer(Obtener(a), 1); }
    public static bool SoltadaEsteFrame(AccionTecla a) { return !Bloqueado && Leer(Obtener(a), 2); }

    private static bool Leer(int codigo, int modo)
    {
        ButtonControl b = Control(codigo);
        if (b == null) return false;
        switch (modo)
        {
            case 0: return b.isPressed;
            case 1: return b.wasPressedThisFrame;
            default: return b.wasReleasedThisFrame;
        }
    }

    private static ButtonControl Control(int codigo)
    {
        if (codigo > 0)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || codigo >= 200) return null;
            return kb[(Key)codigo];
        }

        Mouse m = Mouse.current;
        if (m == null) return null;
        switch (codigo)
        {
            case -1: return m.leftButton;
            case -2: return m.rightButton;
            case -3: return m.middleButton;
        }
        return null;
    }

    // ------------------------------------------------------------------ para la pantalla de opciones
    // Devuelve true cuando el jugador pulsa una tecla o un boton del raton (para asignarlo)
    public static bool CapturarPulsacion(out int codigo)
    {
        codigo = 0;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            foreach (KeyControl k in kb.allKeys)
            {
                if (k.wasPressedThisFrame && k.keyCode != Key.None && (int)k.keyCode < 200)
                {
                    codigo = (int)k.keyCode;
                    return true;
                }
            }
        }

        Mouse m = Mouse.current;
        if (m != null)
        {
            if (m.leftButton.wasPressedThisFrame) { codigo = -1; return true; }
            if (m.rightButton.wasPressedThisFrame) { codigo = -2; return true; }
            if (m.middleButton.wasPressedThisFrame) { codigo = -3; return true; }
        }
        return false;
    }

    public static string Nombre(int codigo)
    {
        if (codigo == -1) return "Clic izq.";
        if (codigo == -2) return "Clic der.";
        if (codigo == -3) return "Clic medio";
        if (codigo <= 0) return "-";

        Key k = (Key)codigo;
        switch (k)
        {
            case Key.Space: return "Espacio";
            case Key.LeftShift: return "Shift izq.";
            case Key.RightShift: return "Shift der.";
            case Key.LeftCtrl: return "Ctrl izq.";
            case Key.RightCtrl: return "Ctrl der.";
            case Key.LeftAlt: return "Alt izq.";
            case Key.RightAlt: return "Alt der.";
            case Key.Escape: return "Esc";
            case Key.Enter: return "Enter";
            case Key.Backspace: return "Retroceso";
            case Key.UpArrow: return "Flecha arriba";
            case Key.DownArrow: return "Flecha abajo";
            case Key.LeftArrow: return "Flecha izq.";
            case Key.RightArrow: return "Flecha der.";
        }

        string n = k.ToString();
        if (n.StartsWith("Digit")) return n.Substring(5);
        if (n.StartsWith("Numpad")) return "Num " + n.Substring(6);
        return n;
    }

    public static string NombreAccion(AccionTecla a)
    {
        switch (a)
        {
            case AccionTecla.Adelante: return "Avanzar";
            case AccionTecla.Atras: return "Retroceder";
            case AccionTecla.Izquierda: return "Izquierda";
            case AccionTecla.Derecha: return "Derecha";
            case AccionTecla.Saltar: return "Saltar";
            case AccionTecla.Correr: return "Correr";
            case AccionTecla.Interactuar: return "Interactuar";
            case AccionTecla.Disparar: return "Disparar";
            case AccionTecla.Pausa: return "Pausa";
            case AccionTecla.Manual: return "Manual";
            case AccionTecla.ContenedorIzquierda: return "Nivel 2: izquierda";
            case AccionTecla.ContenedorArriba: return "Nivel 2: arriba";
            default: return "Nivel 2: derecha";
        }
    }
}
