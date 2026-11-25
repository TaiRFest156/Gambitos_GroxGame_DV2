using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera cam;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0;

    [Header("UI")]
    [SerializeField] private Button interactButton;   // Botón del Canvas

    [SerializeField] private bool mostrarTeclaEnBoton = true;

    private IInteractable current;

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        interactButton.gameObject.SetActive(false);
        interactButton.onClick.AddListener(OnButtonClicked);

        // En PC el boton avisa que se usa con la tecla E
        if (mostrarTeclaEnBoton && ControlesJugador.PCActivo && !ControlesJugador.TactilActivo)
        {
            TMP_Text txt = interactButton.GetComponentInChildren<TMP_Text>(true);
            if (txt != null) txt.text = "Interactuar [" + TeclasJuego.Nombre(TeclasJuego.Obtener(AccionTecla.Interactuar)) + "]";
        }
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;   // pausa o manual abierto
        current = null;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask))
        {
            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanInteract)
                current = interactable;
        }

        interactButton.gameObject.SetActive(current != null);

        // PC / mando: E o boton X
        if (current != null && !ControlesJugador.CursorLibreForzado)
        {
            Gamepad gp = Gamepad.current;
            bool pedido = TeclasJuego.PulsadaEsteFrame(AccionTecla.Interactuar) || (gp != null && gp.buttonWest.wasPressedThisFrame);
            if (pedido) current.Interact();
        }
    }

    private void OnButtonClicked()
    {
        if (current != null) current.Interact();
    }
}