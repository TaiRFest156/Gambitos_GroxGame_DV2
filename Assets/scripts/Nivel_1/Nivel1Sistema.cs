using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reglas del Nivel 1 que se agregan al juego actual:
//  - Alcance de la pistola y energia del blaster
//  - Contaminacion del planeta (niebla, barra, derrota al 100%)
//  - Combos con puntos extra
// Se crea solo (lo pide SpawnBasura). Ajusta los valores en el objeto "Nivel1Sistema" que aparece al jugar,
// o agrega este script a un objeto de la escena para dejar los valores guardados.
public class Nivel1Sistema : MonoBehaviour
{
    public static Nivel1Sistema Instance { get; private set; }

    public static Color ColorContaminacionActual
    {
        get { return Instance != null ? Instance.colorContaminacion : new Color(0.45f, 0.5f, 0.3f, 1f); }
    }

    public static Nivel1Sistema Asegurar()
    {
        if (Instance != null) return Instance;
        Nivel1Sistema ya = FindFirstObjectByType<Nivel1Sistema>();
        if (ya != null) return ya;
        return new GameObject("Nivel1Sistema").AddComponent<Nivel1Sistema>();
    }

    [System.Serializable]
    public class TramoCombo
    {
        [Tooltip("Desde cuantas capturas seguidas aplica")] public int desde = 1;
        public float multiplicador = 1f;
        public TramoCombo() { }
        public TramoCombo(int d, float m) { desde = d; multiplicador = m; }
    }

    // ------------------------------------------------------------------ configuracion
    [Header("Alcance de la pistola")]
    [Tooltip("Distancia maxima (en metros) a la que la pistola acierta. El puntero cambia de color segun si llegas o no.")]
    public float rangoDisparo = 14f;

    [Header("Energia del blaster")]
    public float energiaMaxima = 100f;
    [Tooltip("Energia que gasta cada disparo")]
    public float costoDisparo = 5f;
    [Tooltip("Recarga lenta natural (red de seguridad para no quedarte sin poder disparar a las esferas). 0 = sin recarga natural.")]
    public float regenPorSegundo = 3f;
    public float retrasoRegen = 1.2f;
    [Tooltip("Energia que da cada esfera")]
    public float energiaPorEsfera = 30f;

    [Header("Contaminacion del planeta")]
    [Tooltip("% que sube la contaminacion cada vez que un residuo toca el suelo y se desvanece")]
    public float porResiduoCaido = 3f;
    [Tooltip("% que baja con cada residuo capturado")]
    public float limpiezaPorCaptura = 0.4f;
    [Tooltip("Segundos que un residuo espera en el suelo antes de desvanecerse (puedes rescatarlo con la pistola)")]
    public float segundosAntesDeDesvanecer = 1.5f;
    [Tooltip("Cuanto contamina cada tipo respecto al valor base")]
    public float pesoToxico = 2f;
    public float pesoPlastico = 1.3f;
    public float pesoMetal = 1.2f;
    public float pesoVidrio = 1f;
    public float pesoPapel = 0.8f;
    public float pesoOrganico = 0.6f;
    public Color colorContaminacion = new Color(0.45f, 0.5f, 0.3f, 1f);
    [Tooltip("Densidad de la niebla del escenario al 100% de contaminacion")]
    public float densidadNieblaMaxima = 0.035f;
    [Tooltip("La luz principal se apaga hasta esta fraccion de su intensidad al 100%")]
    [Range(0.3f, 1f)] public float intensidadLuzMinima = 0.65f;
    public bool alterarNiebla = true;
    public bool alterarLuz = true;
    public string escenaDerrota = "Derrota";

    [Header("Combo")]
    [Tooltip("Segundos para capturar el siguiente residuo antes de perder el combo")]
    public float ventanaCombo = 2.5f;
    [Tooltip("Si esta activo, un disparo que no acierta rompe el combo")]
    public bool fallarRompeCombo = false;
    [Tooltip("Segundos extra que da una esfera de energia al combo (no cuenta como captura ni lo rompe)")]
    public float segundosExtraPorEsfera = 1f;
    public List<TramoCombo> tramosCombo = new List<TramoCombo>
    {
        new TramoCombo(1, 1f), new TramoCombo(3, 1.25f), new TramoCombo(5, 1.5f), new TramoCombo(8, 2f), new TramoCombo(12, 2.5f)
    };

    // ------------------------------------------------------------------ estado
    public float EnergiaPct { get { return energia / Mathf.Max(1f, energiaMaxima) * 100f; } }
    public bool TieneEnergia { get { return energia >= costoDisparo; } }
    public float Contaminacion { get { return contaminacion; } }

    private float energia;
    private float ultimoDisparo = -99f;
    private float ultimoAvisoSinEnergia = -99f;
    private float ultimoToastSinEnergia = -99f;

    private float contaminacion;     // 0 a 100
    private float contVisible;       // 0 a 1 (suavizado)
    private readonly bool[] alertasDadas = new bool[3];
    private readonly float[] umbralesAlerta = { 50f, 75f, 90f };
    private bool terminado;

    private int combo;
    private float tiempoCombo;
    private float multiplicadorActual = 1f;

    // Mejor racha de la partida (para la pantalla de puntaje)
    public int MejorCombo { get; private set; }

    private Nivel1HUD hud;

    // originales del escenario (para restaurar)
    private bool fogOn;
    private Color fogColor;
    private float fogDensity;
    private FogMode fogMode;
    private Light sol;
    private float solIntensidad;
    private Color solColor;

    // ================================================================== inicio
    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;

        energia = energiaMaxima;

        if (FindFirstObjectByType<Nivel1Audio>() == null) gameObject.AddComponent<Nivel1Audio>();

        hud = FindFirstObjectByType<Nivel1HUD>();
        if (hud == null) hud = gameObject.AddComponent<Nivel1HUD>();
    }

    private void Start()
    {
        fogOn = RenderSettings.fog;
        fogColor = RenderSettings.fogColor;
        fogDensity = RenderSettings.fogDensity;
        fogMode = RenderSettings.fogMode;

        sol = RenderSettings.sun;
        if (sol == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sol = l; break; }
        }
        if (sol != null) { solIntensidad = sol.intensity; solColor = sol.color; }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            RenderSettings.fog = fogOn;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogMode = fogMode;
            if (sol != null) { sol.intensity = solIntensidad; sol.color = solColor; }
            Nivel1Audio.SetAmbiente(0f);
        }
    }

    // ================================================================== bucle
    private void Update()
    {
        if (Time.timeScale == 0f) return;
        float dt = Time.deltaTime;

        // Energia: recarga lenta si hace un rato que no disparas
        if (regenPorSegundo > 0f && Time.time - ultimoDisparo > retrasoRegen)
            energia = Mathf.Min(energiaMaxima, energia + regenPorSegundo * dt);
        hud.SetEnergia(energia / Mathf.Max(1f, energiaMaxima));

        // Combo: cuenta regresiva de la ventana
        if (combo > 0)
        {
            tiempoCombo -= dt;
            if (tiempoCombo <= 0f) RomperCombo();
            else hud.SetCombo(combo, multiplicadorActual, tiempoCombo / ventanaCombo);
        }

        ActualizarPuntero();
        ActualizarAmbiente(dt);
    }

    // ================================================================== puntero y disparo
    private void ActualizarPuntero()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        EstadoPuntero estado = EstadoPuntero.Neutral;
        if (!TieneEnergia)
        {
            estado = EstadoPuntero.SinEnergia;
        }
        else
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 60f) && EsObjetivo(hit.collider))
                estado = hit.distance <= rangoDisparo ? EstadoPuntero.EnRango : EstadoPuntero.FueraDeRango;
        }
        hud.SetPuntero(estado);
    }

    public static bool EsTagBasura(string t)
    {
        return t == "Metal" || t == "Plastico" || t == "Vidrio" || t == "Papel" || t == "Toxico" || t == "Organico";
    }

    public static bool EsObjetivo(Collider c)
    {
        if (c == null) return false;
        return EsTagBasura(c.tag) || c.GetComponentInParent<PowerUpOrb>() != null;
    }

    public void GastarEnergia()
    {
        energia = Mathf.Max(0f, energia - costoDisparo);
        ultimoDisparo = Time.time;
        hud.PunchEnergia();
    }

    // El jugador toca para disparar pero no hay energia
    public void SinEnergia()
    {
        if (Time.time - ultimoAvisoSinEnergia > 0.35f)
        {
            ultimoAvisoSinEnergia = Time.time;
            Nivel1Audio.Reproducir(SonidoN1.SinEnergia);
            hud.FlashSinEnergia();
        }
        if (Time.time - ultimoToastSinEnergia > 4f)
        {
            ultimoToastSinEnergia = Time.time;
            hud.Toast("¡Sin energía! Dispara a una esfera azul", new Color(1f, 0.55f, 0.2f));
        }
    }

    public void RecargarEnergia(Vector3 pos)
    {
        float antes = energia;
        energia = Mathf.Min(energiaMaxima, energia + energiaPorEsfera);
        int ganada = Mathf.RoundToInt(energia - antes);

        Nivel1Audio.Reproducir(SonidoN1.Recarga);
        hud.FlashRecarga();
        hud.TextoMundo("+" + ganada + "% ENERGÍA", pos, new Color(0.3f, 0.95f, 1f));

        // Un respiro para el combo: tomar la esfera no lo interrumpe ni cuenta como captura
        if (combo > 0) tiempoCombo = Mathf.Min(ventanaCombo, tiempoCombo + segundosExtraPorEsfera);
    }

    public void FueraDeRango()
    {
        Nivel1Audio.Reproducir(SonidoN1.FueraDeRango);
        hud.Toast("¡Muy lejos! Acércate", new Color(1f, 0.55f, 0.2f));
        if (fallarRompeCombo) RomperCombo();
    }

    public void RegistrarFallo()
    {
        Nivel1Audio.Reproducir(SonidoN1.Fallo);
        if (fallarRompeCombo) RomperCombo();
    }

    // ================================================================== captura y combo
    public void RegistrarCaptura(string tipo, Vector3 pos)
    {
        int valor = ReciclajeContador.valores.ContainsKey(tipo) ? ReciclajeContador.valores[tipo] : 0;

        float multAnterior = multiplicadorActual;
        combo++;
        if (combo > MejorCombo) MejorCombo = combo;
        tiempoCombo = ventanaCombo;
        multiplicadorActual = MultiplicadorPara(combo);

        int bonus = Mathf.RoundToInt(valor * (multiplicadorActual - 1f));
        if (bonus > 0) ReciclajeContador.AgregarBonus(bonus);
        ReciclajeContador.Registrar(tipo);   // actualiza la barra de objetivo

        // Un poco de limpieza al capturar
        contaminacion = Mathf.Max(0f, contaminacion - limpiezaPorCaptura);

        // Feedback
        float pitch = 1f + 0.05f * Mathf.Min(combo - 1, 10);
        Nivel1Audio.Reproducir(SonidoN1.Captura, pitch);
        if (combo >= 2) { hud.ComboSube(); hud.SetCombo(combo, multiplicadorActual, 1f); }

        string texto = "+" + valor;
        if (bonus > 0) texto += "\n<size=70%><color=#FFD60A>COMBO +" + bonus + "</color></size>";
        hud.TextoMundo(texto, pos, Color.white);

        if (multiplicadorActual > multAnterior + 0.001f)
        {
            Nivel1Audio.Reproducir(SonidoN1.Combo);
            hud.Toast("¡COMBO x" + multiplicadorActual.ToString("0.##") + "!", new Color(1f, 0.84f, 0.1f));
        }
    }

    private float MultiplicadorPara(int c)
    {
        float m = 1f;
        foreach (TramoCombo t in tramosCombo)
            if (c >= t.desde) m = t.multiplicador;
        return m;
    }

    private void RomperCombo()
    {
        if (combo >= 3)
        {
            Nivel1Audio.Reproducir(SonidoN1.ComboRoto);
            hud.Toast("Combo perdido", new Color(1f, 0.55f, 0.2f));
        }
        combo = 0;
        multiplicadorActual = 1f;
        hud.SetCombo(0, 1f, 0f);
    }

    // ================================================================== contaminacion
    // Un residuo toco el suelo y se desvanecio
    public void RegistrarCaida(string tipo, Vector3 pos)
    {
        if (terminado) return;

        float aporte = porResiduoCaido * PesoDe(tipo);
        contaminacion = Mathf.Min(100f, contaminacion + aporte);

        Nivel1Audio.Reproducir(SonidoN1.Suelo);
        hud.PunchContaminacion();
        hud.TextoMundo("+" + aporte.ToString("0.#") + "% contaminación", pos, new Color(0.75f, 0.85f, 0.4f));

        for (int i = 0; i < umbralesAlerta.Length; i++)
        {
            if (!alertasDadas[i] && contaminacion >= umbralesAlerta[i])
            {
                alertasDadas[i] = true;
                Nivel1Audio.Reproducir(SonidoN1.Alerta);
                hud.Toast("¡Contaminación al " + (int)umbralesAlerta[i] + "%!", new Color(1f, 0.4f, 0.3f));
            }
        }

        if (contaminacion >= 100f) StartCoroutine(Perder());
    }

    private float PesoDe(string tipo)
    {
        switch (tipo)
        {
            case "Toxico": return pesoToxico;
            case "Plastico": return pesoPlastico;
            case "Metal": return pesoMetal;
            case "Vidrio": return pesoVidrio;
            case "Papel": return pesoPapel;
            case "Organico": return pesoOrganico;
            default: return 1f;
        }
    }

    private void ActualizarAmbiente(float dt)
    {
        float objetivo = contaminacion / 100f;
        contVisible = Mathf.MoveTowards(contVisible, objetivo, dt * 0.4f);
        float p = contVisible;

        hud.SetContaminacion(objetivo, p);

        // Bordes de la pantalla
        hud.SetColorAmbiente(colorContaminacion);
        hud.SetAmbiente(Mathf.Clamp01((p - 0.15f) / 0.85f) * 0.55f, contaminacion >= 90f ? 1f : 0f);

        // Niebla del escenario
        if (alterarNiebla)
        {
            if (p > 0.01f)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = Color.Lerp(fogColor, colorContaminacion, Mathf.Clamp01(p * 1.5f));
                float baseDens = fogOn ? fogDensity : 0f;
                RenderSettings.fogDensity = Mathf.Lerp(baseDens, densidadNieblaMaxima, p * p);
            }
            else
            {
                RenderSettings.fog = fogOn;
            }
        }

        // La luz principal se apaga y se enferma
        if (alterarLuz && sol != null)
        {
            sol.intensity = Mathf.Lerp(solIntensidad, solIntensidad * intensidadLuzMinima, p);
            sol.color = Color.Lerp(solColor, Color.Lerp(solColor, colorContaminacion, 0.5f), p);
        }

        Nivel1Audio.SetAmbiente(p);
    }

    // El spawner avisa que termino el tiempo: ya no se puede perder por contaminacion
    public void NivelTerminado()
    {
        terminado = true;
    }

    private IEnumerator Perder()
    {
        if (terminado) yield break;
        terminado = true;

        SpawnBasura spawner = FindFirstObjectByType<SpawnBasura>();
        if (spawner != null) spawner.DetenerSpawner();

        hud.Toast("¡Contaminación total!", new Color(1f, 0.3f, 0.3f));
        Nivel1Audio.Reproducir(SonidoN1.ComboRoto);
        yield return new WaitForSecondsRealtime(1.8f);

        // Misma convencion que el resto del juego: la pantalla de derrota sabe a que nivel volver
        Time.timeScale = 1f;
        PlayerPrefs.SetString("EscenaAnterior", SceneManager.GetActiveScene().name);
        SceneManager.LoadScene(escenaDerrota);
    }
}
