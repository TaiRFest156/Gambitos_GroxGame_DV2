using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento del jugador (Input System). Ahora con:
//  - Movimiento relativo a hacia donde MIRAS (antes avanzaba por los ejes del mundo)
//  - Salto (boton en pantalla, Espacio, boton A del mando) con coyote time y buffer
//  - Teclado (WASD / flechas, Shift = correr) y mando
// Tu joystick de pantalla sigue conectado por "Move Action", como antes.
public class CharacterControllerMove : MonoBehaviour
{
    [Header("Input Actions (se conservan)")]
    public InputActionReference moveAction;   // Vector2: tu joystick de pantalla va conectado aqui
    public InputActionReference jumpAction;   // opcional

    [Header("Movimiento")]
    public float velocidad = 4f;
    [Tooltip("Shift (PC) o click del stick izquierdo (mando)")]
    public float velocidadCorrer = 6.5f;
    [Tooltip("Avanzar hacia donde mira la camara")]
    public bool relativoALaCamara = true;

    [Header("Salto")]
    public float alturaSalto = 1.2f;
    public float gravedad = -20f;
    [Tooltip("Gravedad extra al caer: el salto se siente menos flotante")]
    public float multiplicadorCaida = 1.3f;
    [Tooltip("Mantener el boton = salto mas alto; soltarlo antes = salto mas corto")]
    public bool saltoVariable = true;
    [Tooltip("Segundos despues de dejar el borde en los que todavia se puede saltar")]
    public float tiempoCoyote = 0.12f;
    [Tooltip("Si pulsas saltar un instante antes de aterrizar, salta al tocar el suelo")]
    public float bufferSalto = 0.15f;

    private CharacterController controller;
    private float velocidadY;
    private float tiempoDesdeSuelo = 99f;
    private float bufferTimer;
    private bool enSuelo;

    private static readonly RaycastHit[] golpes = new RaycastHit[6];

    private void Awake()
    {
        controller = gameObject.AddComponent<CharacterController>();
    }

    private void Start()
    {
        ControlesJugador.Asegurar();
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (jumpAction != null) jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        float dt = Time.deltaTime;

        // ---------------- suelo
        enSuelo = controller.isGrounded || RayoAlSuelo();
        if (enSuelo)
        {
            tiempoDesdeSuelo = 0f;
            if (velocidadY < 0f) velocidadY = -2f;   // pegado al suelo
        }
        else
        {
            tiempoDesdeSuelo += dt;
        }

        // ---------------- entrada
        bool correr;
        Vector2 input = LeerMovimiento(out correr);

        bool saltoPulsado;
        bool saltoMantenido;
        LeerSalto(out saltoPulsado, out saltoMantenido);

        // ---------------- salto
        if (saltoPulsado) bufferTimer = bufferSalto;
        else bufferTimer -= dt;

        if (bufferTimer > 0f && tiempoDesdeSuelo <= tiempoCoyote)
        {
            velocidadY = Mathf.Sqrt(alturaSalto * -2f * gravedad);
            bufferTimer = 0f;
            tiempoDesdeSuelo = tiempoCoyote + 1f;   // gasta el coyote time: no hay doble salto
        }

        // ---------------- gravedad
        float g = gravedad;
        if (velocidadY < 0f) g *= multiplicadorCaida;
        else if (saltoVariable && !saltoMantenido) g *= 2.2f;   // soltaste el boton: el salto se corta
        velocidadY += g * dt;

        // ---------------- mover
        Vector3 direccion = Direccion(input);
        float rapidez = correr ? velocidadCorrer : velocidad;
        Vector3 total = direccion * rapidez + Vector3.up * velocidadY;

        CollisionFlags flags = controller.Move(total * dt);
        if ((flags & CollisionFlags.Above) != 0 && velocidadY > 0f) velocidadY = 0f;   // golpe con el techo
    }

    // ------------------------------------------------------------------ lectura de controles
    private Vector2 LeerMovimiento(out bool correr)
    {
        correr = false;
        if (ControlesJugador.CursorLibreForzado) return Vector2.zero;

        Vector2 v = Vector2.zero;

        // Tu joystick de pantalla (y lo que tengas en el Input Action)
        if (moveAction != null) v += moveAction.action.ReadValue<Vector2>();

        // Teclado
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            // Teclas configurables en Opciones (las flechas siempre funcionan)
            if (TeclasJuego.Presionada(AccionTecla.Derecha) || kb.rightArrowKey.isPressed) v.x += 1f;
            if (TeclasJuego.Presionada(AccionTecla.Izquierda) || kb.leftArrowKey.isPressed) v.x -= 1f;
            if (TeclasJuego.Presionada(AccionTecla.Adelante) || kb.upArrowKey.isPressed) v.y += 1f;
            if (TeclasJuego.Presionada(AccionTecla.Atras) || kb.downArrowKey.isPressed) v.y -= 1f;
            if (TeclasJuego.Presionada(AccionTecla.Correr)) correr = true;
        }

        // Mando
        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            v += gp.leftStick.ReadValue();
            if (gp.leftStickButton.isPressed) correr = true;
        }

        // Joystick de movimiento creado por ControlesJugador (opcional)
        v += ControlesJugador.MoverVirtual;

        return Vector2.ClampMagnitude(v, 1f);
    }

    private void LeerSalto(out bool pulsado, out bool mantenido)
    {
        pulsado = ControlesJugador.ConsumirSalto();   // consumir siempre, aunque este bloqueado
        mantenido = ControlesJugador.SaltoMantenido;

        if (ControlesJugador.CursorLibreForzado)
        {
            pulsado = false;
            mantenido = false;
            return;
        }

        if (TeclasJuego.PulsadaEsteFrame(AccionTecla.Saltar)) pulsado = true;
        if (TeclasJuego.Presionada(AccionTecla.Saltar)) mantenido = true;

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame) pulsado = true;
            if (gp.buttonSouth.isPressed) mantenido = true;
        }

        if (jumpAction != null)
        {
            if (jumpAction.action.WasPressedThisFrame()) pulsado = true;
            if (jumpAction.action.IsPressed()) mantenido = true;
        }
    }

    // Direccion en el mundo: "adelante" es hacia donde mira la camara (sin inclinar hacia arriba o abajo)
    private Vector3 Direccion(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

        Vector3 adelante = Vector3.forward;
        Vector3 derecha = Vector3.right;

        if (relativoALaCamara)
        {
            Transform cam = ControlesJugador.CamaraJugador;
            if (cam != null)
            {
                adelante = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                if (adelante.sqrMagnitude < 0.0001f) adelante = Vector3.ProjectOnPlane(cam.up, Vector3.up);
                adelante.Normalize();
                derecha = Vector3.Cross(Vector3.up, adelante);
            }
        }

        return adelante * input.y + derecha * input.x;
    }

    // Respaldo de isGrounded (que es poco confiable): rayo corto hacia abajo desde el centro del jugador
    private bool RayoAlSuelo()
    {
        if (velocidadY > 1f) return false;   // subiendo: no esta en el suelo

        Vector3 origen = transform.TransformPoint(controller.center);
        float distancia = controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y) + 0.12f;

        int n = Physics.RaycastNonAlloc(origen, Vector3.down, golpes, distancia, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = golpes[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;   // yo mismo
            return true;
        }
        return false;
    }
}
