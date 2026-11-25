using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Panel de OPCIONES: el mismo en el menu principal y en la pausa de los niveles.
// Secciones: Sonido | Controles (segun PC o movil) | Pantalla y graficos.
// Todo se guarda en el dispositivo. Imagenes, fuentes, colores y textos: asset PuntajeConfig.
public class PanelOpciones : MonoBehaviour
{
    public static bool Capturando { get; private set; }

    public static PanelOpciones Crear(Transform padre, Vector2 min, Vector2 max, Action alCerrar)
    {
        RectTransform rt = UIPuntaje.Rect("PanelOpciones", padre, min, max);
        PanelOpciones p = rt.gameObject.AddComponent<PanelOpciones>();
        p.Construir(alCerrar);
        rt.gameObject.SetActive(false);
        return p;
    }

    private const int Secciones = 3;

    private PuntajeConfig cfg;
    private Action alCerrar;
    private readonly Button[] tabs = new Button[Secciones];
    private readonly RectTransform[] contenidos = new RectTransform[Secciones];
    private int tabActual;
    private float proximoBlip;

    // sonido
    private Slider sMusica, sEfectos;
    private Image icoMusica, icoEfectos;
    private TextMeshProUGUI pMusica, pEfectos;

    // controles PC
    private RectTransform contPC, contMovil;
    private Slider sMouse;
    private TextMeshProUGUI vMouse;
    private Toggle tInvertir;
    private readonly Dictionary<AccionTecla, Button> botonesTecla = new Dictionary<AccionTecla, Button>();
    private int capturando = -1;
    private int frameFinCaptura = -1;

    // controles movil
    private Button[] segCamara;
    private Slider sStick, sGiro, sTam, sOpac;
    private TextMeshProUGUI vStick, vGiro, vTam, vOpac;
    private Toggle tVibrar;
    private readonly List<RectTransform> filasMovil = new List<RectTransform>();

    // pantalla
    private RectTransform filaPantallaCompleta;
    private Toggle tPantalla;
    private Slider sBrillo;
    private TextMeshProUGUI vBrillo, infoCalidad;
    private Button[] segCalidad, segFps;
    private readonly List<RectTransform> filasPantalla = new List<RectTransform>();

    private static readonly AccionTecla[] OrdenTeclas =
    {
        AccionTecla.Adelante, AccionTecla.Atras, AccionTecla.Izquierda, AccionTecla.Derecha,
        AccionTecla.Saltar, AccionTecla.Correr, AccionTecla.Interactuar,
        AccionTecla.Disparar, AccionTecla.Pausa, AccionTecla.Manual,
        AccionTecla.ContenedorIzquierda, AccionTecla.ContenedorArriba, AccionTecla.ContenedorDerecha
    };

    // ================================================================== abrir / cerrar
    public void Abrir()
    {
        gameObject.SetActive(true);
        Refrescar();
        SeleccionarTab(tabActual);
    }

    public void Cerrar()
    {
        CancelarCaptura();
        OpcionesJuego.Guardar();
        gameObject.SetActive(false);
        if (alCerrar != null) alCerrar();
    }

    private void OnDisable()
    {
        CancelarCaptura();
    }

    // ================================================================== construccion
    private void Construir(Action cerrar)
    {
        alCerrar = cerrar;
        cfg = UIPuntaje.Config;

        RectTransform area = UIPuntaje.Panel("Fondo", transform, Vector2.zero, Vector2.one);

        TextMeshProUGUI titulo = UIPuntaje.Texto("Titulo", area, new Vector2(0.12f, 0.91f), new Vector2(0.88f, 0.99f), 28f, 64f,
                                                 FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        titulo.text = cfg.tituloOpciones;

        Button x = UIPuntaje.BotonCerrar(area);
        x.onClick.AddListener(Cerrar);

        string[] nombres = { "Sonido", "Controles", "Pantalla" };
        for (int i = 0; i < Secciones; i++)
        {
            float x0 = 0.04f + i * 0.32f;
            int idx = i;
            tabs[i] = UIPuntaje.Boton("Tab" + i, nombres[i], area, new Vector2(x0, 0.80f), new Vector2(x0 + 0.30f, 0.895f), TipoBoton.Secundario);
            tabs[i].onClick.AddListener(() => SeleccionarTab(idx));
            contenidos[i] = UIPuntaje.Rect("Contenido" + i, area, new Vector2(0.04f, 0.15f), new Vector2(0.96f, 0.785f));
        }

        ConstruirSonido(contenidos[0]);
        ConstruirControles(contenidos[1]);
        ConstruirPantalla(contenidos[2]);

        Button reset = UIPuntaje.Boton("Restablecer", "Restablecer", area, new Vector2(0.05f, 0.03f), new Vector2(0.33f, 0.12f), TipoBoton.Advertencia);
        reset.onClick.AddListener(RestablecerSeccion);

        Button volver = UIPuntaje.Boton("Volver", "Volver", area, new Vector2(0.67f, 0.03f), new Vector2(0.95f, 0.12f), TipoBoton.Primario);
        volver.onClick.AddListener(Cerrar);
    }

    // ------------------------------------------------------------------ ayudas de filas
    private RectTransform NuevaFila(RectTransform padre, string nombre, List<RectTransform> lista)
    {
        RectTransform r = UIPuntaje.Rect(nombre, padre, Vector2.zero, Vector2.one);
        if (lista != null) lista.Add(r);
        return r;
    }

    private void Etiqueta(RectTransform fila, string texto, float x0, float x1)
    {
        TextMeshProUGUI t = UIPuntaje.Texto("Etiqueta", fila, new Vector2(x0, 0f), new Vector2(x1, 1f), 12f, 36f,
                                            FontStyles.Bold, TextAlignmentOptions.MidlineLeft, cfg.colorTexto);
        t.text = texto;
    }

    private Slider FilaSlider(RectTransform fila, string etiqueta, float min, float max, float valor, out TextMeshProUGUI valorTxt)
    {
        Etiqueta(fila, etiqueta, 0f, 0.30f);
        Slider s = UIPuntaje.CrearSlider("Slider", fila, new Vector2(0.32f, 0.08f), new Vector2(0.88f, 0.92f), min, max, valor);
        valorTxt = UIPuntaje.Texto("Valor", fila, new Vector2(0.89f, 0f), new Vector2(1f, 1f), 12f, 34f,
                                   FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorOro);
        return s;
    }

    private Toggle FilaCasilla(RectTransform fila, string etiqueta)
    {
        Etiqueta(fila, etiqueta, 0f, 0.30f);
        return UIPuntaje.CrearCasilla("Casilla", fila, new Vector2(0.32f, 0.15f), new Vector2(0.385f, 0.85f), false);
    }

    private Button[] Segmentos(RectTransform fila, string etiqueta, string[] textos, Action<int> alElegir)
    {
        Etiqueta(fila, etiqueta, 0f, 0.30f);
        int n = textos.Length;
        Button[] bs = new Button[n];
        for (int i = 0; i < n; i++)
        {
            float x0 = 0.32f + 0.68f * (i / (float)n);
            float x1 = 0.32f + 0.68f * ((i + 1) / (float)n) - 0.012f;
            bs[i] = UIPuntaje.Boton("Seg" + i, textos[i], fila, new Vector2(x0, 0.08f), new Vector2(x1, 0.92f), TipoBoton.Secundario);
            int idx = i;
            bs[i].onClick.AddListener(() => alElegir(idx));
        }
        return bs;
    }

    private void MarcarSegmento(Button[] bs, int sel)
    {
        for (int i = 0; i < bs.Length; i++) PintarBoton(bs[i], i == sel);
    }

    private void PintarBoton(Button b, bool sel)
    {
        Image img = (Image)b.targetGraphic;
        TextMeshProUGUI txt = b.GetComponentInChildren<TextMeshProUGUI>();
        if (cfg.fondoBotonPrimario != null || cfg.fondoBotonSecundario != null)
        {
            img.color = sel ? Color.white : new Color(0.6f, 0.6f, 0.65f, 1f);
        }
        else
        {
            img.color = sel ? cfg.colorBotonPrimario : cfg.colorBotonSecundario;
            if (txt != null) txt.color = sel ? cfg.colorTextoBoton : Color.white;
        }
    }

    private static void Apilar(List<RectTransform> filas)
    {
        List<RectTransform> activas = filas.FindAll(f => f.gameObject.activeSelf);
        int n = Mathf.Max(1, activas.Count);
        float hueco = 0.014f;
        float alto = Mathf.Min(0.17f, 1f / n - hueco);

        for (int i = 0; i < activas.Count; i++)
        {
            float y1 = 1f - i * (alto + hueco);
            RectTransform r = activas[i];
            r.anchorMin = new Vector2(0f, y1 - alto);
            r.anchorMax = new Vector2(1f, y1);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }
    }

    // ================================================================== SONIDO
    private void ConstruirSonido(RectTransform c)
    {
        RectTransform f1 = UIPuntaje.Rect("Musica", c, new Vector2(0f, 0.70f), new Vector2(1f, 0.90f));
        FilaAudio(f1, "Música", true, out sMusica, out icoMusica, out pMusica);

        RectTransform f2 = UIPuntaje.Rect("Efectos", c, new Vector2(0f, 0.42f), new Vector2(1f, 0.62f));
        FilaAudio(f2, "Efectos", false, out sEfectos, out icoEfectos, out pEfectos);

        TextMeshProUGUI nota = UIPuntaje.Texto("Nota", c, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.3f), 12f, 28f,
                                               FontStyles.Italic, TextAlignmentOptions.Center,
                                               new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.6f));
        nota.text = "Toca el altavoz para silenciar o activar el sonido rápidamente.";
    }

    private void FilaAudio(RectTransform fila, string etiqueta, bool musica, out Slider slider, out Image icono, out TextMeshProUGUI porcentaje)
    {
        Image ico;
        Button b = UIPuntaje.BotonIcono("Altavoz", fila, new Vector2(0f, 0.06f), new Vector2(0.09f, 0.94f), out ico);
        icono = ico;

        Etiqueta(fila, etiqueta, 0.11f, 0.30f);

        TextMeshProUGUI pct;
        Slider s = FilaSlider2(fila, 0f, 1f, musica ? OpcionesJuego.VolumenMusica : OpcionesJuego.VolumenEfectos, out pct);
        slider = s;
        porcentaje = pct;

        s.onValueChanged.AddListener(v =>
        {
            if (musica)
            {
                OpcionesJuego.VolumenMusica = v;
                if (OpcionesJuego.SilencioMusica && v > 0f) OpcionesJuego.SilencioMusica = false;
            }
            else
            {
                OpcionesJuego.VolumenEfectos = v;
                if (OpcionesJuego.SilencioEfectos && v > 0f) OpcionesJuego.SilencioEfectos = false;
                Blip();
            }
            RefrescarAudio();
        });

        b.onClick.AddListener(() =>
        {
            if (musica)
            {
                OpcionesJuego.SilencioMusica = !OpcionesJuego.SilencioMusica;
                if (!OpcionesJuego.SilencioMusica && OpcionesJuego.VolumenMusica <= 0.01f) OpcionesJuego.VolumenMusica = 0.5f;
            }
            else
            {
                OpcionesJuego.SilencioEfectos = !OpcionesJuego.SilencioEfectos;
                if (!OpcionesJuego.SilencioEfectos && OpcionesJuego.VolumenEfectos <= 0.01f) OpcionesJuego.VolumenEfectos = 0.5f;
            }
            RefrescarAudio();
        });
    }

    // Slider de la fila de audio (sin etiqueta, porque el icono ocupa el primer lugar)
    private Slider FilaSlider2(RectTransform fila, float min, float max, float valor, out TextMeshProUGUI valorTxt)
    {
        Slider s = UIPuntaje.CrearSlider("Slider", fila, new Vector2(0.32f, 0.08f), new Vector2(0.88f, 0.92f), min, max, valor);
        valorTxt = UIPuntaje.Texto("Valor", fila, new Vector2(0.89f, 0f), new Vector2(1f, 1f), 12f, 34f,
                                   FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorOro);
        return s;
    }

    private void RefrescarAudio()
    {
        sMusica.SetValueWithoutNotify(OpcionesJuego.VolumenMusica);
        sEfectos.SetValueWithoutNotify(OpcionesJuego.VolumenEfectos);

        UIPuntaje.PonerIconoAltavoz(icoMusica, !OpcionesJuego.SilencioMusica);
        UIPuntaje.PonerIconoAltavoz(icoEfectos, !OpcionesJuego.SilencioEfectos);

        pMusica.text = OpcionesJuego.SilencioMusica ? "OFF" : Mathf.RoundToInt(OpcionesJuego.VolumenMusica * 100f) + "%";
        pEfectos.text = OpcionesJuego.SilencioEfectos ? "OFF" : Mathf.RoundToInt(OpcionesJuego.VolumenEfectos * 100f) + "%";
    }

    private void Blip()
    {
        if (Time.unscaledTime < proximoBlip) return;
        proximoBlip = Time.unscaledTime + 0.12f;
        UIPuntaje.Sonar(null, ProceduralSfx.Nota(3), 1.3f, 0.5f);
    }

    // ================================================================== CONTROLES
    private void ConstruirControles(RectTransform c)
    {
        contPC = UIPuntaje.Rect("PC", c, Vector2.zero, Vector2.one);
        contMovil = UIPuntaje.Rect("Movil", c, Vector2.zero, Vector2.one);
        ConstruirControlesPC(contPC);
        ConstruirControlesMovil(contMovil);
    }

    // ------------------------------------------------------------------ PC
    private void ConstruirControlesPC(RectTransform c)
    {
        RectTransform f1 = UIPuntaje.Rect("Sensibilidad", c, new Vector2(0f, 0.87f), new Vector2(1f, 1f));
        sMouse = FilaSlider(f1, "Sensibilidad del ratón", 0.3f, 3f, ControlSettings.SensibilidadMouse, out vMouse);
        sMouse.onValueChanged.AddListener(v => { ControlSettings.SensibilidadMouse = v; RefrescarControles(); });

        RectTransform f2 = UIPuntaje.Rect("Invertir", c, new Vector2(0f, 0.73f), new Vector2(1f, 0.855f));
        tInvertir = FilaCasilla(f2, "Invertir eje vertical");
        tInvertir.onValueChanged.AddListener(v => ControlSettings.InvertirY = v);

        TextMeshProUGUI cab = UIPuntaje.Texto("Cabecera", c, new Vector2(0f, 0.645f), new Vector2(1f, 0.715f), 12f, 30f,
                                              FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorBorde, true);
        cab.text = "TECLAS   (toca una para cambiarla; Esc cancela)";

        float alturaTotal = 0.63f;
        float fila = alturaTotal / 7f;
        for (int i = 0; i < OrdenTeclas.Length; i++)
        {
            AccionTecla accion = OrdenTeclas[i];
            int col = i / 7;
            int r = i % 7;
            float x0 = col * 0.52f;
            float y1 = alturaTotal - r * fila;
            float y0 = y1 - fila + 0.008f;

            TextMeshProUGUI et = UIPuntaje.Texto("Accion_" + accion, c, new Vector2(x0, y0), new Vector2(x0 + 0.25f, y1), 11f, 30f,
                                                 FontStyles.Normal, TextAlignmentOptions.MidlineLeft, cfg.colorTexto);
            et.text = TeclasJuego.NombreAccion(accion);

            Button b = UIPuntaje.Boton("Tecla_" + accion, "", c, new Vector2(x0 + 0.26f, y0 + 0.004f), new Vector2(x0 + 0.48f, y1 - 0.004f), TipoBoton.Secundario);
            AccionTecla a = accion;
            b.onClick.AddListener(() => EmpezarCaptura(a));
            botonesTecla[accion] = b;
        }
    }

    private void EmpezarCaptura(AccionTecla a)
    {
        if (frameFinCaptura == Time.frameCount) return;   // el mismo Enter/Espacio que acaba de asignar la tecla

        CancelarCaptura();
        capturando = (int)a;
        Capturando = true;
        TeclasJuego.Bloqueado = true;
        UIPuntaje.PonerTexto(botonesTecla[a], "Pulsa una tecla...");
    }

    private void CancelarCaptura()
    {
        if (capturando < 0 && !Capturando) return;
        capturando = -1;
        Capturando = false;
        TeclasJuego.Bloqueado = false;
        frameFinCaptura = Time.frameCount;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        RefrescarTeclas();
    }

    private void RefrescarTeclas()
    {
        foreach (KeyValuePair<AccionTecla, Button> kv in botonesTecla)
            UIPuntaje.PonerTexto(kv.Value, TeclasJuego.Nombre(TeclasJuego.Obtener(kv.Key)));
    }

    private void Update()
    {
        if (capturando < 0) return;

        int codigo;
        if (TeclasJuego.CapturarPulsacion(out codigo))
        {
            if (codigo != (int)Key.Escape)
            {
                AccionTecla a = (AccionTecla)capturando;
                // Para asignar una tecla a una accion se debe poder leer: se libera el bloqueo antes de guardar
                TeclasJuego.Asignar(a, codigo);
            }
            CancelarCaptura();
        }
    }

    // ------------------------------------------------------------------ movil
    private void ConstruirControlesMovil(RectTransform c)
    {
        RectTransform f1 = NuevaFila(c, "Camara", filasMovil);
        segCamara = Segmentos(f1, "Control de cámara", new[] { "Joystick", "Giroscopio", "Arrastrar" }, i =>
        {
            ControlSettings.Modo = (ModoCamara)i;
            MarcarSegmento(segCamara, i);
        });

        RectTransform f2 = NuevaFila(c, "SensStick", filasMovil);
        sStick = FilaSlider(f2, "Sensibilidad de cámara", 0.3f, 3f, ControlSettings.SensibilidadStick, out vStick);
        sStick.onValueChanged.AddListener(v => { ControlSettings.SensibilidadStick = v; RefrescarControles(); });

        RectTransform f3 = NuevaFila(c, "SensGiro", filasMovil);
        sGiro = FilaSlider(f3, "Sensibilidad del giroscopio", 0.3f, 3f, ControlSettings.SensibilidadGiro, out vGiro);
        sGiro.onValueChanged.AddListener(v => { ControlSettings.SensibilidadGiro = v; RefrescarControles(); });

        RectTransform f4 = NuevaFila(c, "Tamano", filasMovil);
        sTam = FilaSlider(f4, "Tamaño de controles", 0.6f, 1.6f, ControlSettings.EscalaControles, out vTam);
        sTam.onValueChanged.AddListener(v => { ControlSettings.EscalaControles = v; RefrescarControles(); });

        RectTransform f5 = NuevaFila(c, "Opacidad", filasMovil);
        sOpac = FilaSlider(f5, "Transparencia", 0.2f, 1f, ControlSettings.OpacidadControles, out vOpac);
        sOpac.onValueChanged.AddListener(v => { ControlSettings.OpacidadControles = v; RefrescarControles(); });

        RectTransform f6 = NuevaFila(c, "Vibracion", filasMovil);
        tVibrar = FilaCasilla(f6, "Vibración");
        tVibrar.onValueChanged.AddListener(v => OpcionesJuego.Vibrar = v);

        RectTransform f7 = NuevaFila(c, "Personalizar", filasMovil);
        Button pers = UIPuntaje.Boton("Personalizar", "Personalizar posición de los controles", f7,
                                      new Vector2(0.15f, 0.06f), new Vector2(0.85f, 0.94f), TipoBoton.Primario);
        pers.onClick.AddListener(() => EditorControlesTactiles.Abrir(null));
    }

    private void RefrescarControles()
    {
        vMouse.text = Mathf.RoundToInt(ControlSettings.SensibilidadMouse * 100f) + "%";
        vStick.text = Mathf.RoundToInt(ControlSettings.SensibilidadStick * 100f) + "%";
        vGiro.text = Mathf.RoundToInt(ControlSettings.SensibilidadGiro * 100f) + "%";
        vTam.text = Mathf.RoundToInt(ControlSettings.EscalaControles * 100f) + "%";
        vOpac.text = Mathf.RoundToInt(ControlSettings.OpacidadControles * 100f) + "%";
    }

    // ================================================================== PANTALLA
    private void ConstruirPantalla(RectTransform c)
    {
        filaPantallaCompleta = NuevaFila(c, "PantallaCompleta", filasPantalla);
        tPantalla = FilaCasilla(filaPantallaCompleta, "Pantalla completa");
        tPantalla.onValueChanged.AddListener(v => OpcionesJuego.PantallaCompleta = v);

        RectTransform f2 = NuevaFila(c, "Brillo", filasPantalla);
        sBrillo = FilaSlider(f2, "Brillo", 0.6f, 1.4f, OpcionesJuego.Brillo, out vBrillo);
        sBrillo.onValueChanged.AddListener(v => { OpcionesJuego.Brillo = v; RefrescarPantalla(); });

        RectTransform f3 = NuevaFila(c, "Calidad", filasPantalla);
        segCalidad = Segmentos(f3, "Calidad gráfica", new[] { "Auto", "Baja", "Media", "Alta" }, i =>
        {
            OpcionesJuego.Calidad = i - 1;
            RefrescarPantalla();
        });

        RectTransform f4 = NuevaFila(c, "Fps", filasPantalla);
        segFps = Segmentos(f4, "Límite de FPS", new[] { "30", "60", "Sin límite" }, i =>
        {
            OpcionesJuego.FpsLimite = i == 0 ? 30 : (i == 1 ? 60 : 0);
            RefrescarPantalla();
        });

        infoCalidad = UIPuntaje.Texto("Info", c, new Vector2(0.05f, 0f), new Vector2(0.95f, 0.1f), 11f, 26f,
                                      FontStyles.Italic, TextAlignmentOptions.Center,
                                      new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.6f));
    }

    private void RefrescarPantalla()
    {
        tPantalla.SetIsOnWithoutNotify(OpcionesJuego.PantallaCompleta);
        sBrillo.SetValueWithoutNotify(OpcionesJuego.Brillo);
        vBrillo.text = Mathf.RoundToInt(OpcionesJuego.Brillo * 100f) + "%";

        MarcarSegmento(segCalidad, OpcionesJuego.Calidad + 1);
        int fps = OpcionesJuego.FpsLimite;
        MarcarSegmento(segFps, fps == 30 ? 0 : (fps == 60 ? 1 : 2));

        infoCalidad.text = "Calidad aplicada: " + OpcionesJuego.NombreCalidadActual() +
                           (OpcionesJuego.Calidad < 0 ? "  (elegida automáticamente para tu equipo)" : "");
    }

    // ================================================================== general
    private void Refrescar()
    {
        RefrescarAudio();
        RefrescarControles();
        RefrescarPantalla();
        RefrescarTeclas();

        tInvertir.SetIsOnWithoutNotify(ControlSettings.InvertirY);
        tVibrar.SetIsOnWithoutNotify(OpcionesJuego.Vibrar);
        sMouse.SetValueWithoutNotify(ControlSettings.SensibilidadMouse);
        sStick.SetValueWithoutNotify(ControlSettings.SensibilidadStick);
        sGiro.SetValueWithoutNotify(ControlSettings.SensibilidadGiro);
        sTam.SetValueWithoutNotify(ControlSettings.EscalaControles);
        sOpac.SetValueWithoutNotify(ControlSettings.OpacidadControles);
        MarcarSegmento(segCamara, (int)ControlSettings.Modo);
    }

    private void SeleccionarTab(int i)
    {
        CancelarCaptura();
        tabActual = Mathf.Clamp(i, 0, Secciones - 1);

        for (int k = 0; k < Secciones; k++)
        {
            contenidos[k].gameObject.SetActive(k == tabActual);
            PintarBoton(tabs[k], k == tabActual);
        }

        if (tabActual == 1)
        {
            bool tactil = ControlesJugador.TactilActivo;
            contPC.gameObject.SetActive(!tactil);
            contMovil.gameObject.SetActive(tactil);
            if (tactil) Apilar(filasMovil);
        }
        else if (tabActual == 2)
        {
            filaPantallaCompleta.gameObject.SetActive(!Application.isMobilePlatform);   // solo en PC
            Apilar(filasPantalla);
        }
    }

    private void RestablecerSeccion()
    {
        switch (tabActual)
        {
            case 0: OpcionesJuego.RestablecerSonido(); break;
            case 1:
                if (ControlesJugador.TactilActivo) { ControlSettings.RestablecerTactil(); OpcionesJuego.Vibrar = true; }
                else ControlSettings.RestablecerPC();
                break;
            default: OpcionesJuego.RestablecerPantalla(); break;
        }
        Refrescar();
        UIPuntaje.Sonar(null, ProceduralSfx.Nota(1), 1f, 0.5f);
    }
}
