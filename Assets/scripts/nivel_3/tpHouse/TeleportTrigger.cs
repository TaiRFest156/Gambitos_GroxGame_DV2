
using UnityEngine;

public class TeleportTrigger : MonoBehaviour
{
    [Header("Configuración del Teletransporte")]
    public Vector3 destino = new Vector3(3.41f, 2.18f, -12.52f);
    public float tiempoNecesario = 2f;

    [Header("Sonido")]
    public AudioClip sonidoTeleport;

    private float tiempoDentro = 0f;
    private bool teletransportado = false;

    private Transform jugador;
    private Rigidbody rb;

    private void OnTriggerStay(Collider other)
    {
        // Buscar el objeto principal del jugador
        Transform posibleJugador = other.transform.root;

        if (!posibleJugador.CompareTag("Player"))
            return;

        jugador = posibleJugador;

        if (teletransportado)
            return;

        tiempoDentro += Time.deltaTime;

        Debug.Log("Jugador dentro: " + tiempoDentro.ToString("F2"));

        if (tiempoDentro >= tiempoNecesario)
        {
            teletransportado = true;

            // Reproducir sonido
            if (sonidoTeleport != null)
            {
                AudioSource.PlayClipAtPoint(
                    sonidoTeleport,
                    transform.position
                );
            }

            // Buscar Rigidbody del jugador
            rb = jugador.GetComponent<Rigidbody>();

            // Teletransportar el objeto principal
            if (rb != null)
            {
                rb.position = destino;
                rb.linearVelocity = Vector3.zero;
            }
            else
            {
                CharacterController cc = jugador.GetComponent<CharacterController>();

                if (cc != null)
                    cc.enabled = false;

                jugador.position = destino;

                if (cc != null)
                    cc.enabled = true;
            }

            Debug.Log("TELETRANSPORTE COMPLETADO");
            Debug.Log("Nueva posición del Player: " + jugador.position);

            tiempoDentro = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (jugador == null)
            return;

        if (other.transform.root != jugador)
            return;

        tiempoDentro = 0f;
        teletransportado = false;
        jugador = null;
    }
}
