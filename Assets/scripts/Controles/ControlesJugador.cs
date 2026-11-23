using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public enum ModoControles { Auto, SoloTactil, SoloPC }

// Central de controles del Nivel 1 y 3 (Input System):
//  - MOVIL: joystick de camara (o arrastre / giroscopio), boton de salto, boton de fuego.
//           Lee los dedos directamente (multitouch) y respeta tu UI: si tocas un boton tuyo, no gira ni dispara.
//  - PC:    cursor bloqueado, teclado y raton, mando. Esc = pausa, M = manual.
// Se crea solo (lo piden CameraController y CharacterControllerMove).
public class ControlesJugador : MonoBehaviour
{
    // ================================================================== API estatica
    public static ControlesJugador Instance { get; private set; }

    public static ControlesJugador Asegurar()
    {
        if (Instance != null) return Instance;
        ControlesJugador ya = FindFirstObjectByType<ControlesJugador>();
        if (ya != null) return ya;
        return new GameObject("ControlesJugador").AddComponent<ControlesJugador>();
    }

    public static Vector2 MirarVirtual;      // joystick de camara, -1 a 1
    public static Vector2 MoverVirtual;      // joystick de movimiento propio (opcional), -1 a 1
    public static bool SaltoMantenido;
    public static bool CursorLibreForzado;   // paneles que necesitan el raton/dedo (resultados, tarjetas)
    public static int FrameUltimoBloqueo = -10;

    private static bool saltoPulsado, disparoPulsado;
    private static Vector2 arrastre;
    private static ModoControles modoActual = ModoControles.Auto;
    private static CameraController camCtrl;

    public static bool PCActivo
    {
        get { return Keyboard.current != null && Mouse.current != null; }
    }

    public static bool TactilActivo
    {
        get
        {
            switch (modoActual)
            {
                case ModoControles.SoloTactil: return true;
                case ModoControles.SoloPC: return false;
                default: return Application.isMobilePlatform || (Touchscreen.current != null && Keyboard.current == null);
            }
        }
    }

    public static bool UsaRaton
    {
        get { return PCActivo && modoActual != ModoControles.SoloTactil && !Application.isMobilePlatform; }
    }

    public static bool CursorBloqueado
    {
        get { return Cursor.lockState == CursorLockMode.Locked; }
    }

    // Camara del jugador (la del CameraController: durante la cinematica Camera.main puede ser otra)
    public static Transform CamaraJugador
    {
        get
        {
            if (camCtrl == null) camCtrl = FindFirstObjectByType<CameraController>();
            if (camCtrl != null) return camCtrl.transform;
            Camera c = Camera.main;
            return c != null ? c.transform : null;
        }
    }

    public static void PedirSalto() { saltoPulsado = true; }
    public static void PedirDisparo() { disparoPulsado = true; }
    public static bool ConsumirSalto() { bool v = saltoPulsado; saltoPulsado = false; return v; }
    public static bool ConsumirDisparo() { bool v = disparoPulsado; disparoPulsado = false; return v; }
    public static void AcumularArrastre(Vector2 d) { arrastre += d; }
    public static Vector2 ConsumirArrastre() { Vector2 a = arrastre; arrastre = Vector2.zero; return a; }

    // ================================================================== configuracion
    [Header("Plataforma")]
    [Tooltip("Auto = tactil en movil, teclado y raton en PC. Para probar los controles tactiles en el editor con el mouse: SoloTactil.")]
    public ModoControles modo = ModoControles.Auto;

    [Header("Controles tactiles (pixeles sobre 1920x1080)")]
    [Range(0.2f, 1f)] public float opacidad = 0.6f;
    public float radioStick = 130f;
    public float tamanoBotonFuego = 190f;
    public float tamanoBotonSalto = 160f;
    [Tooltip("Posicion de reposo del joystick de camara, desde la esquina inferior derecha")]
    public Vector2 posicionStick = new Vector2(-260f, 260f);
    public Vector2 posicionFuego = new Vector2(-260f, 590f);
    public Vector2 posicionSalto = new Vector2(-580f, 270f);

    [Header("Joystick de movimiento")]
    [Tooltip("Normalmente NO: ya tienes tu joystick en pantalla conectado a 'Move Action'. Activalo solo si quieres que se cree uno nuevo.")]
    public bool crearJoystickMovimiento = false;
    public Vector2 posicionStickMover = new Vector2(260f, 260f);
    [Tooltip("Tu joystick de movimiento actual: se oculta solo cuando se juega con teclado y raton")]
    public GameObject joystickMovimientoExistente;

    [Header("PC")]
    public bool bloquearCursor = true;

    // ================================================================== estado interno
    private enum Rol { Ninguno, Stick, Arrastre, Tap, Fuego, Salto, Mover }

    private class Dedo
    {
        public int id;
        public Vector2 pos, pos0, base0;
        public float t0, dist;
        public Rol rol;
    }

    private readonly Dictionary<int, Dedo> dedos = new Dictionary<int, Dedo>();
    private readonly HashSet<int> vistos = new HashSet<int>();
    private readonly List<int> terminados = new List<int>();
    private readonly List<RaycastResult> resultados = new List<RaycastResult>();

    private Canvas canvas;
    private RectTransform canvasRT, fuegoRT, saltoRT;
    private Image stickBase, stickKnob, moverBase, moverKnob, fuegoImg, saltoImg;
    private bool cursorLibreUsuario;
    private bool ocultoJoystickExistente;

    private static Sprite sDisco, sAnillo, sTriangulo;

    // ================================================================== ciclo de vida
    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        modoActual = modo;

        AsegurarEventSystem();
        ConstruirVisuales();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        // MUY importante: al cambiar de escena (menu, derrota) el cursor debe quedar libre
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        MirarVirtual = Vector2.zero;
        MoverVirtual = Vector2.zero;
        SaltoMantenido = false;
        CursorLibreForzado = false;
        saltoPulsado = false;
        disparoPulsado = false;
        arrastre = Vector2.zero;
    }

    private void Update()
    {
        modoActual = modo;

        AtajosPC();
        ActualizarCursor();

        bool bloqueado = Time.timeScale == 0f || CursorLibreForzado || ManualSystem.EstaAbierto;
        if (TactilActivo && !bloqueado) ProcesarDedos();
        else SoltarTodo();

        ActualizarVisuales();
        OcultarJoystickExistenteEnPC();
    }

    // ================================================================== PC: atajos y cursor
    private void AtajosPC()
    {
        Keyboard kb = Keyboard.current;
        Gamepad gp = Gamepad.current;

        bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
        bool start = gp != null && gp.startButton.wasPressedThisFrame;
        if ((esc || start) && !ManualSystem.EstaAbierto && !CursorLibreForzado) AlternarPausa();

        if (kb != null && kb.mKey.wasPressedThisFrame && Time.timeScale != 0f) ManualSystem.AbrirManual();
    }

    private void AlternarPausa()
    {
        PauseMenu pm = FindFirstObjectByType<PauseMenu>();
        if (pm != null && pm.PausePanel != null)
        {
            if (pm.PausePanel.activeSelf) pm.Continue();
            else pm.Pause();
        }
        else
        {
            cursorLibreUsuario = !cursorLibreUsuario;   // sin menu de pausa: Esc libera/captura el cursor
        }
    }

    private void ActualizarCursor()
    {
        if (!UsaRaton || !bloquearCursor)
        {
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            return;
        }

        bool modal = Time.timeScale == 0f || CursorLibreForzado || ManualSystem.EstaAbierto;
        if (modal)
        {
            cursorLibreUsuario = false;
            Liberar();
            return;
        }

        if (cursorLibreUsuario)
        {
            Mouse m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame && !UIBloquea(m.position.ReadValue()))
                cursorLibreUsuario = false;   // un clic en el juego vuelve a capturar el cursor
            else { Liberar(); return; }
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            FrameUltimoBloqueo = Time.frameCount;
        }
    }

    private void Liberar()
    {
        if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OcultarJoystickExistenteEnPC()
    {
        if (joystickMovimientoExistente == null) return;

        // Solo se oculta (una vez) cuando se juega con teclado y raton
        if (UsaRaton && !TactilActivo && !ocultoJoystickExistente)
        {
            if (Time.frameCount > 3)
            {
                joystickMovimientoExistente.SetActive(false);
                ocultoJoystickExistente = true;
            }
        }
    }

    // ================================================================== tactil: dedos
    private void ProcesarDedos()
    {
        vistos.Clear();

        Touchscreen ts = Touchscreen.current;
        if (ts != null)
        {
            for (int i = 0; i < ts.touches.Count; i++)
            {
                TouchControl t = ts.touches[i];
                if (!t.isInProgress) continue;
                int id = t.touchId.ReadValue();
                vistos.Add(id);
                Procesar(id, t.position.ReadValue());
            }
        }
        else if (modoActual == ModoControles.SoloTactil && Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            // Para probar en el editor: el mouse hace de dedo
            vistos.Add(-1);
            Procesar(-1, Mouse.current.position.ReadValue());
        }

        terminados.Clear();
        foreach (KeyValuePair<int, Dedo> kv in dedos)
            if (!vistos.Contains(kv.Key)) terminados.Add(kv.Key);
        foreach (int id in terminados) Terminar(id);
    }

    private void Procesar(int id, Vector2 pos)
    {
        Dedo d;
        if (!dedos.TryGetValue(id, out d))
        {
            d = Empezar(id, pos);
            dedos[id] = d;
        }

        Vector2 anterior = d.pos;
        d.pos = pos;

        switch (d.rol)
        {
            case Rol.Stick:
                MirarVirtual = VectorStick(d);
                break;
            case Rol.Mover:
                MoverVirtual = VectorStick(d);
                break;
            case Rol.Arrastre:
            case Rol.Tap:
                Vector2 delta = pos - anterior;
                d.dist += delta.magnitude;
                if (d.rol == Rol.Arrastre) AcumularArrastre(delta);
                break;
        }
    }

    private Dedo Empezar(int id, Vector2 pos)
    {
        Dedo d = new Dedo();
        d.id = id;
        d.pos = pos;
        d.pos0 = pos;
        d.t0 = Time.unscaledTime;
        d.rol = Rol.Ninguno;

        if (UIBloquea(pos)) return d;   // lo agarro un boton/joystick de TU interfaz

        float W = Screen.width, H = Screen.height;
        ModoCamara mc = ControlSettings.Modo;

        if (EnRect(fuegoRT, pos))
        {
            d.rol = Rol.Fuego;
            PedirDisparo();
        }
        else if (EnRect(saltoRT, pos))
        {
            d.rol = Rol.Salto;
            PedirSalto();
            SaltoMantenido = true;
        }
        else if (mc == ModoCamara.Joystick && pos.x >= W * 0.5f && pos.y <= H * 0.65f)
        {
            d.rol = Rol.Stick;
            d.base0 = LimitarABase(pos);
        }
        else if (mc != ModoCamara.Joystick && pos.x >= W * 0.5f && pos.y <= H * 0.80f)
        {
            d.rol = mc == ModoCamara.Arrastrar ? Rol.Arrastre : Rol.Tap;   // giroscopio: tocar dispara
        }
        else if (crearJoystickMovimiento && pos.x < W * 0.5f && pos.y <= H * 0.65f)
        {
            d.rol = Rol.Mover;
            d.base0 = LimitarABase(pos);
        }
        return d;
    }

    private void Terminar(int id)
    {
        Dedo d;
        if (!dedos.TryGetValue(id, out d)) return;

        switch (d.rol)
        {
            case Rol.Stick: MirarVirtual = Vector2.zero; break;
            case Rol.Mover: MoverVirtual = Vector2.zero; break;
            case Rol.Salto: SaltoMantenido = false; break;
            case Rol.Arrastre:
            case Rol.Tap:
                // Toque corto sin mover = disparo
                if (Time.unscaledTime - d.t0 < 0.25f && d.dist < Screen.height * 0.025f) PedirDisparo();
                break;
        }
        dedos.Remove(id);
    }

    private void SoltarTodo()
    {
        if (dedos.Count > 0) dedos.Clear();
        MirarVirtual = Vector2.zero;
        MoverVirtual = Vector2.zero;
        SaltoMantenido = false;
        arrastre = Vector2.zero;
    }

    private Vector2 LimitarABase(Vector2 p)
    {
        float r = radioStick * ScaleFactor();
        p.x = Mathf.Clamp(p.x, r, Screen.width - r);
        p.y = Mathf.Clamp(p.y, r, Screen.height - r);
        return p;
    }

    private Vector2 VectorStick(Dedo d)
    {
        float r = Mathf.Max(1f, radioStick * ScaleFactor());
        Vector2 v = (d.pos - d.base0) / r;
        if (v.magnitude > 1f) v.Normalize();

        const float zona = 0.1f;
        float m = v.magnitude;
        if (m < zona) return Vector2.zero;
        return v.normalized * ((m - zona) / (1f - zona));
    }

    private float ScaleFactor()
    {
        return canvas != null ? canvas.scaleFactor : 1f;
    }

    private bool EnRect(RectTransform rt, Vector2 pos)
    {
        return rt != null && rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null);
    }

    // ¿Hay un boton/joystick/slider de la interfaz del juego bajo este dedo?
    private bool UIBloquea(Vector2 pos)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        PointerEventData ped = new PointerEventData(es);
        ped.position = pos;
        resultados.Clear();
        es.RaycastAll(ped, resultados);

        foreach (RaycastResult r in resultados)
        {
            GameObject go = r.gameObject;
            if (go == null) continue;
            if (ExecuteEvents.GetEventHandler<IPointerDownHandler>(go) != null ||
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null ||
                ExecuteEvents.GetEventHandler<IDragHandler>(go) != null)
                return true;
        }
        return false;
    }

    private void AsegurarEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        UnityEngine.InputSystem.UI.InputSystemUIInputModule modulo = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        modulo.AssignDefaultActions();
    }

    // ================================================================== visuales (solo dibujo, sin raycast)
    private void ConstruirVisuales()
    {
        GameObject cgo = new GameObject("ControlesTactiles", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler sc = cgo.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 0.5f;
        canvasRT = (RectTransform)cgo.transform;

        stickBase = NuevoStick("StickCamara", radioStick * 2f, out stickKnob);
        moverBase = NuevoStick("StickMover", radioStick * 2f, out moverKnob);

        fuegoRT = NuevoBoton("BotonFuego", new Vector2(1f, 0f), posicionFuego, tamanoBotonFuego,
                             new Color(1f, 0.45f, 0.3f), true, out fuegoImg);
        saltoRT = NuevoBoton("BotonSalto", new Vector2(1f, 0f), posicionSalto, tamanoBotonSalto,
                             new Color(0.2f, 0.9f, 0.65f), false, out saltoImg);
    }

    private Image NuevoStick(string nombre, float tam, out Image knob)
    {
        RectTransform rt = NuevoRect(nombre, canvasRT);
        rt.sizeDelta = new Vector2(tam, tam);
        Image b = rt.gameObject.AddComponent<Image>();
        b.sprite = SpriteDisco();
        b.raycastTarget = false;

        RectTransform anillo = NuevoRect("Anillo", rt);
        anillo.anchorMin = Vector2.zero;
        anillo.anchorMax = Vector2.one;
        anillo.offsetMin = anillo.offsetMax = Vector2.zero;
        Image ai = anillo.gameObject.AddComponent<Image>();
        ai.sprite = SpriteAnillo();
        ai.raycastTarget = false;

        RectTransform k = NuevoRect(nombre + "_Knob", canvasRT);
        k.sizeDelta = new Vector2(tam * 0.42f, tam * 0.42f);
        knob = k.gameObject.AddComponent<Image>();
        knob.sprite = SpriteDisco();
        knob.raycastTarget = false;
        return b;
    }

    private RectTransform NuevoBoton(string nombre, Vector2 ancla, Vector2 pos, float tam, Color color, bool fuego, out Image fondo)
    {
        RectTransform rt = NuevoRect(nombre, canvasRT);
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.sizeDelta = new Vector2(tam, tam);
        rt.anchoredPosition = pos;

        fondo = rt.gameObject.AddComponent<Image>();
        fondo.sprite = SpriteDisco();
        fondo.color = new Color(color.r, color.g, color.b, 0.45f);
        fondo.raycastTarget = false;

        RectTransform anillo = NuevoRect("Anillo", rt);
        anillo.anchorMin = Vector2.zero;
        anillo.anchorMax = Vector2.one;
        anillo.offsetMin = anillo.offsetMax = Vector2.zero;
        Image ai = anillo.gameObject.AddComponent<Image>();
        ai.sprite = SpriteAnillo();
        ai.color = new Color(1f, 1f, 1f, 0.85f);
        ai.raycastTarget = false;

        if (fuego)
        {
            // Mira: anillo chico + punto
            RectTransform mira = NuevoRect("Mira", rt);
            mira.sizeDelta = new Vector2(tam * 0.5f, tam * 0.5f);
            Image mi = mira.gameObject.AddComponent<Image>();
            mi.sprite = SpriteAnillo();
            mi.color = Color.white;
            mi.raycastTarget = false;

            RectTransform punto = NuevoRect("Punto", rt);
            punto.sizeDelta = new Vector2(tam * 0.12f, tam * 0.12f);
            Image pi = punto.gameObject.AddComponent<Image>();
            pi.sprite = SpriteDisco();
            pi.color = Color.white;
            pi.raycastTarget = false;
        }
        else
        {
            // Flecha hacia arriba
            RectTransform flecha = NuevoRect("Flecha", rt);
            flecha.sizeDelta = new Vector2(tam * 0.55f, tam * 0.55f);
            flecha.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Image fi = flecha.gameObject.AddComponent<Image>();
            fi.sprite = SpriteTriangulo();
            fi.color = Color.white;
            fi.raycastTarget = false;
        }
        return rt;
    }

    private RectTransform NuevoRect(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private Vector2 Local(Vector2 pantalla)
    {
        Vector2 l;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, pantalla, null, out l);
        return l;
    }

    private void ActualizarVisuales()
    {
        bool tactil = TactilActivo && Time.timeScale != 0f && !CursorLibreForzado && !ManualSystem.EstaAbierto;
        if (canvas.gameObject.activeSelf != tactil) canvas.gameObject.SetActive(tactil);
        if (!tactil) return;

        Rect r = canvasRT.rect;
        float a = opacidad;

        // ---- joystick de camara
        bool verStick = ControlSettings.Modo == ModoCamara.Joystick;
        stickBase.gameObject.SetActive(verStick);
        stickKnob.gameObject.SetActive(verStick);
        if (verStick)
        {
            Vector2 reposo = new Vector2(r.width * 0.5f + posicionStick.x, -r.height * 0.5f + posicionStick.y);
            Dedo d = DedoConRol(Rol.Stick);
            DibujarStick(stickBase, stickKnob, d, MirarVirtual, reposo, a);
        }

        // ---- joystick de movimiento propio
        moverBase.gameObject.SetActive(crearJoystickMovimiento);
        moverKnob.gameObject.SetActive(crearJoystickMovimiento);
        if (crearJoystickMovimiento)
        {
            Vector2 reposo = new Vector2(-r.width * 0.5f + posicionStickMover.x, -r.height * 0.5f + posicionStickMover.y);
            Dedo d = DedoConRol(Rol.Mover);
            DibujarStick(moverBase, moverKnob, d, MoverVirtual, reposo, a);
        }

        // ---- botones
        bool fuegoPresionado = DedoConRol(Rol.Fuego) != null;
        bool saltoPresionado = DedoConRol(Rol.Salto) != null;
        fuegoRT.localScale = Vector3.one * (fuegoPresionado ? 0.9f : 1f);
        saltoRT.localScale = Vector3.one * (saltoPresionado ? 0.9f : 1f);
        fuegoImg.color = new Color(fuegoImg.color.r, fuegoImg.color.g, fuegoImg.color.b, fuegoPresionado ? 0.85f : a * 0.75f);
        saltoImg.color = new Color(saltoImg.color.r, saltoImg.color.g, saltoImg.color.b, saltoPresionado ? 0.85f : a * 0.75f);
    }

    private Dedo DedoConRol(Rol rol)
    {
        foreach (KeyValuePair<int, Dedo> kv in dedos)
            if (kv.Value.rol == rol) return kv.Value;
        return null;
    }

    private void DibujarStick(Image bas, Image knob, Dedo d, Vector2 valor, Vector2 reposo, float a)
    {
        RectTransform brt = bas.rectTransform;
        RectTransform krt = knob.rectTransform;
        brt.sizeDelta = new Vector2(radioStick * 2f, radioStick * 2f);

        if (d != null)
        {
            Vector2 centro = Local(d.base0);
            brt.anchoredPosition = centro;
            krt.anchoredPosition = centro + valor * radioStick;
            bas.color = new Color(1f, 1f, 1f, a * 0.5f);
            knob.color = new Color(1f, 1f, 1f, a);
        }
        else
        {
            brt.anchoredPosition = reposo;
            krt.anchoredPosition = reposo;
            bas.color = new Color(1f, 1f, 1f, a * 0.28f);
            knob.color = new Color(1f, 1f, 1f, a * 0.4f);
        }
    }

    // ------------------------------------------------------------------ sprites generados por codigo
    private static Sprite SpriteDisco()
    {
        if (sDisco != null) return sDisco;
        const int n = 64;
        Color32[] c = new Color32[n * n];
        float m = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - m, dy = y + 0.5f - m;
                float al = Mathf.Clamp01(m - 1f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                c[y * n + x] = new Color32(255, 255, 255, (byte)(al * 255f));
            }
        sDisco = Crear(c, n);
        return sDisco;
    }

    private static Sprite SpriteAnillo()
    {
        if (sAnillo != null) return sAnillo;
        const int n = 128;
        Color32[] c = new Color32[n * n];
        float m = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - m) / m, dy = (y + 0.5f - m) / m;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float al = Mathf.Clamp01((0.07f - Mathf.Abs(d - 0.9f)) * m * 0.5f);
                c[y * n + x] = new Color32(255, 255, 255, (byte)(al * 255f));
            }
        sAnillo = Crear(c, n);
        return sAnillo;
    }

    private static Sprite SpriteTriangulo()
    {
        if (sTriangulo != null) return sTriangulo;
        const int n = 128;
        Color32[] c = new Color32[n * n];
        Vector2 a = new Vector2(0.2f, 0.12f), b = new Vector2(0.85f, 0.5f), cc = new Vector2(0.2f, 0.88f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                float d = Mathf.Min(Dist(p, a, b), Mathf.Min(Dist(p, b, cc), Dist(p, cc, a)));
                c[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d * n + 0.5f) * 255f));
            }
        sTriangulo = Crear(c, n);
        return sTriangulo;
    }

    private static float Dist(Vector2 p, Vector2 p0, Vector2 p1)
    {
        Vector2 e = p1 - p0;
        return Vector2.Dot(p - p0, new Vector2(-e.y, e.x).normalized);
    }

    private static Sprite Crear(Color32[] c, int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
