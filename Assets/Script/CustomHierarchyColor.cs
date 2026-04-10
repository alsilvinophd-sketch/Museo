using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Questo è il "manager" che metti sugli Empty / GameObject in scena
public class CustomHierarchyColor : MonoBehaviour
{
    [Tooltip("Oggetti a cui applicare il colore nella Hierarchy")]
    public List<GameObject> coloredObjects = new List<GameObject>();

    public Color color = Color.yellow;

    [Tooltip("Se lo stesso oggetto compare in più manager, vince quello con priority più alta.")]
    public int priority = 0;

#if UNITY_EDITOR
    [MenuItem("GameObject/Create Custom Hierarchy Color Manager", false, 10)]
    static void CreateCustomHierarchyColorManager(MenuCommand menuCommand)
    {
        GameObject go = new GameObject("HierarchyColorManager");
        go.AddComponent<CustomHierarchyColor>();
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create HierarchyColorManager");
        Selection.activeObject = go;
    }
#endif
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class CustomHierarchyColorDrawer
{
    // Cache: oggetto -> (colore, priority)
    private struct ColorInfo
    {
        public Color color;
        public int priority;
    }

    private static readonly Dictionary<int, ColorInfo> _colorByInstanceId = new();
    private static bool _dirty = true;

    static CustomHierarchyColorDrawer()
    {
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;

        // Quando cambia la gerarchia o la scena, ricostruisci la cache
        EditorApplication.hierarchyChanged += MarkDirty;
        EditorApplication.projectChanged += MarkDirty;
        EditorSceneManagerHook.Install(MarkDirty); // hook per cambi scena (vedi classe sotto)

        // Prima build
        RebuildCache();
    }

    private static void MarkDirty()
    {
        _dirty = true;
    }

    private static void RebuildCache()
    {
        _colorByInstanceId.Clear();

        // Prendi TUTTI i manager in scena (anche inattivi, se vuoi)
        var managers = Object.FindObjectsOfType<CustomHierarchyColor>(true);

        foreach (var manager in managers)
        {
            if (manager == null) continue;

            int prio = manager.priority;
            Color col = manager.color;

            // Aggiungi ogni oggetto mappandolo al suo colore, con gestione priorità
            foreach (var go in manager.coloredObjects)
            {
                if (go == null) continue;

                int id = go.GetInstanceID();
                if (_colorByInstanceId.TryGetValue(id, out var existing))
                {
                    // Se già presente, vince la priority più alta
                    if (prio > existing.priority)
                    {
                        _colorByInstanceId[id] = new ColorInfo { color = col, priority = prio };
                    }
                }
                else
                {
                    _colorByInstanceId.Add(id, new ColorInfo { color = col, priority = prio });
                }
            }
        }

        _dirty = false;
    }

    private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
    {
        if (_dirty) RebuildCache();

        // Se questo oggetto è nella cache, coloralo
        if (_colorByInstanceId.TryGetValue(instanceID, out var info))
        {
            // Background color
            EditorGUI.DrawRect(selectionRect, info.color);

            // Label (testo nero per leggibilità: cambia se vuoi)
            var obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null) return;

            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = Color.black;

            EditorGUI.LabelField(selectionRect, obj.name, style);
        }
    }

    /// <summary>
    /// Mini-hook per intercettare cambi scena senza dipendere da UnityEditor.SceneManagement direttamente qui sopra.
    /// </summary>
    private static class EditorSceneManagerHook
    {
        private static bool _installed;

        public static void Install(System.Action onChange)
        {
            if (_installed) return;
            _installed = true;

            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += (_, __) => onChange();
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (_, __) => onChange();
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += (_) => onChange();
        }
    }
}
#endif
