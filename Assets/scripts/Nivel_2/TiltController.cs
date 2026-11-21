using UnityEngine;

public enum DireccionInclinacion { Ninguna, Izquierda, Derecha, Arriba }

// Lee la inclinacion del celular de forma estable y comoda:
//  - Se CALIBRA: mide desde la posicion en que sostienes el celular, no desde una posicion fija.
//  - Suaviza los temblores de la mano (filtro).
//  - Exige mantener la inclinacion unos instantes (evita roces y movimientos involuntarios).
//  - Despues de lanzar hay que volver al centro: no se lanza otro residuo por seguir inclinado.
//  - Si cambias de postura, se readapta solo.
public class TiltController
{
    public float umbralLateral = 0.30f;   // ~17 grados
    public float umbralArriba = 0.30f;
    public float zonaMuerta = 0.12f;      // "volver al centro" = quedar dentro de esta zona
    public float suavizado = 12f;
    public float tiempoConfirmar = 0.10f; // segundos que hay que sostener la inclinacion
    public float derivaNeutral = 0.4f;    // seguimiento lento de la postura mientras estas centrado
    public float segundosParaRecentrar = 1.0f;
    public bool invertirLateral;
    public bool invertirArriba;

    // Hacia donde se esta inclinando (solo mientras confirma el lanzamiento): sirve para iluminar el contenedor
    public DireccionInclinacion DireccionPrevia { get; private set; }

    private Vector2 neutral;
    private Vector2 filtrado;
    private bool inicializado;
    private bool armado = true;
    private DireccionInclinacion candidata = DireccionInclinacion.Ninguna;
    private float tiempoCandidata;
    private float tiempoQuieto;

    private bool calibrando;
    private Vector2 acumulado;
    private float tiempoAcumulado;

    private Vector2 Relativa
    {
        get
        {
            Vector2 r = filtrado - neutral;
            if (invertirLateral) r.x = -r.x;
            if (invertirArriba) r.y = -r.y;
            return r;
        }
    }

    // ---- calibracion: promedia la postura durante un tiempo (ej. el ultimo segundo de la cuenta regresiva)
    public void IniciarCalibracion()
    {
        calibrando = true;
        acumulado = Vector2.zero;
        tiempoAcumulado = 0f;
    }

    public void FinalizarCalibracion()
    {
        if (tiempoAcumulado > 0.05f)
        {
            neutral = acumulado / tiempoAcumulado;
            filtrado = neutral;
        }
        else
        {
            neutral = filtrado;
        }
        calibrando = false;
        armado = true;
        candidata = DireccionInclinacion.Ninguna;
        tiempoCandidata = 0f;
    }

    // Toma la postura actual como "centro" (util para un boton de recalibrar)
    public void Recalibrar()
    {
        neutral = filtrado;
        armado = true;
        candidata = DireccionInclinacion.Ninguna;
        tiempoCandidata = 0f;
    }

    // Llamar una vez por frame. Devuelve la direccion SOLO en el frame en que se confirma un lanzamiento.
    public DireccionInclinacion Actualizar(float dt, bool puedeLanzar)
    {
        if (dt <= 0f) return DireccionInclinacion.Ninguna;

        Vector3 a = Input.acceleration;
        Vector2 raw = new Vector2(a.x, a.y);

        if (!inicializado)
        {
            filtrado = raw;
            neutral = raw;
            inicializado = true;
        }

        if (calibrando)
        {
            acumulado += raw * dt;
            tiempoAcumulado += dt;
        }

        Vector2 previo = filtrado;
        filtrado = Vector2.Lerp(filtrado, raw, 1f - Mathf.Exp(-suavizado * dt));

        DireccionPrevia = DireccionInclinacion.Ninguna;
        if (calibrando) return DireccionInclinacion.Ninguna;

        Vector2 rel = Relativa;
        bool centrada = Mathf.Abs(rel.x) < zonaMuerta && Mathf.Abs(rel.y) < zonaMuerta;

        // ---- esperando volver al centro despues de un lanzamiento
        if (!armado)
        {
            if (centrada)
            {
                armado = true;
                tiempoQuieto = 0f;
            }
            else
            {
                // Si te quedas quieto en otra postura, esa pasa a ser el nuevo centro
                float vel = (filtrado - previo).magnitude / dt;
                tiempoQuieto = vel < 0.06f ? tiempoQuieto + dt : 0f;
                if (tiempoQuieto >= segundosParaRecentrar)
                {
                    neutral = filtrado;
                    armado = true;
                    tiempoQuieto = 0f;
                }
            }
            candidata = DireccionInclinacion.Ninguna;
            tiempoCandidata = 0f;
            return DireccionInclinacion.Ninguna;
        }

        DireccionInclinacion cand = Candidata(rel);

        if (cand == DireccionInclinacion.Ninguna)
        {
            // Centrado: el "centro" sigue lentamente los cambios de postura
            if (centrada) neutral = Vector2.Lerp(neutral, filtrado, Mathf.Clamp01(derivaNeutral * dt));
            candidata = DireccionInclinacion.Ninguna;
            tiempoCandidata = 0f;
            return DireccionInclinacion.Ninguna;
        }

        if (cand != candidata)
        {
            candidata = cand;
            tiempoCandidata = 0f;
        }

        // Mientras no se pueda lanzar (residuo en vuelo, cooldown) no se acumula nada
        if (!puedeLanzar)
        {
            tiempoCandidata = 0f;
            return DireccionInclinacion.Ninguna;
        }

        tiempoCandidata += dt;
        DireccionPrevia = cand;

        if (tiempoCandidata >= tiempoConfirmar)
        {
            armado = false;
            candidata = DireccionInclinacion.Ninguna;
            tiempoCandidata = 0f;
            DireccionPrevia = DireccionInclinacion.Ninguna;
            return cand;
        }
        return DireccionInclinacion.Ninguna;
    }

    private DireccionInclinacion Candidata(Vector2 rel)
    {
        float fx = Mathf.Abs(rel.x) / Mathf.Max(0.01f, umbralLateral);
        float fy = rel.y / Mathf.Max(0.01f, umbralArriba);

        if (fx < 1f && fy < 1f) return DireccionInclinacion.Ninguna;
        if (fy > fx) return DireccionInclinacion.Arriba;
        return rel.x < 0f ? DireccionInclinacion.Izquierda : DireccionInclinacion.Derecha;
    }
}
