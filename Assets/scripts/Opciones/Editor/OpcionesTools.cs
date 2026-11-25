using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Herramientas de menu para Opciones y Pausa. Debe estar en una carpeta llamada "Editor".
public static class OpcionesTools
{
    [MenuItem("Grox/Opciones/1. Agregar boton de Opciones a esta escena (menu)")]
    public static void AgregarBotonOpciones()
    {
        if (Object.FindFirstObjectByType<OpcionesMenu>() != null)
        {
            EditorUtility.DisplayDialog("Opciones", "Esta escena ya tiene un OpcionesMenu.", "OK");
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Opciones", "No encontré ningún Canvas en esta escena.", "OK");
            return;
        }

        GameObject sistema = new GameObject("OpcionesMenu");
        Undo.RegisterCreatedObjectUndo(sistema, "Agregar OpcionesMenu");
        OpcionesMenu om = sistema.AddComponent<OpcionesMenu>();

        GameObject go = new GameObject("BotonOpciones", typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Agregar boton Opciones");
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 110f);
        rt.anchoredPosition = new Vector2(0f, -400f);
        go.GetComponent<Image>().color = new Color32(70, 140, 255, 255);

        GameObject tgo = new GameObject("Texto", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        RectTransform trt = (RectTransform)tgo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = tgo.AddComponent<TextMeshProUGUI>();
        t.text = "Opciones";
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 48;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;

        UnityEventTools.AddPersistentListener(go.GetComponent<Button>().onClick, om.Abrir);

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
