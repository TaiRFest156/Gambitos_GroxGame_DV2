using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tabla de puntajes con pestanas por nivel. Se usa dentro de la pantalla de puntaje
// y tambien sola (boton "Puntajes" del menu, ver RankingMenu).
public class PanelRanking : MonoBehaviour
{
    public static PanelRanking Crear(Transform padre, Vector2 min, Vector2 max, Action alCerrar, string textoCerrar = "Volver")
    {
        RectTransform rt = UIPuntaje.Rect("PanelRanking", padre, min, max);
        PanelRanking p = rt.gameObject.AddComponent<PanelRanking>();
        p.Construir(alCerrar, textoCerrar);
        rt.gameObject.SetActive(false);
        return p;
    }

    private class Fila
    {
        public Image fondo;
        public TextMeshProUGUI puesto, nombre, puntaje, fecha;
        public Image[] estrellas = new Image[3];
    }

    private const int Niveles = 3;

    private Action alCerrar;
    private Button[] tabs = new Button[Niveles];
    private Fila[] filas;
    private TextMeshProUGUI titulo, vacio;
    private int nivelActual = 1;
    private string idDestacado;
    private int solicitud;   // evita que una respuesta tardia (backend online) pise a una mas nueva

    public void Abrir(int nivel, string destacar = null)
    {
        idDestacado = destacar;
        gameObject.SetActive(true);
        Seleccionar(Mathf.Clamp(nivel, 1, Niveles));
    }

    public void Cerrar()
    {
        gameObject.SetActive(false);
        if (alCerrar != null) alCerrar();
    }

    // ================================================================== construccion
    private void Construir(Action cerrar, string textoCerrar)
    {
        alCerrar = cerrar;
        PuntajeConfig cfg = UIPuntaje.Config;

        RectTransform area = UIPuntaje.Panel("Fondo", transform, Vector2.zero, Vector2.one);

        titulo = UIPuntaje.Texto("Titulo", area, new Vector2(0.12f, 0.88f), new Vector2(0.88f, 0.98f), 30f, 64f,
                                 FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTitulo, true);
        titulo.text = "RANKING LOCAL";

        Button x = UIPuntaje.BotonCerrar(area);
        x.onClick.AddListener(Cerrar);

        // Pestanas por nivel
        for (int i = 0; i < Niveles; i++)
        {
            float x0 = 0.06f + i * 0.31f;
            int nivel = i + 1;
            tabs[i] = UIPuntaje.Boton("Tab" + nivel, "Nivel " + nivel, area, new Vector2(x0, 0.775f),
                                      new Vector2(x0 + 0.29f, 0.865f), TipoBoton.Secundario);
            tabs[i].onClick.AddListener(() => Seleccionar(nivel));
        }

        // Encabezados de columnas
        Color dim = new Color(cfg.colorTexto.r, cfg.colorTexto.g, cfg.colorTexto.b, 0.6f);
        Encabezado(area, "#", 0.06f, 0.14f, TextAlignmentOptions.Center, dim);
        Encabezado(area, "JUGADOR", 0.16f, 0.50f, TextAlignmentOptions.MidlineLeft, dim);
        Encabezado(area, "ESTRELLAS", 0.50f, 0.65f, TextAlignmentOptions.Center, dim);
        Encabezado(area, "PUNTAJE", 0.65f, 0.83f, TextAlignmentOptions.MidlineRight, dim);
        Encabezado(area, "FECHA", 0.84f, 0.95f, TextAlignmentOptions.Center, dim);

        // Filas
        int n = Mathf.Clamp(cfg.filasVisibles, 5, 10);
        filas = new Fila[n];
        float top = 0.725f, bottom = 0.155f;
        float alto = (top - bottom) / n;
        for (int i = 0; i < n; i++)
        {
            float y1 = top - i * alto;
            float y0 = y1 - alto + 0.004f;
            Fila f = new Fila();
            Color par = new Color(1f, 1f, 1f, 0.07f), impar = new Color(1f, 1f, 1f, 0.03f);
            f.fondo = UIPuntaje.Img("Fila" + i, area, new Vector2(0.05f, y0), new Vector2(0.95f, y1),
                                    i % 2 == 0 ? par : impar, UIPuntaje.RectRedondeado(), true);
            RectTransform fr = f.fondo.rectTransform;

            f.puesto = UIPuntaje.Texto("Puesto", fr, new Vector2(0.01f, 0f), new Vector2(0.10f, 1f), 14f, 36f,
                                       FontStyles.Bold, TextAlignmentOptions.Center, cfg.colorTexto);
            f.nombre = UIPuntaje.Texto("Nombre", fr, new Vector2(0.12f, 0f), new Vector2(0.49f, 1f), 14f, 34f,
                                       FontStyles.Bold, TextAlignmentOptions.MidlineLeft, cfg.colorTexto);
            for (int k = 0; k < 3; k++)
            {
                float sx = 0.505f + k * 0.047f;
                f.estrellas[k] = UIPuntaje.Img("Estrella" + k, fr, new Vector2(sx, 0.15f), new Vector2(sx + 0.045f, 0.85f),
                                               Color.white, null, false);
            }
            f.puntaje = UIPuntaje.Texto("Puntaje", fr, new Vector2(0.65f, 0f), new Vector2(0.83f, 1f), 14f, 34f,
                                        FontStyles.Bold, TextAlignmentOptions.MidlineRight, cfg.colorOro);
            f.fecha = UIPuntaje.Texto("Fecha", fr, new Vector2(0.84f, 0f), new Vector2(0.99f, 1f), 10f, 24f,
                                      FontStyles.Normal, TextAlignmentOptions.Center, dim);
            filas[i] = f;
        }

        vacio = UIPuntaje.Texto("Vacio", area, new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.58f), 18f, 44f,
                                FontStyles.Bold, TextAlignmentOptions.Center, dim);
        vacio.text = "Todavía no hay puntajes en este nivel.\n¡Sé el primero en guardar el tuyo!";

        TextMeshProUGUI nota = UIPuntaje.Texto("Nota", area, new Vector2(0.05f, 0.105f), new Vector2(0.95f, 0.15f), 10f, 22f,
                                               FontStyles.Italic,
                                               TextAlignmentOptions.Center, dim);
        nota.text = "Los puntajes se guardan en este dispositivo";

        Button volver = UIPuntaje.Boton("Volver", textoCerrar, area, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.1f), TipoBoton.Secundario);
        volver.onClick.AddListener(Cerrar);
    }

    private void Encabezado(Transform padre, string texto, float x0, float x1, TextAlignmentOptions alin, Color c)
    {
        TextMeshProUGUI t = UIPuntaje.Texto("H_" + texto, padre, new Vector2(x0, 0.728f), new Vector2(x1, 0.768f), 10f, 22f,
                                            FontStyles.Bold, alin, c);
        t.text = texto;
    }

    // ================================================================== datos
    private void Seleccionar(int nivel)
    {
        nivelActual = nivel;
        PuntajeConfig cfg = UIPuntaje.Config;

        for (int i = 0; i < Niveles; i++)
        {
            bool sel = (i + 1) == nivel;
            Image img = (Image)tabs[i].targetGraphic;
            TextMeshProUGUI txt = tabs[i].GetComponentInChildren<TextMeshProUGUI>();

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

        titulo.text = "RANKING LOCAL  -  NIVEL " + nivel;
        Refrescar();
    }

    private void Refrescar()
    {
        int mia = ++solicitud;
        vacio.text = "Cargando...";
        vacio.gameObject.SetActive(true);
        foreach (Fila f in filas) f.fondo.gameObject.SetActive(false);

        RankingService.Obtener(nivelActual, filas.Length, lista =>
        {
            if (mia != solicitud || this == null) return;   // llego tarde
            Mostrar(lista);
        });
    }

    private void Mostrar(List<RegistroPuntaje> lista)
    {
        PuntajeConfig cfg = UIPuntaje.Config;

        vacio.text = "Todavía no hay puntajes en este nivel.\n¡Sé el primero en guardar el tuyo!";
        vacio.gameObject.SetActive(lista == null || lista.Count == 0);

        for (int i = 0; i < filas.Length; i++)
        {
            Fila f = filas[i];
            bool hay = lista != null && i < lista.Count;
            f.fondo.gameObject.SetActive(hay);
            if (!hay) continue;

            RegistroPuntaje r = lista[i];
            bool mio = !string.IsNullOrEmpty(idDestacado) && r.id == idDestacado;

            f.puesto.text = (i + 1).ToString();
            f.puesto.color = i == 0 ? cfg.colorOro
                           : i == 1 ? new Color(0.82f, 0.86f, 0.92f)
                           : i == 2 ? new Color(0.85f, 0.55f, 0.3f)
                           : cfg.colorTexto;

            f.nombre.text = r.nombre;
            f.nombre.color = mio ? cfg.colorOro : cfg.colorTexto;
            f.puntaje.text = r.puntaje.ToString("N0", CultureInfo.CurrentCulture);

            for (int k = 0; k < 3; k++) UIPuntaje.PonerEstrella(f.estrellas[k], k < r.estrellas);

            string fecha = "";
            try { fecha = DateTimeOffset.FromUnixTimeSeconds(r.fecha).ToLocalTime().ToString("dd/MM/yy"); } catch (Exception) { }
            f.fecha.text = fecha;

            Color baseFila = i % 2 == 0 ? new Color(1f, 1f, 1f, 0.07f) : new Color(1f, 1f, 1f, 0.03f);
            f.fondo.color = mio ? new Color(cfg.colorOro.r, cfg.colorOro.g, cfg.colorOro.b, 0.28f) : baseFila;
        }
    }
}
