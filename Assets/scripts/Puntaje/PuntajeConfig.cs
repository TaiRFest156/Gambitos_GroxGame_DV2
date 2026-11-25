using TMPro;
using UnityEngine;

// Configuracion y ESTILO de todas las pantallas de puntaje y de ranking.
// Crea el asset con el menu  Grox > Puntaje > Crear configuracion  (queda en Assets/Resources/PuntajeConfig).
// Todo lo que dejes vacio usa el estilo por defecto dibujado por codigo.
[CreateAssetMenu(fileName = "PuntajeConfig", menuName = "Grox/Configuracion de puntaje")]
public class PuntajeConfig : ScriptableObject
{
    [Header("Navegacion (nombres de escena en Build Settings)")]
    public string escenaSeleccion = "LevelSelector";
    public string escenaMenu = "Menu";

    [Header("Fuentes (vacio = la fuente por defecto de TextMeshPro)")]
    public TMP_FontAsset fuenteTitulos;
    public TMP_FontAsset fuenteTextos;

    [Header("Estrellas (vacio = estrella dibujada por codigo)")]
    public Sprite estrellaLlena;
    public Sprite estrellaVacia;

    [Header("Imagenes de la interfaz (vacio = estilo por defecto). Los rectangulos deben ser 9-slice.")]
    public Sprite fondoPanel;
    public Sprite fondoBotonPrimario;
    public Sprite fondoBotonSecundario;
    public Sprite fondoBotonCerrar;
    public Sprite fondoCampoTexto;

    [Header("Colores")]
    public Color colorPanel = new Color32(24, 28, 52, 245);
    public Color colorBorde = new Color32(46, 230, 166, 255);
    public Color colorTitulo = new Color32(255, 255, 255, 255);
    public Color colorTexto = new Color32(225, 230, 245, 255);
    public Color colorOro = new Color32(255, 214, 10, 255);
    public Color colorBotonPrimario = new Color32(46, 230, 166, 255);
    public Color colorBotonSecundario = new Color32(70, 140, 255, 255);
    public Color colorBotonAdvertencia = new Color32(255, 150, 70, 255);
    public Color colorTextoBoton = new Color32(24, 28, 52, 255);
    public Color colorExito = new Color32(60, 210, 120, 255);
    public Color colorError = new Color32(255, 90, 90, 255);

    [Header("Sonidos (vacio = sonido generado por codigo)")]
    public AudioClip sonidoEstrella;
    public AudioClip sonidoPanel;
    public AudioClip sonidoBoton;
    public AudioClip sonidoGuardar;
    public AudioClip sonidoRecord;

    [Header("Ranking")]
    [Tooltip("Cuantos puntajes se guardan por nivel en este dispositivo")]
    public int maxRegistrosPorNivel = 50;
    [Tooltip("Filas que muestra la tabla")]
    [Range(5, 10)] public int filasVisibles = 10;
    public int maxCaracteresNombre = 12;

    [Header("Pausa y opciones: imagenes (vacio = dibujado por codigo)")]
    [Tooltip("Imagen del boton de pausa (si la pones, reemplaza al dibujo por defecto)")]
    public Sprite iconoBotonPausa;
    public Sprite iconoAltavozActivo;
    public Sprite iconoAltavozSilenciado;
    public Sprite fondoSlider;
    public Sprite rellenoSlider;
    public Sprite perillaSlider;
    public Sprite fondoCasilla;
    public Sprite marcaCasilla;

    [Header("Pausa y opciones: textos")]
    public string tituloPausa = "EN PAUSA";
    public string textoContinuar = "Continuar";
    public string textoReiniciar = "Reiniciar nivel";
    public string textoOpciones = "Opciones";
    public string textoSalirMenu = "Salir al menú principal";
    public string tituloOpciones = "OPCIONES";

    private static PuntajeConfig actual;

    public static PuntajeConfig Actual
    {
        get
        {
            if (actual == null)
            {
                actual = Resources.Load<PuntajeConfig>("PuntajeConfig");
                if (actual == null) actual = CreateInstance<PuntajeConfig>();   // valores por defecto
            }
            return actual;
        }
    }
}
