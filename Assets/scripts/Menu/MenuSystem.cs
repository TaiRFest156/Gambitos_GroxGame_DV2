using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuSystem : MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene("LevelSelector");
    }

    // Conecta tu boton "Salir" a ESTE metodo: muestra la ventana "¿Seguro que quieres salir?" con el estilo del juego
    public void ConfirmarSalir()
    {
        VentanaConfirmar.Mostrar("¿SALIR DEL JUEGO?", "¿Seguro que quieres salir?", Exit);
    }

    public void Exit()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }
}
