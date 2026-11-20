using UnityEngine;

// Comportamiento de cada residuo que cae. SpawnBasura se lo agrega solo a cada instancia.
//   1) Cae.
//   2) Al tocar el suelo hay unos segundos de GRACIA (late) para rescatarlo con la pistola.
//   3) Si nadie lo rescata, se desvanece en una bocanada de humo contaminante
//      y suma contaminacion al planeta.
public class BasuraItem : MonoBehaviour
{
    public string tipo = "";

    private enum Fase { Cayendo, EnSuelo, Disolviendo, Capturada }
    private Fase fase = Fase.Cayendo;
    private float tFase;
    private float proximoChequeo;
    private Collider[] cols;
    private Vector3 escalaBase;
    private float gracia = 1.5f;

    private static readonly RaycastHit[] buffer = new RaycastHit[8];

    public void Iniciar(string tipoDetectado)
    {
        tipo = tipoDetectado;
    }

    private void Awake()
    {
        cols = GetComponentsInChildren<Collider>();
        escalaBase = transform.localScale;
    }

    private void Start()
    {
        if (Nivel1Sistema.Instance != null) gracia = Nivel1Sistema.Instance.segundosAntesDeDesvanecer;
        if (GetComponentInChildren<Rigidbody>() == null)
            Debug.LogWarning("BasuraItem: '" + name + "' no tiene Rigidbody, asi que no va a caer.", this);
    }

    private void Update()
    {
        tFase += Time.deltaTime;

        switch (fase)
        {
            case Fase.Cayendo:
                if (Time.time >= proximoChequeo)
                {
                    proximoChequeo = Time.time + 0.08f;
                    if (TocaSuelo()) Aterrizar();
                }
                if (tFase > 12f) Aterrizar();   // seguridad: por si quedo trabada en el aire
                break;

            case Fase.EnSuelo:
            {
                // Late cada vez mas rapido: "se esta por contaminar, ¡dispárame!"
                float uso = Mathf.Clamp01(tFase / Mathf.Max(0.1f, gracia));
                float amp = 0.04f + 0.10f * uso;
                float frec = 8f + 16f * uso;
                transform.localScale = escalaBase * (1f + amp * Mathf.Sin(tFase * frec));
                if (tFase >= gracia) Disolver();
                break;
            }

            case Fase.Disolviendo:
            {
                float k = Mathf.Clamp01(tFase / 0.8f);
                transform.localScale = escalaBase * Mathf.Pow(1f - k, 1.5f);
                transform.position += Vector3.down * 0.15f * Time.deltaTime;
                transform.Rotate(0f, 240f * Time.deltaTime, 0f);
                if (k >= 1f) Destroy(gameObject);
                break;
            }
        }
    }

    private void Aterrizar()
    {
        fase = Fase.EnSuelo;
        tFase = 0f;
    }

    private void Disolver()
    {
        fase = Fase.Disolviendo;
        tFase = 0f;
        transform.localScale = escalaBase;

        // Ya no se puede disparar: no queda "fantasma" disparable
        foreach (Collider c in cols) if (c != null) c.enabled = false;

        Nivel1Fx.Humo(transform.position + Vector3.up * 0.1f, Nivel1Sistema.ColorContaminacionActual);

        if (Nivel1Sistema.Instance != null)
            Nivel1Sistema.Instance.RegistrarCaida(tipo, transform.position);
    }

    // Lo llama RaycastClickDestroy al acertarle
    public void Capturar()
    {
        if (fase == Fase.Capturada || fase == Fase.Disolviendo) return;
        fase = Fase.Capturada;
        Destroy(gameObject);
    }

    // Rayo hacia abajo desde el centro: ¿hay algo (suelo u otra cosa) justo debajo?
    private bool TocaSuelo()
    {
        Bounds b = default(Bounds);
        bool hay = false;
        foreach (Collider c in cols)
        {
            if (c == null) continue;
            if (!hay) { b = c.bounds; hay = true; }
            else b.Encapsulate(c.bounds);
        }
        if (!hay) return false;

        float dist = b.extents.y + 0.12f;
        int n = Physics.RaycastNonAlloc(b.center, Vector3.down, buffer, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = buffer[i].collider;
            if (c.transform.IsChildOf(transform)) continue;                 // yo mismo
            if (c.GetComponentInParent<PowerUpOrb>() != null) continue;     // las esferas no cuentan
            return true;
        }
        return false;
    }
}
