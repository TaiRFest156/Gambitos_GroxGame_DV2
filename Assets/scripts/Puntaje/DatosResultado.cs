using System;
using System.Collections.Generic;
using UnityEngine;

// Una fila del resumen (ej. "Papel (8 bien, 2 mal)  +60").
public class FilaDetalle
{
    public string etiqueta;
    public bool esNumero;
    public int numero;       // si esNumero: se anima contando hasta este valor
    public string prefijo = "";
    public string sufijo = "";
    public string texto;     // si no es numero
    public bool conColor;
    public Color color;

    public static FilaDetalle Numero(string etiqueta, int valor, string prefijo = "", string sufijo = "")
    {
        return new FilaDetalle { etiqueta = etiqueta, esNumero = true, numero = valor, prefijo = prefijo, sufijo = sufijo };
    }

    public static FilaDetalle Texto(string etiqueta, string texto)
    {
        return new FilaDetalle { etiqueta = etiqueta, esNumero = false, texto = texto };
    }
}

// Todo lo que necesita PantallaPuntaje para mostrar el resultado de un nivel.
public class DatosResultado
{
    public int nivel;
    public string tituloNivel = "";
    public int puntaje;
    public int meta = 1;

    [Tooltip("Multiplos de la meta para 1, 2 y 3 estrellas")]
    public float[] umbrales = { 1f, 1.5f, 2f };

    public List<FilaDetalle> filas = new List<FilaDetalle>();

    public bool permitirGuardar = true;

    // Boton de continuar (opcional): o una accion propia, o la escena siguiente
    public string textoContinuar = "Siguiente nivel";
    public Action alContinuar;
    public string escenaSiguiente = "";

    public bool congelarJuego = true;

    public int Estrellas
    {
        get { return CalculoEstrellas.Calcular(puntaje, meta, umbrales); }
    }
}

public static class CalculoEstrellas
{
    // Estrellas = cuantos umbrales (meta x multiplo) alcanzo el puntaje. Menos que el primero = 0.
    public static int Calcular(int puntaje, int meta, float[] umbrales)
    {
        meta = Mathf.Max(1, meta);
        int e = 0;
        for (int i = 0; i < 3 && i < umbrales.Length; i++)
            if (puntaje >= Mathf.RoundToInt(meta * umbrales[i])) e = i + 1;
        return e;
    }
}
