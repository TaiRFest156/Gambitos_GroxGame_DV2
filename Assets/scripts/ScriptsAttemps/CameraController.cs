using UnityEngine;
using UnityEngine.InputSystem;

// Giro de camara del jugador. Va en la CAMARA (igual que antes).
//  - Movil: segun el modo elegido (ControlSettings.Modo), SOLO UNO a la vez:
//        Joystick (por defecto) | Giroscopio | Arrastrar
//  - PC:    raton (con el cursor bloqueado) y mando.
// Se conservan SetGyroSensitivity / GetGyroSensitivity (tu slider de sensibilidad sigue funcionando).
public class CameraController : MonoBehaviour
{
    [Header("Joystick tactil")]
    [Tooltip("Grados por segundo con el joystick al maximo")]
    public float velocidadStick = 140f;
    [Tooltip("Mayor = mas precision al inclinar poco el joystick")]
    public float curvaStick = 1.6f;

    [Header("Arrastre tactil")]
    [Tooltip("Grados por pixel arrastrado")]
    public float sensibilidadArrastre = 0.15f;

    [Header("Giroscopio")]
    public float gyroSensitivity = 3f;
    public float gyroSmooth = 0.1f;

    [Header("Raton y mando (PC)")]
    [Tooltip("Grados por pixel de movimiento del raton")]
    public float sensibilidadMouse = 0.1f;
    [Tooltip("Grados por segundo con el stick derecho del mando")]
    public float velocidadMando = 160f;

    [Header("Limite vertical")]
    public float minY = -80f;
    public float maxY = 80f;

    private float rotX;
    private float rotY;
    private Vector3 gyroAcumulado;
    private bool gyroActivo;

    private void Start()
    {
        ControlesJugador.Asegurar();

        // Angulo inicial (convertido a -180..180: antes un 350 se "pegaba" al limite)
        Vector3 inicial = transform.localEulerAngles;
        rotX = Mathf.DeltaAngle(0f, inicial.x);
        rotY = inicial.y;
    }

    private void OnDisable()
    {
        ActivarGiroscopio(false);
    }

    private void Update()
    {
        if (Time.timeScale == 0f || ControlesJugador.CursorLibreForzado) return;

        float dt = Time.deltaTime;
        float yaw = 0f, pitch = 0f;   // grados a sumar este frame (pitch positivo = mirar arriba)

        // ---------------- tactil
        if (ControlesJugador.TactilActivo)
        {
            ModoCamara modo = ControlSettings.Modo;
            ActivarGiroscopio(modo == ModoCamara.Giroscopio);

            switch (modo)
            {
                case ModoCamara.Joystick:
                {
                    Vector2 s = ControlesJugador.MirarVirtual;
                    float m = s.magnitude;
                    if (m > 0.001f)
                    {
                        float k = Mathf.Pow(m, curvaStick) / m;   // curva: precision con poco movimiento
                        float v = velocidadStick * ControlSettings.SensibilidadStick * dt;
                        yaw += s.x * k * v;
                        pitch += s.y * k * v;
                    }
                    break;
                }
                case ModoCamara.Arrastrar:
                {
                    Vector2 d = ControlesJugador.ConsumirArrastre();
                    float v = sensibilidadArrastre * ControlSettings.SensibilidadStick;
                    yaw += d.x * v;
                    pitch += d.y * v;
                    break;
                }
                case ModoCamara.Giroscopio:
                {
                    Vector2 g = LeerGiroscopio(dt);
                    yaw += g.x;
                    pitch += g.y;
                    break;
                }
            }

            if (modo != ModoCamara.Arrastrar) ControlesJugador.ConsumirArrastre();
        }
        else
        {
            ActivarGiroscopio(false);
            ControlesJugador.ConsumirArrastre();
        }

        // ---------------- PC: raton (solo con el cursor capturado) y mando
        Mouse mouse = Mouse.current;
        if (mouse != null && ControlesJugador.UsaRaton && ControlesJugador.CursorBloqueado
            && Time.frameCount > ControlesJugador.FrameUltimoBloqueo + 1)
        {
            Vector2 md = mouse.delta.ReadValue();
            float v = sensibilidadMouse * ControlSettings.SensibilidadMouse;
            yaw += md.x * v;
            pitch += md.y * v;
        }

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 rs = gp.rightStick.ReadValue();
            float m = rs.magnitude;
            if (m > 0.15f)
            {
                float k = Mathf.Pow(m, 1.6f) / m;
                float v = velocidadMando * ControlSettings.SensibilidadStick * dt;
                yaw += rs.x * k * v;
                pitch += rs.y * k * v;
            }
        }

        if (ControlSettings.InvertirY) pitch = -pitch;

        rotY += yaw;
        rotX = Mathf.Clamp(rotX - pitch, minY, maxY);
        transform.localRotation = Quaternion.Euler(rotX, rotY, 0f);
    }

    // ------------------------------------------------------------------ giroscopio
    // Se mantiene el mismo calculo que ya tenias (ahora independiente de los FPS).
    private Vector2 LeerGiroscopio(float dt)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        Vector3 g = Input.gyro.rotationRateUnbiased;
        Vector3 rot = new Vector3(-g.x, -g.y, 0f) * gyroSensitivity;

        // Zona muerta: evita que gire solo
        if (rot.magnitude < 0.02f) rot = Vector3.zero;

        float f = 1f - Mathf.Pow(1f - Mathf.Clamp01(gyroSmooth), dt * 60f);
        gyroAcumulado = Vector3.Lerp(gyroAcumulado, rot, f);

        // En tu codigo original: rotX += acc.x (mira abajo) y rotY += acc.y
        float escala = dt * 60f;
        return new Vector2(gyroAcumulado.y * escala, -gyroAcumulado.x * escala);
#else
        return Vector2.zero;
#endif
    }

    private void ActivarGiroscopio(bool activar)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (gyroActivo == activar) return;
        gyroActivo = activar;
        if (SystemInfo.supportsGyroscope) Input.gyro.enabled = activar;
        if (!activar) gyroAcumulado = Vector3.zero;
#endif
    }

    // ------------------------------------------------------------------ para tu slider de sensibilidad
    public void SetGyroSensitivity(float value)
    {
        gyroSensitivity = value;
    }

    public float GetGyroSensitivity()
    {
        return gyroSensitivity;
    }
}
