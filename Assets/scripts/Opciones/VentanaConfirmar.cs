using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ventana de confirmacion con el MISMO estilo que la pausa y las opciones (usa PuntajeConfig).
// Sirve en cualquier escena (menu principal, niveles...). Se crea sola y se destruye al cerrarse.
//
// Uso desde codigo:
//   VentanaConfirmar.Mostrar("¿SALIR DEL JUEGO?", "¿Seguro que quieres salir?", () => Application.Quit());
//
// Uso desde un boton (sin codigo): agrega este script a un objeto vacio y conecta
//   OnClick -> VentanaConfirmar.PedirSalirDelJuego      (cierra la aplicacion)
//   OnClick -> VentanaConfirmar.PedirSalirAlMenu        (carga la escena de menu de PuntajeConfig)
public class VentanaConfirmar : MonoBehaviour
{
    [Header("Textos (para los metodos que se llaman desde un boton)")]
    public string tituloSalirJuego = "¿SALIR DEL JUEGO?";
    public string mensajeSalirJuego = "¿Seguro que quieres salir?";
    public string tituloSalirMenu = "¿SALIR AL MENÚ?";
    public string mensajeSalirMenu = "Perderás el progreso de esta partida.";
    public string textoSi = "Sí";
    public string textoNo = "No";

    private static GameObject abierta;

    // ---------------------------------------------------------------- para botones del Inspector
    public void PedirSalirDelJuego()
    {
        Mostrar(tituloSalirJuego, mensajeSalirJuego, SalirDelJuego, null, textoSi, textoNo);
    }

    public void PedirSalirAlMenu()
    {
        Mostrar(tituloSalirMenu, mensajeSalirMenu, () =>
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(PuntajeConfig.Actual.escenaMenu);
        }, null, textoSi, textoNo);
    }

    private static void SalirDelJuego()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- desde codigo
    public static void Mostrar(string titulo, string mensaje, Action alAceptar, Action alCancelar = null,
                               string textoSi = "Sí", string textoNo = "No")
    {
        if (abierta != null) return;
        PuntajeConfig cfg = PuntajeConfig.Actual;

        GameObject cgo = new GameObject("ConfirmarCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;   // por encima de pausa (520) y opciones (600)
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        abierta = cgo;

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem));
            UnityEngine.InputSystem.UI.InputSystemUIInputModule m = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            m.AssignDefaultActions();
        }

        // Fondo oscuro que tapa los botones de atras
        Image dim = UIPuntaje.Img("Oscuro", cgo.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.8f), null, false);
        dim.raycastTarget = true;

        // La ventana (misma funcion que la pausa: respeta fondoPanel, colores y forma de esquinas)
        RectTransform c = UIPuntaje.Panel("Confirmar", cgo.transform, new Vector2(0.30f, 0.28f), new Vector2(0.70f, 0.72f));

        TextMeshProUGUI t = UIPuntaje.Texto("Titulo", c, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.92f), 24f, 64f,
                                            FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        t.text = titulo;
        TextMeshProUGUI msg = UIPuntaje.Texto("Mensaje", c, new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.66f), 16f, 40f,
                                              FontStyles.Normal, TextAlignmentOptions.Center, cfg.colorTexto);
        msg.text = mensaje;

        Button si = UIPuntaje.Boton("Si", textoSi, c, new Vector2(0.08f, 0.1f), new Vector2(0.48f, 0.36f), TipoBoton.Advertencia);
        si.onClick.AddListener(() =>
        {
            Cerrar();
            if (alAceptar != null) alAceptar();
        });

        Button no = UIPuntaje.Boton("No", textoNo, c, new Vector2(0.52f, 0.1f), new Vector2(0.92f, 0.36f), TipoBoton.Primario);
        no.onClick.AddListener(() =>
        {
            Cerrar();
            if (alCancelar != null) alCancelar();
        });
    }

    public static void Cerrar()
    {
        if (abierta != null) Destroy(abierta);
        abierta = null;
    }
}
