using System.Text;
using UnityEditor;
using UnityEngine;

// Inspector amigable para GroxAudioConfig:
//  - explica en simple la remezcla vertical y la resecuenciacion horizontal
//  - revisa que tus audios duren compases completos segun el BPM (si no, los cambios no caen "en tiempo")
//  - en Play: slider de intensidad para escuchar como evoluciona la musica y botones para saltar de seccion
[CustomEditor(typeof(GroxAudioConfig))]
public class GroxAudioConfigEditor : Editor
{
    private bool verAyuda = true, verChequeo = true, verPrueba = true;

    public override void OnInspectorGUI()
    {
        GroxAudioConfig cfg = (GroxAudioConfig)target;

        verAyuda = EditorGUILayout.Foldout(verAyuda, "¿Cómo funciona la música? (leer primero)", true);
        if (verAyuda)
        {
            EditorGUILayout.HelpBox(
                "UNA PISTA POR ESCENA: en 'Music' hay una entrada por cada nivel / pantalla (Menu, Loading, Nivel 1, 2, 3...). " +
                "En 'Scenes' escribes el nombre EXACTO de la escena. BPM = tempo de tu audio (los cambios caen en compas).\n\n" +
                "REMEZCLA VERTICAL (capas): dentro de una seccion pones varios audios que suenan a la vez (Base, Tension, Climax). " +
                "Cada capa tiene 'Enter At': entra cuando la intensidad (0 a 1) llega a ese valor. Asi la musica se llena sola cuando el nivel se pone dificil.\n\n" +
                "RESECUENCIACION HORIZONTAL (secciones): agrega mas secciones (Calma, Accion...). Cada una tiene Min/Max Intensity " +
                "y al terminar la actual, salta a la que corresponde a la intensidad del momento. 'Is Intro' = suena una sola vez al inicio.\n\n" +
                "LA INTENSIDAD sube sola: Nivel 1 con la contaminacion, Nivel 2 con el tiempo, Nivel 3 con los fusibles perdidos. " +
                "Todos los audios de una seccion deben durar lo mismo y en compases completos.\n\n" +
                "VOLUMEN: el panel de Opciones del juego (Música / Efectos) controla todo esto.",
                MessageType.Info);
        }

        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        verChequeo = EditorGUILayout.Foldout(verChequeo, "Revisión de tus audios (compases y tempo)", true);
        if (verChequeo) Chequeo(cfg);

        if (Application.isPlaying && GroxAudio.Music != null)
        {
            EditorGUILayout.Space(8);
            verPrueba = EditorGUILayout.Foldout(verPrueba, "Prueba en vivo (solo en Play)", true);
            if (verPrueba) Prueba(cfg);
            Repaint();
        }
    }

    private void Chequeo(GroxAudioConfig cfg)
    {
        if (cfg.music == null || cfg.music.Length == 0)
        {
            EditorGUILayout.HelpBox("No hay pistas en 'Music'.", MessageType.Warning);
            return;
        }

        bool todoBien = true;
        for (int a = 0; a < cfg.music.Length; a++)
        {
            MusicTrack t = cfg.music[a];
            if (t == null) continue;
            string nombre = string.IsNullOrEmpty(t.name) ? "Pista " + a : t.name;

            if (!t.HasAudio())
            {
                EditorGUILayout.HelpBox(nombre + ": todavía no tiene ningún audio.", MessageType.None);
                continue;
            }
            if (t.bpm < 1f) { Aviso(nombre + ": el BPM debe ser mayor que 0.", ref todoBien); continue; }
            if (t.scenes == null || t.scenes.Length == 0) Aviso(nombre + ": no tiene escenas asignadas, no sonará en ninguna.", ref todoBien);

            double compas = 60.0 / t.bpm * Mathf.Max(1, t.beatsPerBar);
            if (t.sections == null) continue;

            for (int b = 0; b < t.sections.Length; b++)
            {
                MusicSection s = t.sections[b];
                if (s == null || s.layers == null) continue;
                double min = double.MaxValue, max = 0;
                for (int c = 0; c < s.layers.Length; c++)
                {
                    MusicLayer L = s.layers[c];
                    if (L == null || L.clip == null) continue;
                    double len = L.clip.samples / (double)L.clip.frequency;
                    min = System.Math.Min(min, len);
                    max = System.Math.Max(max, len);
                    double compases = len / compas;
                    double dif = System.Math.Abs(compases - System.Math.Round(compases));
                    if (dif > 0.02)
                        Aviso(nombre + " / " + s.name + " / " + L.name + ": dura " + compases.ToString("0.00") +
                              " compases (con " + t.bpm + " BPM). Debería ser un número entero; revisa el BPM o recorta el audio.", ref todoBien);
                }
                if (max > 0 && max - min > 0.05)
                    Aviso(nombre + " / " + s.name + ": las capas no duran lo mismo (de " + min.ToString("0.00") + " s a " + max.ToString("0.00") + " s). Se desfasarían.", ref todoBien);
            }
        }

        if (todoBien) EditorGUILayout.HelpBox("Todo en orden: tus audios caen en compases completos.", MessageType.Info);
    }

    private static void Aviso(string texto, ref bool todoBien)
    {
        todoBien = false;
        EditorGUILayout.HelpBox(texto, MessageType.Warning);
    }

    private void Prueba(GroxAudioConfig cfg)
    {
        MusicDirector m = GroxAudio.Music;
        MusicTrack t = m.CurrentTrack;
        EditorGUILayout.LabelField("Sonando", t != null ? t.name : "(nada)");
        EditorGUILayout.LabelField("Intensidad actual", m.Intensity.ToString("0.00"));

        bool usar = GroxAudio.TestIntensity >= 0f;
        bool nuevoUsar = EditorGUILayout.Toggle("Controlar intensidad a mano", usar);
        if (nuevoUsar != usar) GroxAudio.TestIntensity = nuevoUsar ? Mathf.Max(0f, m.Intensity) : -1f;

        if (GroxAudio.TestIntensity >= 0f)
            GroxAudio.TestIntensity = EditorGUILayout.Slider("Intensidad de prueba", GroxAudio.TestIntensity, 0f, 1f);

        if (t != null && t.sections != null)
        {
            EditorGUILayout.LabelField("Saltar a sección (al terminar la actual):");
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < t.sections.Length; i++)
            {
                MusicSection s = t.sections[i];
                if (s == null || s.isIntro) continue;
                if (GUILayout.Button(s.name)) GroxAudio.GoToSection(s.name);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
