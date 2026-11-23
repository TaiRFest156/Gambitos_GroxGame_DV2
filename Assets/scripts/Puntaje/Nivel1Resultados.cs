using System.Collections.Generic;
using UnityEngine;

// Arma el resultado del Nivel 1 (con el puntaje de ReciclajeContador) y abre la pantalla de puntaje.
public static class Nivel1Resultados
{
    public static void Mostrar(SceneLoader loader, float umbralDosEstrellas, float umbralTresEstrellas)
    {
        DatosResultado d = new DatosResultado();
        d.nivel = 1;
        d.tituloNivel = "Nivel 1 - Recolección";
        d.puntaje = ReciclajeContador.CalcularPuntajeTotal();
        d.meta = ReciclajeContador.MetaPuntaje;
        d.umbrales = new float[] { 1f, umbralDosEstrellas, umbralTresEstrellas };

        // Un renglon por tipo de residuo recogido
        int total = 0;
        foreach (KeyValuePair<string, int> kv in ReciclajeContador.conteo)
        {
            if (kv.Value <= 0) continue;
            total += kv.Value;
            int valor = ReciclajeContador.valores.ContainsKey(kv.Key) ? ReciclajeContador.valores[kv.Key] : 0;
            d.filas.Add(FilaDetalle.Numero(Nombre(kv.Key) + "  x" + kv.Value, kv.Value * valor, "+"));
        }
        if (total == 0) d.filas.Add(FilaDetalle.Texto("Residuos recogidos", "0"));

        if (ReciclajeContador.PuntosBonus > 0)
            d.filas.Add(FilaDetalle.Numero("Bonus de combos", ReciclajeContador.PuntosBonus, "+"));

        Nivel1Sistema sis = Nivel1Sistema.Instance;
        if (sis != null)
        {
            if (sis.MejorCombo >= 2) d.filas.Add(FilaDetalle.Texto("Mejor combo", "x" + sis.MejorCombo));
            d.filas.Add(FilaDetalle.Texto("Contaminación del planeta", Mathf.RoundToInt(sis.Contaminacion) + "%"));
        }

        d.permitirGuardar = d.Estrellas >= 1;

        // "Continuar" mantiene tu flujo: oculta el panel y desbloquea la puerta hacia el Nivel 2
        if (loader != null)
        {
            d.textoContinuar = "Continuar";
            d.alContinuar = loader.CargarSiguienteEscena;
        }

        PantallaPuntaje.Mostrar(d);
    }

    private static string Nombre(string tag)
    {
        switch (tag)
        {
            case "Plastico": return "Plástico";
            case "Toxico": return "Tóxico";
            case "Organico": return "Orgánico";
            default: return tag;
        }
    }
}
