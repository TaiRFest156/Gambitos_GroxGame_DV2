using TMPro;
using UnityEngine;

// Para conectar el modo de camara a TU menu de opciones o de pausa, sin escribir codigo:
//  - Botones:  OnClick -> SelectorModoCamara.UsarJoystick / UsarGiroscopio / UsarArrastre
//  - Dropdown: arrastralo al campo "Dropdown" (opciones en este orden: Joystick, Giroscopio, Arrastrar)
//  - Sliders / toggles: SetSensibilidadStick, SetSensibilidadMouse, SetInvertirY
public class SelectorModoCamara : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    private void Start()
    {
        if (dropdown != null)
        {
            dropdown.SetValueWithoutNotify((int)ControlSettings.Modo);
            dropdown.onValueChanged.AddListener(SetModo);
        }
    }

    public void UsarJoystick() { ControlSettings.Modo = ModoCamara.Joystick; }
    public void UsarGiroscopio() { ControlSettings.Modo = ModoCamara.Giroscopio; }
    public void UsarArrastre() { ControlSettings.Modo = ModoCamara.Arrastrar; }
    public void SetModo(int indice) { ControlSettings.Modo = (ModoCamara)Mathf.Clamp(indice, 0, 2); }

    public void SetSensibilidadStick(float v) { ControlSettings.SensibilidadStick = v; }
    public void SetSensibilidadMouse(float v) { ControlSettings.SensibilidadMouse = v; }
    public void SetInvertirY(bool v) { ControlSettings.InvertirY = v; }
}
