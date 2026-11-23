using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class ResidueLevelManager : MonoBehaviour
{
    // ====== Originales (conectados en el Inspector: no cambiar los nombres) ======
    [Header("Residues")]
    public ResidueData[] residuePool;
    public Transform spawnPoint;
    public float launchSpeed = 5f;

    [Header("Target Points (3D world)")]
    public Transform targetLeft;
    public Transform targetRight;
    public Transform targetUp;

    [Header("Gameplay")]
    public float levelDuration = 30f;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;

    public ParticleSystem residueParticles;
    public VictoryManager victoryManager;
    public DefeatManager defeatManager;

    // ====== Nuevos ======
    [Header("Meta y derrota")]
    [Tooltip("Puntaje para ganar (antes estaba escrito en 3 scripts distintos)")]
    public int metaPuntaje = 1050;
    [Tooltip("Si esta activo, perder 4 errores seguidos termina el nivel (regla de DefeatManager)")]
    public bool derrotaPorErroresSeguidos = true;

    [Header("Puntaje")]
    [Tooltip("Escena a la que lleva 'Siguiente nivel' (vacio = sin boton)")]
    public string escenaSiguiente = "Nivel_3";
    [Tooltip("Estrellas: multiplos de la meta de puntaje. 1 estrella = alcanzar la meta.")]
    public float umbralDosEstrellas = 1.25f;
    public float umbralTresEstrellas = 1.5f;

    [Header("Inicio")]
    [Tooltip("Segundos de cuenta regresiva antes de que corra el tiempo")]
    public int cuentaRegresiva = 3;

    [Header("Residuos")]
    [Tooltip("Tamano general de los residuos (1 = el del prefab). Cada ResidueData tiene ademas su propia escala.")]
    public float escalaResiduos = 0.6f;
    [Tooltip("Al volar hacia el contenedor se achican a esta fraccion de su tamano")]
    [Range(0.3f, 1f)] public float escalaAlLlegar = 0.6f;
    [Tooltip("Pausa despues de cada lanzamiento, antes de que aparezca el siguiente residuo")]
    public float esperaEntreResiduos = 0.45f;

    [Header("Trayectoria")]
    [Tooltip("El residuo vuela hasta donde se VE el contenedor en pantalla (recomendado). Si lo desmarcas, usa los puntos 'Target' 3D de antes.")]
    public bool apuntarAlContenedor = true;
    [Tooltip("Camara que dibuja los residuos. Vacio = la camara principal.")]
    public Camera camaraJuego;

    [Header("Contenedores (las imagenes del Canvas)")]
    public Graphic contenedorPapel;
    public Graphic contenedorVidrio;
    public Graphic contenedorPlastico;

    [Header("Contenedores extra y sorteo (5 disponibles, 3 por partida)")]
    [Tooltip("Imagen del contenedor marron de residuos organicos (opcional)")]
    public Graphic contenedorOrganico;
    [Tooltip("Imagen del contenedor rojo de residuos peligrosos (opcional)")]
    public Graphic contenedorPeligroso;
    [Tooltip("Al empezar el nivel se sortean 3 de los contenedores disponibles")]
    public bool sortearContenedores = true;
    [Tooltip("Ademas del sorteo, se mezclan sus posiciones (izquierda / arriba / derecha)")]
    public bool mezclarPosiciones = true;
    [Tooltip("Evita que toquen los mismos 3 contenedores dos veces seguidas")]
    public bool evitarRepetirSorteo = true;
    [Tooltip("Opcional: posiciones de los 3 lugares. Vacio = las posiciones actuales de los contenedores de papel (izq.), vidrio (arriba) y plastico (der.)")]
    public RectTransform slotIzquierda;
    public RectTransform slotArriba;
    public RectTransform slotDerecha;

    [Header("Control de inclinacion")]
    [Tooltip("Cuanto hay que inclinar (0.30 = unos 17 grados). Mas alto = menos sensible.")]
    public float umbralInclinacion = 0.30f;
    [Tooltip("Segundos que hay que sostener la inclinacion para lanzar. Evita roces involuntarios.")]
    public float tiempoConfirmar = 0.10f;
    [Tooltip("Que tan cerca del centro hay que volver para poder lanzar otro")]
    public float zonaMuerta = 0.12f;
    [Tooltip("Si inclinar a un lado lanza al lado contrario, activa esto")]
    public bool invertirLateral;
    public bool invertirArriba;

    // ---- estado
    private enum Estado { Esperando, Cuenta, Jugando, Terminado }
    private Estado estado = Estado.Esperando;

    private int score = 0;
    private float timeRemaining;
    private GameObject currentResidueInstance;
    private ResidueData currentResidueData;
    private bool residueThrown = false;
    private bool listoParaLanzar;
    private bool terminarAlAterrizar;

    private int racha;
    private int erroresSeguidos;

    private TiltController tilt;
    private Nivel2Effects efectos;
    private ContainerFeedback cfPapel, cfVidrio, cfPlastico, cfOrganico, cfPeligroso;
    private ContainerFeedback[] feedbacks;

    // que contenedor hay en cada lugar (cambia con el sorteo)
    private ResidueKind kindIzq = ResidueKind.Papel;
    private ResidueKind kindArriba = ResidueKind.Vidrio;
    private ResidueKind kindDer = ResidueKind.Plastico;
    private readonly List<ResidueData> poolActivo = new List<ResidueData>();
    private static string ultimoSorteo = "";

    // estadisticas para la pantalla de puntaje
    private readonly Dictionary<ResidueKind, int> aciertosTipo = new Dictionary<ResidueKind, int>();
    private readonly Dictionary<ResidueKind, int> erroresTipo = new Dictionary<ResidueKind, int>();
    private readonly Dictionary<ResidueKind, int> puntosTipo = new Dictionary<ResidueKind, int>();
    private int mejorRacha;

    // ================================================================== inicio
    void Start()
    {
        Input.gyro.enabled = true;
        timeRemaining = levelDuration;
        Time.timeScale = 1f;

        if (victoryManager == null) victoryManager = Object.FindFirstObjectByType<VictoryManager>();
        if (defeatManager == null) defeatManager = Object.FindFirstObjectByType<DefeatManager>();

        tilt = new TiltController();
        tilt.umbralLateral = umbralInclinacion;
        tilt.umbralArriba = umbralInclinacion;
        tilt.tiempoConfirmar = tiempoConfirmar;
        tilt.zonaMuerta = zonaMuerta;
        tilt.invertirLateral = invertirLateral;
        tilt.invertirArriba = invertirArriba;

        // Primero se sortean y colocan los contenedores; despues se les agrega el feedback
        SortearContenedores();

        cfPapel = ObtenerFeedback(contenedorPapel);
        cfVidrio = ObtenerFeedback(contenedorVidrio);
        cfPlastico = ObtenerFeedback(contenedorPlastico);
        cfOrganico = ObtenerFeedback(contenedorOrganico);
        cfPeligroso = ObtenerFeedback(contenedorPeligroso);
        feedbacks = new ContainerFeedback[] { cfPapel, cfVidrio, cfPlastico, cfOrganico, cfPeligroso };

        efectos = Object.FindFirstObjectByType<Nivel2Effects>();
        if (efectos == null) efectos = gameObject.AddComponent<Nivel2Effects>();
        efectos.Configurar(scoreText, timerText, feedbacks);

        if (timerText != null) timerText.text = Mathf.Ceil(timeRemaining).ToString();
        UpdateUI();

        StartCoroutine(Intro());
    }

    ContainerFeedback ObtenerFeedback(Graphic g)
    {
        if (g == null || !g.gameObject.activeInHierarchy) return null;
        ContainerFeedback cf = g.GetComponent<ContainerFeedback>();
        if (cf == null) cf = g.gameObject.AddComponent<ContainerFeedback>();
        return cf;
    }

    IEnumerator Intro()
    {
        // Un frame de espera: asi el manual (si se abre al empezar) ya pauso el juego
        yield return null;

        // Esperar a que el jugador elija el control
        ControlSelector selector = Object.FindFirstObjectByType<ControlSelector>();
        if (selector != null && selector.controlPanel != null)
            yield return new WaitUntil(() => !selector.controlPanel.activeInHierarchy);

        estado = Estado.Cuenta;
        yield return new WaitForSeconds(0.3f);

        // Cuenta regresiva. En el ultimo segundo se mide como sostienes el celular (calibracion).
        int n = Mathf.Max(1, cuentaRegresiva);
        for (int i = n; i >= 1; i--)
        {
            efectos.MostrarCuenta(i.ToString(), n - i);
            if (i == 1) tilt.IniciarCalibracion();

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                tilt.Actualizar(Time.deltaTime, false);
                yield return null;
            }
        }
        tilt.FinalizarCalibracion();

        efectos.MostrarCuenta("¡YA!", -1);
        estado = Estado.Jugando;
        SpawnResidue();
    }

    // ================================================================== bucle principal
    void Update()
    {
        if (Time.timeScale == 0f) return;       // pausa / manual abierto
        if (estado != Estado.Jugando) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            TiempoAgotado();
            return;
        }
        if (timerText != null) timerText.text = Mathf.Ceil(timeRemaining).ToString();

        bool puede = PuedeLanzar();
        DireccionInclinacion dir = tilt.Actualizar(Time.deltaTime, puede);
        ActualizarPrevisualizacion(puede);

        efectos.Tick(timeRemaining, levelDuration, score, metaPuntaje);

        if (!puede) return;

        // Teclado (para probar en el editor)
        if (dir == DireccionInclinacion.Ninguna)
        {
            if (Input.GetKeyDown(KeyCode.A)) dir = DireccionInclinacion.Izquierda;
            else if (Input.GetKeyDown(KeyCode.W)) dir = DireccionInclinacion.Arriba;
            else if (Input.GetKeyDown(KeyCode.D)) dir = DireccionInclinacion.Derecha;
        }

        if (dir != DireccionInclinacion.Ninguna) Lanzar(dir);
    }

    bool PuedeLanzar()
    {
        return estado == Estado.Jugando && !residueThrown && currentResidueInstance != null && listoParaLanzar;
    }

    void ActualizarPrevisualizacion(bool puede)
    {
        DireccionInclinacion d = puede ? tilt.DireccionPrevia : DireccionInclinacion.Ninguna;
        if (feedbacks != null)
            foreach (ContainerFeedback cf in feedbacks)
                if (cf != null) cf.previsualizando = false;

        ContainerFeedback activo = null;
        if (d == DireccionInclinacion.Izquierda) activo = Contenedor(kindIzq);
        else if (d == DireccionInclinacion.Derecha) activo = Contenedor(kindDer);
        else if (d == DireccionInclinacion.Arriba) activo = Contenedor(kindArriba);
        if (activo != null) activo.previsualizando = true;
    }

    void Lanzar(DireccionInclinacion dir)
    {
        switch (dir)
        {
            case DireccionInclinacion.Izquierda:
                StartCoroutine(ThrowResidue(targetLeft, kindIzq)); break;
            case DireccionInclinacion.Derecha:
                StartCoroutine(ThrowResidue(targetRight, kindDer)); break;
            case DireccionInclinacion.Arriba:
                StartCoroutine(ThrowResidue(targetUp, kindArriba)); break;
        }
    }

    // Boton opcional de "recalibrar": toma la postura actual como centro
    public void Recalibrar()
    {
        if (tilt != null) tilt.Recalibrar();
    }

    // ================================================================== lanzamiento
    IEnumerator ThrowResidue(Transform targetPoint, ResidueKind directionKind)
    {
        residueThrown = true;
        listoParaLanzar = false;

        GameObject inst = currentResidueInstance;
        Transform tr = inst.transform;
        ResidueData data = currentResidueData;

        Vector3 startPos = tr.position;
        Vector3 endPos = DestinoDelLanzamiento(directionKind, targetPoint, startPos);

        Vector3 escalaIni = tr.localScale;
        Vector3 escalaFin = escalaIni * escalaAlLlegar;   // se achica al alejarse: da profundidad y despeja el centro

        // Particulas del residuo durante el vuelo
        ParticleSystem residueParticleSystem = inst.GetComponent<ParticleSystem>();
        if (residueParticleSystem != null) residueParticleSystem.Play();

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * launchSpeed;
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            tr.position = Vector3.Lerp(startPos, endPos, smooth);
            tr.localScale = Vector3.Lerp(escalaIni, escalaFin, smooth);
            yield return null;
        }
        tr.position = endPos;

        if (residueParticleSystem != null) residueParticleSystem.Stop();

        // ---- resultado
        bool acierto = data.kind == directionKind;
        int delta = acierto ? data.scoreValue : data.penaltyValue;
        score += delta;
        RegistrarEstadistica(data.kind, acierto, delta);

        if (acierto)
        {
            racha++;
            if (racha > mejorRacha) mejorRacha = racha;
            erroresSeguidos = 0;
            if (victoryManager != null) victoryManager.OnCorrectResidue();
        }
        else
        {
            racha = 0;
            erroresSeguidos++;
            if (victoryManager != null) victoryManager.OnWrongResidue();
        }

        FeedbackAterrizaje(acierto, directionKind, data.kind, delta, endPos);
        UpdateUI();

        // El residuo "entra" al contenedor
        float k = 0f;
        while (k < 1f && tr != null)
        {
            k += Time.deltaTime / 0.12f;
            tr.localScale = Vector3.Lerp(escalaFin, Vector3.zero, Mathf.Clamp01(k));
            yield return null;
        }
        Destroy(inst);

        if (terminarAlAterrizar)
        {
            Finalizar();
            yield break;
        }

        yield return new WaitForSeconds(esperaEntreResiduos);

        // Regla de derrota por errores seguidos (se avisa recien ahora para que se vea el feedback)
        if (defeatManager != null && derrotaPorErroresSeguidos)
        {
            if (acierto) defeatManager.PlayerCorrectThrow();
            else defeatManager.PlayerMadeMistake();
        }

        residueThrown = false;
        if (estado == Estado.Jugando) SpawnResidue();
    }

    // El contenedor es una imagen del Canvas (2D) y el residuo es un objeto 3D: no comparten coordenadas.
    // Se convierte la posicion del contenedor en pantalla a un punto del mundo 3D, a la misma
    // profundidad que el residuo. Asi el vuelo es una recta hacia el contenedor en cualquier pantalla.
    Vector3 DestinoDelLanzamiento(ResidueKind tipo, Transform respaldo, Vector3 inicio)
    {
        Camera cam = camaraJuego != null ? camaraJuego : Camera.main;
        ContainerFeedback cf = Contenedor(tipo);

        if (apuntarAlContenedor && cam != null && cf != null)
        {
            RectTransform rt = cf.Rect;
            Vector3[] esquinas = new Vector3[4];
            rt.GetWorldCorners(esquinas);
            Vector3 centro = (esquinas[0] + esquinas[2]) * 0.5f;

            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera camUI = null;   // Canvas en modo Overlay: sin camara
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camUI = canvas.rootCanvas.worldCamera != null ? canvas.rootCanvas.worldCamera : cam;

            Vector2 pantalla = RectTransformUtility.WorldToScreenPoint(camUI, centro);
            float profundidad = Vector3.Dot(inicio - cam.transform.position, cam.transform.forward);
            return cam.ScreenToWorldPoint(new Vector3(pantalla.x, pantalla.y, profundidad));
        }

        // Respaldo: el punto 3D de antes
        Vector3 fin = respaldo != null ? respaldo.position : inicio;
        fin.z = inicio.z;
        return fin;
    }

    void FeedbackAterrizaje(bool acierto, ResidueKind lanzadoA, ResidueKind correcto, int delta, Vector3 puntoMundo)
    {
        ContainerFeedback elegido = Contenedor(lanzadoA);
        ContainerFeedback verdadero = Contenedor(correcto);

        if (acierto)
        {
            if (elegido != null) elegido.Acierto(racha);
            Particulas(puntoMundo, new Color(0.35f, 1f, 0.5f));
        }
        else
        {
            if (elegido != null) elegido.Error();
            if (verdadero != null && verdadero != elegido) verdadero.Pista();   // "este era el correcto"
            Particulas(puntoMundo, new Color(1f, 0.4f, 0.3f));
        }

        int errores = derrotaPorErroresSeguidos ? erroresSeguidos : 0;
        efectos.OnResultado(acierto, racha, errores, delta, elegido != null ? elegido.Rect : null);
    }

    ContainerFeedback Contenedor(ResidueKind tipo)
    {
        switch (tipo)
        {
            case ResidueKind.Papel: return cfPapel;
            case ResidueKind.Vidrio: return cfVidrio;
            case ResidueKind.Plastico: return cfPlastico;
            case ResidueKind.Organico: return cfOrganico;
            default: return cfPeligroso;
        }
    }

    void Particulas(Vector3 pos, Color color)
    {
        if (residueParticles != null)
        {
            residueParticles.transform.position = pos;
            ParticleSystem.MainModule main = residueParticles.main;
            main.startColor = color;
            residueParticles.Play();
        }
        else
        {
            Efectos.Chispas(pos, color, 24);
        }
    }

    // ================================================================== sorteo de contenedores
    void SortearContenedores()
    {
        if (contenedorPapel == null || contenedorVidrio == null || contenedorPlastico == null)
        {
            Debug.LogWarning("ResidueLevelManager: asigna los contenedores de Papel, Vidrio y Plastico (son los 3 lugares).");
            ConstruirPool(new List<ResidueKind> { ResidueKind.Papel, ResidueKind.Vidrio, ResidueKind.Plastico });
            return;
        }

        // Los 3 lugares: por defecto, donde estan hoy papel (izquierda), vidrio (arriba) y plastico (derecha)
        Vector3 pIzq = slotIzquierda != null ? slotIzquierda.position : contenedorPapel.rectTransform.position;
        Vector3 pArr = slotArriba != null ? slotArriba.position : contenedorVidrio.rectTransform.position;
        Vector3 pDer = slotDerecha != null ? slotDerecha.position : contenedorPlastico.rectTransform.position;

        List<ResidueKind> candidatos = new List<ResidueKind> { ResidueKind.Papel, ResidueKind.Vidrio, ResidueKind.Plastico };
        if (contenedorOrganico != null) candidatos.Add(ResidueKind.Organico);
        if (contenedorPeligroso != null) candidatos.Add(ResidueKind.Peligroso);

        List<ResidueKind> elegidos;
        if (!sortearContenedores || candidatos.Count <= 3)
            elegidos = new List<ResidueKind> { ResidueKind.Papel, ResidueKind.Vidrio, ResidueKind.Plastico };   // como antes
        else
            elegidos = Sortear(candidatos);

        kindIzq = elegidos[0];
        kindArriba = elegidos[1];
        kindDer = elegidos[2];

        // Mostrar los elegidos en sus lugares y ocultar el resto
        AcomodarContenedor(contenedorPapel, ResidueKind.Papel, pIzq, pArr, pDer);
        AcomodarContenedor(contenedorVidrio, ResidueKind.Vidrio, pIzq, pArr, pDer);
        AcomodarContenedor(contenedorPlastico, ResidueKind.Plastico, pIzq, pArr, pDer);
        AcomodarContenedor(contenedorOrganico, ResidueKind.Organico, pIzq, pArr, pDer);
        AcomodarContenedor(contenedorPeligroso, ResidueKind.Peligroso, pIzq, pArr, pDer);

        ConstruirPool(elegidos);
    }

    void AcomodarContenedor(Graphic g, ResidueKind tipo, Vector3 pIzq, Vector3 pArr, Vector3 pDer)
    {
        if (g == null) return;

        bool usa = tipo == kindIzq || tipo == kindArriba || tipo == kindDer;
        g.gameObject.SetActive(usa);
        if (!usa) return;

        // Se coloca por posicion en el mundo: funciona aunque tengan anclas distintas
        if (tipo == kindIzq) g.rectTransform.position = pIzq;
        else if (tipo == kindArriba) g.rectTransform.position = pArr;
        else g.rectTransform.position = pDer;
    }

    List<ResidueKind> Sortear(List<ResidueKind> candidatos)
    {
        List<ResidueKind> resultado = null;
        string clave = "";

        for (int intento = 0; intento < 6; intento++)
        {
            List<ResidueKind> barajado = new List<ResidueKind>(candidatos);
            for (int i = barajado.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                ResidueKind tmp = barajado[i]; barajado[i] = barajado[j]; barajado[j] = tmp;
            }

            List<ResidueKind> tres = barajado.GetRange(0, 3);
            List<ResidueKind> ordenados = new List<ResidueKind>(tres);
            ordenados.Sort();

            clave = string.Join(",", ordenados);
            resultado = mezclarPosiciones ? tres : ordenados;

            if (!evitarRepetirSorteo || clave != ultimoSorteo) break;
        }

        ultimoSorteo = clave;
        return resultado;
    }

    // Solo salen residuos cuyo contenedor esta en juego (si no, seria imposible acertar)
    void ConstruirPool(List<ResidueKind> elegidos)
    {
        poolActivo.Clear();
        if (residuePool == null) return;

        foreach (ResidueData r in residuePool)
            if (r != null && r.prefab != null && elegidos.Contains(r.kind)) poolActivo.Add(r);

        foreach (ResidueKind k in elegidos)
        {
            bool hay = false;
            foreach (ResidueData r in poolActivo) if (r.kind == k) { hay = true; break; }
            if (!hay) Debug.LogWarning("ResidueLevelManager: no hay ningun ResidueData de tipo " + k + " en 'Residue Pool'.");
        }

        if (poolActivo.Count == 0)
        {
            Debug.LogWarning("ResidueLevelManager: ningun residuo coincide con los contenedores elegidos; se usara todo el pool.");
            foreach (ResidueData r in residuePool) if (r != null && r.prefab != null) poolActivo.Add(r);
        }
    }

    // Permite usar modelos de otros niveles (con Rigidbody / colliders) sin que caigan ni estorben
    void PrepararPrefab(GameObject go)
    {
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;

        if (go.GetComponent<ResidueSpin>() == null) go.AddComponent<ResidueSpin>();
    }

    // ================================================================== residuos
    void SpawnResidue()
    {
        if (poolActivo.Count == 0) return;

        currentResidueData = poolActivo[Random.Range(0, poolActivo.Count)];
        currentResidueInstance = Instantiate(currentResidueData.prefab, spawnPoint.position, Quaternion.identity);
        PrepararPrefab(currentResidueInstance);

        Vector3 escalaFinal = currentResidueInstance.transform.localScale * escalaResiduos * currentResidueData.escala;
        listoParaLanzar = false;
        StartCoroutine(AparecerResiduo(currentResidueInstance, escalaFinal));
    }

    IEnumerator AparecerResiduo(GameObject inst, Vector3 escalaFinal)
    {
        Transform tr = inst.transform;
        tr.localScale = Vector3.zero;

        float k = 0f;
        while (k < 1f && tr != null)
        {
            k += Time.deltaTime / 0.25f;
            tr.localScale = escalaFinal * EaseOutBack(Mathf.Clamp01(k));
            yield return null;
        }

        if (tr != null)
        {
            tr.localScale = escalaFinal;
            if (inst == currentResidueInstance) listoParaLanzar = true;
        }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // ================================================================== UI y final
    void UpdateUI()
    {
        if (efectos != null) efectos.SetPuntaje(score);
        else if (scoreText != null) scoreText.text = "Puntaje: " + score;
    }

    void TiempoAgotado()
    {
        timeRemaining = 0f;
        if (timerText != null) timerText.text = "0";
        estado = Estado.Terminado;

        ActualizarPrevisualizacion(false);
        efectos.FinDeNivel();

        // Si hay un residuo en el aire, se deja caer y cuenta; el nivel termina al aterrizar
        if (residueThrown) terminarAlAterrizar = true;
        else Finalizar();
    }

    void Finalizar()
    {
        if (currentResidueInstance != null) Destroy(currentResidueInstance);
        CheckGameOver();
        enabled = false;
    }

    public int GetScore()
    {
        return score;
    }

    public float GetTimeRemaining()
    {
        return timeRemaining;
    }

    public int MetaPuntaje
    {
        get { return metaPuntaje; }
    }

    void RegistrarEstadistica(ResidueKind tipo, bool acierto, int delta)
    {
        Dictionary<ResidueKind, int> cuenta = acierto ? aciertosTipo : erroresTipo;
        int actual;
        cuenta.TryGetValue(tipo, out actual);
        cuenta[tipo] = actual + 1;

        int pts;
        puntosTipo.TryGetValue(tipo, out pts);
        puntosTipo[tipo] = pts + delta;
    }

    static string NombreTipo(ResidueKind k)
    {
        switch (k)
        {
            case ResidueKind.Plastico: return "Plástico";
            case ResidueKind.Organico: return "Orgánico";
            default: return k.ToString();
        }
    }

    // Al terminar el tiempo: pantalla de puntaje (estrellas, resumen por tipo, guardar, ranking)
    void CheckGameOver()
    {
        DatosResultado dr = new DatosResultado();
        dr.nivel = 2;
        dr.tituloNivel = "Nivel 2 - Clasificación";
        dr.puntaje = score;
        dr.meta = metaPuntaje;
        dr.umbrales = new float[] { 1f, umbralDosEstrellas, umbralTresEstrellas };

        // Cuenta por tipo, solo con los 3 contenedores que tocaron en esta partida
        ResidueKind[] enJuego = { kindIzq, kindArriba, kindDer };
        foreach (ResidueKind k in enJuego)
        {
            int ok, mal, pts;
            aciertosTipo.TryGetValue(k, out ok);
            erroresTipo.TryGetValue(k, out mal);
            puntosTipo.TryGetValue(k, out pts);
            dr.filas.Add(FilaDetalle.Numero(NombreTipo(k) + "  (" + ok + " bien, " + mal + " mal)", pts, pts > 0 ? "+" : ""));
        }
        dr.filas.Add(FilaDetalle.Texto("Mejor racha", mejorRacha >= 2 ? "x" + mejorRacha : "-"));

        dr.permitirGuardar = dr.Estrellas >= 1;
        if (!string.IsNullOrEmpty(escenaSiguiente))
        {
            dr.textoContinuar = "Siguiente nivel";
            dr.escenaSiguiente = escenaSiguiente;
        }

        PantallaPuntaje.Mostrar(dr);
    }
}
