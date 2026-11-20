using UnityEngine;

// Esfera de recarga de energia: cae del cielo, flota un rato y desaparece si no la disparas.
// Si no asignas un prefab en SpawnBasura, se construye sola con formas basicas.
public class PowerUpOrb : MonoBehaviour
{
    public static int Activos;

    public float velocidadCaida = 2.5f;
    public float alturaFlotando = 1.4f;
    public float duracionFlotando = 12f;

    private enum Fase { Cayendo, Flotando, Desapareciendo, Capturada }
    private Fase fase = Fase.Cayendo;
    private float sueloY;
    private float tFase;
    private float baseY;
    private Vector3 escalaBase;
    private SpriteRenderer brillo;
    private Light luz;
    private bool contada;

    public static PowerUpOrb Crear(GameObject prefab, Vector3 posicion, float sueloY)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, posicion, Quaternion.identity);
        }
        else
        {
            go = new GameObject("EsferaEnergia");
            go.transform.position = posicion;
            ConstruirVisual(go.transform);
        }

        if (go.GetComponentInChildren<Collider>() == null)
        {
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.6f;
        }

        PowerUpOrb orbe = go.GetComponent<PowerUpOrb>();
        if (orbe == null) orbe = go.AddComponent<PowerUpOrb>();
        orbe.sueloY = sueloY;
        return orbe;
    }

    private static void ConstruirVisual(Transform raiz)
    {
        Color cian = new Color(0.2f, 0.9f, 1f);

        // Nucleo
        GameObject nucleo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Collider cn = nucleo.GetComponent<Collider>();
        if (cn != null) Destroy(cn);
        nucleo.transform.SetParent(raiz, false);
        nucleo.transform.localScale = Vector3.one * 0.5f;
        Material m = nucleo.GetComponent<Renderer>().material;
        m.color = cian;
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", cian * 2.5f);
        }

        // Collider generoso (mas facil de apuntar)
        SphereCollider sc = raiz.gameObject.AddComponent<SphereCollider>();
        sc.radius = 0.6f;

        // Resplandor
        GameObject g = new GameObject("Brillo");
        g.transform.SetParent(raiz, false);
        g.transform.localScale = Vector3.one * 2.6f;
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Nivel1Fx.Blob();
        sr.color = new Color(0.3f, 0.9f, 1f, 0.6f);

        // Luz
        GameObject l = new GameObject("Luz");
        l.transform.SetParent(raiz, false);
        Light luz = l.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = cian;
        luz.range = 4f;
        luz.intensity = 2f;
    }

    private void Awake()
    {
        Activos++;
        contada = true;
        escalaBase = transform.localScale;
        brillo = GetComponentInChildren<SpriteRenderer>();
        luz = GetComponentInChildren<Light>();
    }

    private void Start()
    {
        Nivel1Audio.Reproducir(SonidoN1.OrbeAparece);
    }

    private void OnDestroy()
    {
        if (contada) Activos--;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        tFase += dt;

        // El resplandor siempre mira a la camara
        if (brillo != null && Camera.main != null) brillo.transform.rotation = Camera.main.transform.rotation;

        switch (fase)
        {
            case Fase.Cayendo:
                transform.position += Vector3.down * velocidadCaida * dt;
                transform.Rotate(0f, 120f * dt, 0f);
                if (transform.position.y <= sueloY + alturaFlotando)
                {
                    baseY = sueloY + alturaFlotando;
                    fase = Fase.Flotando;
                    tFase = 0f;
                }
                break;

            case Fase.Flotando:
            {
                Vector3 p = transform.position;
                p.y = baseY + Mathf.Sin(tFase * 2.2f) * 0.15f;
                transform.position = p;
                transform.Rotate(0f, 90f * dt, 0f);

                float restante = duracionFlotando - tFase;
                float pulso = 1f + 0.08f * Mathf.Sin(tFase * (restante < 3f ? 18f : 5f));
                transform.localScale = escalaBase * pulso;     // en los ultimos 3 s late rapido: "se va a ir"
                if (luz != null) luz.intensity = 1.6f + 0.8f * Mathf.Sin(tFase * 5f);

                if (tFase >= duracionFlotando)
                {
                    fase = Fase.Desapareciendo;
                    tFase = 0f;
                }
                break;
            }

            case Fase.Desapareciendo:
            {
                float k = Mathf.Clamp01(tFase / 0.4f);
                transform.localScale = escalaBase * (1f - k);
                if (k >= 1f) Destroy(gameObject);
                break;
            }
        }
    }

    // Lo llama RaycastClickDestroy al acertarle
    public void Capturar()
    {
        if (fase == Fase.Capturada || fase == Fase.Desapareciendo) return;
        fase = Fase.Capturada;

        Nivel1Fx.Chispas(transform.position, new Color(0.3f, 0.95f, 1f), 28);

        Nivel1Sistema sis = Nivel1Sistema.Instance;
        if (sis != null) sis.RecargarEnergia(transform.position);

        Destroy(gameObject);
    }
}
