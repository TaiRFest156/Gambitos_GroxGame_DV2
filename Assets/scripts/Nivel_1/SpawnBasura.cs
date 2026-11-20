using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnBasura : MonoBehaviour
{
    // ====== Originales (mismos nombres: no pierdes lo configurado en el Inspector) ======
    [Header("Prefabs de Basura")]
    public GameObject[] basuraPrefabs;
    [Tooltip("Cuantas veces entra cada prefab en la 'bolsa' de sorteo (mismo orden que los prefabs). Vacio = todos iguales.")]
    public int[] pesosPrefabs;

    [Header("Área de Spawn")]
    public float minX = -5f;
    public float maxX = 5f;
    public float minZ = -5f;
    public float maxZ = 5f;
    public float spawnY = 1f;

    [Header("Tiempo entre spawns")]
    public float tiempoInicial = 1.2f;
    public float tiempoMinimo = 0.3f;
    public float reduccionTiempo = 0.01f;

    [Header("Duración del spawner")]
    public float duracionSpawner = 60f;

    // ====== Nuevos ======
    [Header("Distribucion natural")]
    [Tooltip("Por cada residuo se prueban varias posiciones y se elige la mas alejada de los ultimos: reparte la basura sin amontonarla")]
    public int candidatosPorSpawn = 8;
    [Tooltip("Cuantos spawns recientes se recuerdan para no repetir zonas")]
    public int memoriaSpawns = 12;
    [Tooltip("No cae basura mas cerca que esto del jugador")]
    public float distanciaMinimaJugador = 2.5f;

    [Header("Esferas de energia")]
    public bool habilitarEsferas = true;
    [Tooltip("Opcional: tu propio modelo. Vacio = se crea una esfera azul brillante.")]
    public GameObject esferaPrefab;
    [Range(0f, 1f)] public float probabilidadEsfera = 0.08f;
    [Tooltip("Si el blaster esta casi sin energia, aparecen mas seguido (ayuda para no quedarse trabado)")]
    [Range(0f, 1f)] public float probabilidadEsferaEnergiaBaja = 0.45f;
    public float energiaBajaPorcentaje = 25f;
    public int maxEsferasActivas = 2;
    public float segundosMinimosEntreEsferas = 5f;
    public float alturaCaidaEsfera = 8f;

    private float tiempoActual;
    private float tiempoTranscurrido = 0f;
    private Coroutine rutinaSpawner;

    private readonly List<int> bolsa = new List<int>();
    private int ultimoIndice = -1;
    private readonly Queue<Vector3> recientes = new Queue<Vector3>();
    private float ultimaEsfera = -99f;

    // Ahora no arranca solo en Start
    void Awake()
    {
        tiempoActual = tiempoInicial;
        Nivel1Sistema.Asegurar();
    }

    // Método público para iniciar el spawner
    public void IniciarSpawner()
    {
        if (rutinaSpawner == null)
        {
            tiempoTranscurrido = 0f;
            tiempoActual = tiempoInicial;
            rutinaSpawner = StartCoroutine(SpawnRoutine());

            // Llamar al contador
            UIContadorTiempo ui = Object.FindFirstObjectByType<UIContadorTiempo>();
            if (ui != null)
            {
                ui.IniciarContador(duracionSpawner);
            }
        }
    }

    // Método público para detener el spawner antes de tiempo
    public void DetenerSpawner()
    {
        if (rutinaSpawner != null)
        {
            StopCoroutine(rutinaSpawner);
            rutinaSpawner = null;
        }
    }

    IEnumerator SpawnRoutine()
    {
        while (tiempoTranscurrido < duracionSpawner)
        {
            SpawnearBasura();
            if (DebeSpawnearEsfera()) SpawnearEsfera();

            yield return new WaitForSeconds(tiempoActual);
            tiempoTranscurrido += tiempoActual;

            if (tiempoActual > tiempoMinimo)
            {
                tiempoActual -= reduccionTiempo;
            }
        }

        rutinaSpawner = null; // se libera al terminar

        // Ya no se puede perder por contaminacion
        if (Nivel1Sistema.Instance != null) Nivel1Sistema.Instance.NivelTerminado();

        SceneLoader loader = Object.FindFirstObjectByType<SceneLoader>();
        if (loader != null)
        {
            loader.OnSpawnerFinished();

            if (!ReciclajeContador.SuperoMeta())
            {
                Time.timeScale = 1f;
                PlayerPrefs.SetString("EscenaAnterior", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                UnityEngine.SceneManagement.SceneManager.LoadScene("Derrota");
            }
        }
    }

    // ================================================================== residuos
    void SpawnearBasura()
    {
        if (basuraPrefabs == null || basuraPrefabs.Length == 0) return;

        GameObject prefab = basuraPrefabs[SiguienteIndice()];
        Vector3 spawnPos = ElegirPosicion(spawnY);
        Quaternion rot = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
        GameObject go = Instantiate(prefab, spawnPos, rot);

        BasuraItem item = go.GetComponent<BasuraItem>();
        if (item == null) item = go.AddComponent<BasuraItem>();
        item.Iniciar(TipoDe(go));
    }

    // "Bolsa de sorteo": cada prefab sale las veces que indica su peso antes de repetirse,
    // y nunca el mismo dos veces seguidas. Mas parejo que un azar puro.
    int SiguienteIndice()
    {
        if (bolsa.Count == 0)
        {
            for (int i = 0; i < basuraPrefabs.Length; i++)
            {
                int peso = (pesosPrefabs != null && i < pesosPrefabs.Length) ? Mathf.Max(0, pesosPrefabs[i]) : 1;
                for (int k = 0; k < peso; k++) bolsa.Add(i);
            }
            if (bolsa.Count == 0) bolsa.Add(0);

            for (int i = bolsa.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = bolsa[i]; bolsa[i] = bolsa[j]; bolsa[j] = tmp;
            }
        }

        int pos = bolsa.Count - 1;
        if (bolsa[pos] == ultimoIndice)
        {
            for (int i = 0; i < bolsa.Count - 1; i++)
                if (bolsa[i] != ultimoIndice) { pos = i; break; }
        }

        int indice = bolsa[pos];
        bolsa.RemoveAt(pos);
        ultimoIndice = indice;
        return indice;
    }

    // Mejor candidato: prueba varias posiciones y elige la mas lejos de los ultimos spawns
    Vector3 ElegirPosicion(float y)
    {
        Vector3 jugador = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

        Vector3 mejor = new Vector3(Random.Range(minX, maxX), y, Random.Range(minZ, maxZ));
        float mejorPuntaje = -1f;

        int intentos = Mathf.Max(1, candidatosPorSpawn);
        for (int i = 0; i < intentos; i++)
        {
            Vector3 p = new Vector3(Random.Range(minX, maxX), y, Random.Range(minZ, maxZ));

            // Muy cerca del jugador: se descarta
            if (DistanciaXZ(p, jugador) < distanciaMinimaJugador) continue;

            float dMin = 999f;
            foreach (Vector3 q in recientes) dMin = Mathf.Min(dMin, DistanciaXZ(p, q));

            if (dMin > mejorPuntaje)
            {
                mejorPuntaje = dMin;
                mejor = p;
            }
        }

        recientes.Enqueue(mejor);
        while (recientes.Count > Mathf.Max(1, memoriaSpawns)) recientes.Dequeue();
        return mejor;
    }

    static float DistanciaXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // Tag del tipo de basura (en el objeto raiz o en alguno de sus colliders)
    static string TipoDe(GameObject go)
    {
        if (Nivel1Sistema.EsTagBasura(go.tag)) return go.tag;
        foreach (Collider c in go.GetComponentsInChildren<Collider>())
            if (Nivel1Sistema.EsTagBasura(c.tag)) return c.tag;
        return "";
    }

    // ================================================================== esferas de energia
    bool DebeSpawnearEsfera()
    {
        if (!habilitarEsferas) return false;
        if (PowerUpOrb.Activos >= maxEsferasActivas) return false;
        if (Time.time - ultimaEsfera < segundosMinimosEntreEsferas) return false;

        Nivel1Sistema sis = Nivel1Sistema.Instance;
        bool baja = sis != null && sis.EnergiaPct < energiaBajaPorcentaje;
        float prob = baja ? probabilidadEsferaEnergiaBaja : probabilidadEsfera;
        return Random.value < prob;
    }

    void SpawnearEsfera()
    {
        ultimaEsfera = Time.time;

        Vector3 pos = ElegirPosicion(spawnY + alturaCaidaEsfera);

        // Altura del suelo debajo, para que se detenga flotando justo encima
        float sueloY = spawnY - 1f;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(pos.x, pos.y + 20f, pos.z), Vector3.down, out hit, 200f))
            sueloY = hit.point.y;

        PowerUpOrb.Crear(esferaPrefab, pos, sueloY);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 center = new Vector3((minX + maxX) / 2f, spawnY, (minZ + maxZ) / 2f);
        Vector3 size = new Vector3(Mathf.Abs(maxX - minX), 0.1f, Mathf.Abs(maxZ - minZ));
        Gizmos.DrawWireCube(center, size);
    }
}
