using UnityEngine;

public enum TipoAudio { Musica, Efecto }

// Agregalo a un AudioSource para decirle a las Opciones si es MUSICA o EFECTO.
// Sin este script, se detecta solo: un sonido en bucle de 15 segundos o mas es musica; el resto, efectos.
[DisallowMultipleComponent]
public class CategoriaAudio : MonoBehaviour
{
    public TipoAudio tipo = TipoAudio.Efecto;

    [Tooltip("Las Opciones no tocan el volumen de este sonido (uso interno)")]
    public bool ignorar = false;
}
