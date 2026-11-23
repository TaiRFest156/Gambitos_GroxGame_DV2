using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Herramientas de menu para el sistema de puntaje. Debe estar en una carpeta llamada "Editor".
public static class PuntajeTools
{
    [MenuItem("Grox/Puntaje/1. Crear configuracion y estilo")]
    public static void CrearConfig()
    {
        const string ruta = "Assets/Resources/PuntajeConfig.asset";
        PuntajeConfig existente = AssetDatabase.LoadAssetAtPath<PuntajeConfig>(ruta);
        if (existente != null)
        {
            Selection.activeObject = existente;
            EditorGUIUtility.PingObject(existente);
            EditorUtility.DisplayDialog("Puntaje", "Ya existe PuntajeConfig. Lo seleccioné para que lo edites.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        PuntajeConfig cfg = ScriptableObject.CreateInstance<PuntajeConfig>();
        AssetDatabase.CreateAsset(cfg, ruta);
        AssetDatabase.SaveAssets();
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);
        Debug.Log("Puntaje: configuración creada en " + ruta + ". Revisa los nombres de escena ('Elegir nivel' y 'Menú').");
    }

    [MenuItem("Grox/Puntaje/2. Agregar boton de Puntajes a esta escena (menu)")]
    public static void AgregarBotonMenu()
    {
        if (Object.FindFirstObjectByType<RankingMenu>() != null)
        {
            EditorUtility.DisplayDialog("Puntaje", "Esta escena ya tiene un RankingMenu.", "OK");
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Puntaje", "No encontré ningún Canvas en esta escena.", "OK");
            return;
        }

        GameObject sistema = new GameObject("RankingMenu");
        Undo.RegisterCreatedObjectUndo(sistema, "Agregar RankingMenu");
        RankingMenu rm = sistema.AddComponent<RankingMenu>();

        GameObject go = new GameObject("BotonPuntajes", typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Agregar boton Puntajes");
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 110f);
        rt.anchoredPosition = new Vector2(0f, -260f);

        go.GetComponent<Image>().color = new Color32(46, 230, 166, 255);

        GameObject tgo = new GameObject("Texto", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        RectTransform trt = (RectTransform)tgo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = tgo.AddComponent<TextMeshProUGUI>();
        t.text = "Puntajes";
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 48;
        t.fontStyle = FontStyles.Bold;
        t.color = new Color32(24, 28, 52, 255);

        UnityEventTools.AddPersistentListener(go.GetComponent<Button>().onClick, rm.Abrir);

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    [MenuItem("Grox/Puntaje/3. Borrar puntajes guardados (solo pruebas)")]
    public static void BorrarPuntajes()
    {
        if (EditorUtility.DisplayDialog("Puntaje", "¿Borrar todos los puntajes y récords guardados en este equipo?", "Borrar", "Cancelar"))
        {
            RankingService.BorrarTodoLocal();
            Debug.Log("Puntaje: puntajes locales borrados.");
        }
    }
}
