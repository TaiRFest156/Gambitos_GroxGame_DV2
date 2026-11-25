using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Vive siempre (se crea solo al abrir el juego). Se encarga de:
//  - aplicar el volumen de MUSICA y de EFECTOS a todos los sonidos del juego
//  - el brillo (una capa sobre la pantalla)
//  - guardar los ajustes al salir
// Como se reparte el sonido: un AudioSource es MUSICA si tiene el script CategoriaAudio (tipo Musica)
// o si suena en bucle y dura 15 segundos o mas. Todo lo demas son EFECTOS.
public class GestorOpciones : MonoBehaviour
{
    private class Reg
    {
        public float baseVol;
        public float aplicado = -1f;
    }

    private readonly Dictionary<AudioSource, Reg> regs = new Dictionary<AudioSource, Reg>();
    private readonly List<AudioSource> muertos = new List<AudioSource>();
    private Image brilloImg;
    private float proxEscaneo;

    private void Awake()
    {
        // Capa de brillo: por encima de todo, no recibe toques
        GameObject cgo = new GameObject("BrilloCanvas", typeof(RectTransform), typeof(Canvas));
        cgo.transform.SetParent(transform, false);
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        GameObject igo = new GameObject("Brillo", typeof(RectTransform), typeof(Image));
        igo.transform.SetParent(cgo.transform, false);
        RectTransform rt = (RectTransform)igo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        brilloImg = igo.GetComponent<Image>();
        brilloImg.raycastTarget = false;

        OpcionesJuego.AlCambiar += AlCambiar;
        SceneManager.sceneLoaded += AlCargarEscena;
        AlCambiar();
    }

    private void OnDestroy()
    {
        OpcionesJuego.AlCambiar -= AlCambiar;
        SceneManager.sceneLoaded -= AlCargarEscena;
    }

    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        OpcionesJuego.Aplicar();
        Escanear();
    }

    private void AlCambiar()
    {
        float b = OpcionesJuego.Brillo;
        if (b < 1f)
        {
            float a = Mathf.Clamp01((1f - b) / 0.4f) * 0.55f;
            brilloImg.color = new Color(0f, 0f, 0f, a);
        }
        else
        {
            float a = Mathf.Clamp01((b - 1f) / 0.4f) * 0.22f;
            brilloImg.color = new Color(1f, 1f, 1f, a);
        }
        brilloImg.enabled = brilloImg.color.a > 0.003f;
        Escanear();
    }

    private void Update()
    {
        if (Time.unscaledTime >= proxEscaneo)
        {
            proxEscaneo = Time.unscaledTime + 0.25f;
            Escanear();
        }
    }

    private void OnApplicationPause(bool pausa)
    {
        if (pausa) PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    // ================================================================== sonido
    private void Escanear()
    {
        float multMusica = OpcionesJuego.MultiplicadorMusica;
        float multEfectos = OpcionesJuego.MultiplicadorEfectos;

        AudioSource[] todas = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource s in todas)
        {
            if (s == null) continue;

            CategoriaAudio cat = s.GetComponent<CategoriaAudio>();
            if (cat != null && cat.ignorar) continue;

            Reg r;
            if (!regs.TryGetValue(s, out r))
            {
                r = new Reg { baseVol = s.volume };
                regs[s] = r;
            }

            // Si otro script cambio el volumen, ese pasa a ser el volumen base
            if (!Mathf.Approximately(s.volume, r.aplicado)) r.baseVol = s.volume;

            bool esMusica = cat != null ? cat.tipo == TipoAudio.Musica
                                        : (s.loop && s.clip != null && s.clip.length >= 15f);

            float nuevo = r.baseVol * (esMusica ? multMusica : multEfectos);
            s.volume = nuevo;
            r.aplicado = nuevo;
        }

        // limpiar los que ya no existen
        muertos.Clear();
        foreach (KeyValuePair<AudioSource, Reg> kv in regs)
            if (kv.Key == null) muertos.Add(kv.Key);
        foreach (AudioSource m in muertos) regs.Remove(m);
    }
}
