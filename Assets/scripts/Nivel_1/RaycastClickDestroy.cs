using UnityEngine;
using UnityEngine.InputSystem;

// Disparo de la pistola. Ahora con: alcance limitado, energia, esferas de recarga,
// combos y feedback sonoro/visual (a traves de Nivel1Sistema).
public class RaycastClickDestroy : MonoBehaviour
{
    [Tooltip("Distancia maxima de DETECCION: mas alla de 'Rango Disparo' el puntero avisa 'muy lejos'")]
    public float rayDistance = 50f;
    public Pistola pistola; // arrastra aqui tu pistola

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        Nivel1Sistema.Asegurar();
        ControlesJugador.Asegurar();
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;   // pausa o manual abierto

        // ---- Orden de disparar (movil: boton FUEGO o toque corto; PC: clic izquierdo; mando: gatillo derecho)
        bool disparo = ControlesJugador.ConsumirDisparo();

        // PC: boton de disparo configurable en Opciones (clic izquierdo por defecto)
        if (!disparo && ControlesJugador.UsaRaton && ControlesJugador.CursorBloqueado
            && Time.frameCount != ControlesJugador.FrameUltimoBloqueo && TeclasJuego.PulsadaEsteFrame(AccionTecla.Disparar))
            disparo = true;

        Gamepad gamepad = Gamepad.current;
        if (!disparo && gamepad != null && gamepad.rightTrigger.wasPressedThisFrame)
            disparo = true;

        if (!disparo || ControlesJugador.CursorLibreForzado) return;

        Nivel1Sistema sis = Nivel1Sistema.Instance;

        // Sin energia no se dispara
        if (sis != null && !sis.TieneEnergia)
        {
            sis.SinEnergia();
            return;
        }

        // Dispara animacion/shape keys. Si esta en enfriamiento, no hace nada (y no gasta energia).
        if (pistola != null && !pistola.Disparar()) return;
        if (sis != null)
        {
            sis.GastarEnergia();
            // Si tu pistola no tiene sonido propio, suena el generado
            if (pistola == null || pistola.sonidoDisparo == null) Nivel1Audio.Reproducir(SonidoN1.Disparo);
        }

        Transform camJ = ControlesJugador.CamaraJugador != null ? ControlesJugador.CamaraJugador : cam.transform;
        Ray ray = new Ray(camJ.position, camJ.forward);
        float rango = sis != null ? sis.rangoDisparo : rayDistance;

#if UNITY_EDITOR
        Debug.DrawRay(ray.origin, ray.direction * rango, Color.green, 0.5f);
#endif

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, rango))
        {
            if (!ProcesarHit(hit, sis) && sis != null) sis.RegistrarFallo();
            return;
        }

        // No llego: ¿habia algo mas lejos?
        if (sis != null)
        {
            RaycastHit lejos;
            if (Physics.Raycast(ray, out lejos, rayDistance) && Nivel1Sistema.EsObjetivo(lejos.collider))
                sis.FueraDeRango();
            else
                sis.RegistrarFallo();
        }
    }

    // true si acerto algo util
    bool ProcesarHit(RaycastHit hit, Nivel1Sistema sis)
    {
        // Esfera de energia
        PowerUpOrb orbe = hit.collider.GetComponentInParent<PowerUpOrb>();
        if (orbe != null)
        {
            orbe.Capturar();
            return true;
        }

        // Residuo
        string tipo = hit.collider.tag;
        if (!Nivel1Sistema.EsTagBasura(tipo)) return false;

        BasuraItem item = hit.collider.GetComponentInParent<BasuraItem>();
        if (item != null) item.Capturar();
        else Destroy(hit.collider.gameObject);

        if (sis != null) sis.RegistrarCaptura(tipo, hit.point);
        else ReciclajeContador.Registrar(tipo);

        if (ParticlePool.Instance != null)
            ParticlePool.Instance.PlayEffect(hit.point);

        return true;
    }
}
