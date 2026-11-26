using UnityEngine;

// ============================================================================
// GROX AUDIO - ENLACES CON TUS SCRIPTS
// Es el UNICO archivo del sistema de audio que conoce clases de tu juego
// (Nivel1Sistema, Nivel1Audio, ResidueLevelManager, Nivel3Manager, ContainerFeedback,
//  HeadBob, ManualSystem, PantallaPuntaje). Solo LEE su estado o rellena clips
// que dejaste vacios; nunca los modifica de otra forma.
// Si algun dia borras o renombras uno de esos scripts, borra solo su bloque aqui.
// ============================================================================
public static class GroxAudioLinks
{
    private static ResidueLevelManager nivel2;

    public static void ResetSceneCache() { nivel2 = null; }

    // Los botones de la pantalla de puntaje ya tienen su propio sonido
    public static bool ShouldSkipButton(Component c)
    {
        return c.GetComponentInParent<PantallaPuntaje>() != null;
    }

    public static bool ResultsScreenOpen()
    {
        return PantallaPuntaje.Instance != null;
    }

    // Devuelve 0-1 segun lo que pasa en el nivel, o -1 si esta escena no aporta nada
    public static float ReadIntensity()
    {
        // Nivel 1: contaminacion del planeta
        Nivel1Sistema n1 = Nivel1Sistema.Instance;
        if (n1 != null) return Mathf.Clamp01(n1.Contaminacion / 90f);

        // Nivel 2: presion del tiempo
        if (nivel2 == null) nivel2 = Object.FindFirstObjectByType<ResidueLevelManager>();
        if (nivel2 != null)
        {
            float dur = Mathf.Max(1f, nivel2.levelDuration);
            return Mathf.Clamp01(1f - nivel2.GetTimeRemaining() / dur);
        }
        return -1f;
    }

    // Pone TUS clips en los sonidos que ya existen, solo si estaban vacios
    public static void WireClips(GroxAudioConfig cfg)
    {
        Nivel1Audio a = Object.FindFirstObjectByType<Nivel1Audio>();
        if (a != null)
        {
            if (a.disparo == null) a.disparo = C(cfg, GroxSfx.Shoot);
            if (a.captura == null) a.captura = C(cfg, GroxSfx.Collect);
            if (a.fallo == null) a.fallo = C(cfg, GroxSfx.Miss);
            if (a.fueraDeRango == null) a.fueraDeRango = C(cfg, GroxSfx.UiError);
            if (a.sinEnergia == null) a.sinEnergia = C(cfg, GroxSfx.NoEnergy);
            if (a.recarga == null) a.recarga = C(cfg, GroxSfx.Reload);
            if (a.suelo == null) a.suelo = C(cfg, GroxSfx.TrashFall);
            if (a.alerta == null) a.alerta = C(cfg, GroxSfx.Alert);
            if (a.combo == null) a.combo = C(cfg, GroxSfx.Combo);
            if (a.comboRoto == null) a.comboRoto = C(cfg, GroxSfx.ComboBreak);
            if (a.orbeAparece == null) a.orbeAparece = C(cfg, GroxSfx.PowerUp);
            if (a.ambienteContaminacion == null && cfg.ambienceLoop != null) a.ambienteContaminacion = cfg.ambienceLoop;
        }

        Nivel3Manager n3 = Object.FindFirstObjectByType<Nivel3Manager>();
        if (n3 != null)
        {
            if (n3.sonidoExito == null) n3.sonidoExito = C(cfg, GroxSfx.Success);
            if (n3.sonidoError == null) n3.sonidoError = C(cfg, GroxSfx.Wrong);
            if (n3.sonidoTeleport == null) n3.sonidoTeleport = C(cfg, GroxSfx.Teleport);
            if (n3.sonidoDing == null) n3.sonidoDing = C(cfg, GroxSfx.Correct);
        }

        ContainerFeedback cf = Object.FindFirstObjectByType<ContainerFeedback>();
        if (cf != null)
        {
            if (cf.sonidoAcierto == null) cf.sonidoAcierto = C(cfg, GroxSfx.Correct);
            if (cf.sonidoError == null) cf.sonidoError = C(cfg, GroxSfx.Wrong);
        }

        HeadBob hb = Object.FindFirstObjectByType<HeadBob>();
        if (hb != null && hb.fuenteAudio != null && (hb.sonidosPasos == null || hb.sonidosPasos.Length == 0))
        {
            GroxSfxEntry e = cfg.FindSfx(GroxSfx.Footstep);
            if (e != null && e.clips != null && e.clips.Length > 0) hb.sonidosPasos = e.clips;
        }

        ManualSystem m = ManualSystem.Instance;
        if (m != null)
        {
            if (m.sonidoAbrir == null) m.sonidoAbrir = C(cfg, GroxSfx.UiOpen);
            if (m.sonidoPagina == null) m.sonidoPagina = C(cfg, GroxSfx.UiPage);
            if (m.sonidoIntro == null && cfg.manualIntro != null) m.sonidoIntro = cfg.manualIntro;
        }

        if (cfg.simonNotes != null && cfg.simonNotes.Length > 0)
        {
            SimonWall[] walls = Object.FindObjectsByType<SimonWall>(FindObjectsSortMode.None);
            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i].notasPersonalizadas == null || walls[i].notasPersonalizadas.Length == 0)
                    walls[i].notasPersonalizadas = cfg.simonNotes;
            }
        }
    }

    private static AudioClip C(GroxAudioConfig cfg, GroxSfx id)
    {
        GroxSfxEntry e = cfg.FindSfx(id);
        if (e == null || e.clips == null || e.clips.Length == 0) return null;
        return e.clips[0];
    }
}
