using System.IO;
using System.Linq;
using AW.SUR.MI._74;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

public static class CustomMainToolbar
{
    #region Clear Game Data

    [MainToolbarElement ("Project/Clear Game Data", defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateClearGameDataButton ()
    {
        Texture2D icon = EditorGUIUtility.IconContent ("Clear").image as Texture2D;

        MainToolbarContent content = new (
            "Clear Game Data", icon, "Clears all game data and resets the game to its initial state."
        );

        return new MainToolbarButton (content, ClearGameData);
    }

    private static void ClearGameData ()
    {
        ClearPlayerPrefs ();
        ClearPersistentDataPath ();
    }

    private static void ClearPlayerPrefs ()
    {
        PlayerPrefs.DeleteAll ();

        Debug.Log ("PlayerPrefs cleared.".Red (true, true));
    }

    private static void ClearPersistentDataPath ()
    {
        string persistentDataPath = Application.persistentDataPath;

        if (!Directory.Exists (persistentDataPath)) return;

        Directory.Delete (persistentDataPath, true);

        Debug.Log ("Persistent data path cleared.".Red (true, true));
    }

    #endregion

    #region Scenes

    [MainToolbarElement ("Project/Scenes", defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateScenesButton ()
    {
        Texture2D icon = EditorGUIUtility.IconContent ("Scene").image as Texture2D;

        MainToolbarContent content = new (icon, "Open a scene from Build Settings.");

        return new MainToolbarButton (content, ShowScenesPopup);
    }

    private static void ShowScenesPopup () { SceneSelectionPopup.Show (); }

    #endregion

    #region Script Creation

    [MainToolbarElement ("Project/Create Script", defaultDockPosition = MainToolbarDockPosition.Left)]
    public static MainToolbarElement CreateScriptButton ()
    {
        Texture2D icon = EditorGUIUtility.IconContent ("cs Script Icon").image as Texture2D;

        MainToolbarContent content = new (icon, "Creates a new C# script in the selected folder.");

        return new MainToolbarButton (content, CreateNewScript);
    }

    private static void CreateNewScript ()
    {
        EditorApplication.ExecuteMenuItem ("Assets/Create/Scripting/MonoBehaviour Script");
    }

    #endregion
}

#region Scene Selection Popup

internal sealed class SceneSelectionPopup : EditorWindow
{
    private const float WIDTH = 320f;
    private const float ROW_HEIGHT = 28f;
    private const float HEADER_HEIGHT = 30f;
    private const float FOOTER_HEIGHT = 24f;

    public new static void Show ()
    {
        SceneSelectionPopup popup = CreateInstance <SceneSelectionPopup> ();

        popup.titleContent = new GUIContent ("Scenes");

        popup.BuildUI ();

        EditorWindow focusedEditorWindow = focusedWindow;

        Rect anchorRect;

        if (focusedEditorWindow)
        {
            Vector2 mousePosition = GUIUtility.GUIToScreenPoint (
                Event.current != null ? Event.current.mousePosition : Vector2.zero
            );

            anchorRect = new Rect (mousePosition.x, mousePosition.y, 1f, 1f);
        }
        else
        {
            Vector2 mousePosition = GUIUtility.GUIToScreenPoint (GUIUtility.GUIToScreenPoint (Vector2.zero));

            anchorRect = new Rect (mousePosition.x, mousePosition.y, 1f, 1f);
        }

        int sceneCount = GetEnabledSceneCount ();

        float height = HEADER_HEIGHT + Mathf.Max (sceneCount, 1) * ROW_HEIGHT + FOOTER_HEIGHT;

        popup.ShowAsDropDown (anchorRect, new Vector2 (WIDTH, height));
    }

    private void BuildUI ()
    {
        rootVisualElement.Clear ();

        rootVisualElement.style.minWidth = WIDTH;
        rootVisualElement.style.maxWidth = WIDTH;

        CreateHeader ();

        EditorBuildSettingsScene [] scenes = EditorBuildSettings.scenes;

        bool hasScenes = false;

        foreach (EditorBuildSettingsScene buildScene in scenes)
        {
            if (!buildScene.enabled) continue;

            if (string.IsNullOrEmpty (buildScene.path)) continue;

            hasScenes = true;

            CreateSceneRow (buildScene.path);
        }

        if (!hasScenes)
        {
            Label emptyLabel = new ("No enabled scenes in Build Settings.")
            {
                style = { height = ROW_HEIGHT, unityTextAlign = TextAnchor.MiddleCenter }
            };

            rootVisualElement.Add (emptyLabel);
        }

        CreateFooter ();
    }

    private void CreateHeader ()
    {
        VisualElement header = new ()
        {
            style =
            {
                height = HEADER_HEIGHT,
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                paddingLeft = 8,
                paddingRight = 8
            }
        };

        Label label = new ("Scenes")
        {
            style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
        };

        header.Add (label);

        rootVisualElement.Add (header);
    }

    private void CreateSceneRow (string scenePath)
    {
        string sceneName = Path.GetFileNameWithoutExtension (scenePath);

        VisualElement row = new ()
        {
            style =
            {
                height = ROW_HEIGHT,
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                paddingLeft = 6,
                paddingRight = 4
            }
        };

        // Scene icon
        Object sceneAsset = AssetDatabase.LoadAssetAtPath <Object> (scenePath);

        Image sceneIcon = new ()
        {
            image = AssetPreview.GetMiniThumbnail (sceneAsset),
            style = { width = 18, height = 18, marginRight = 6 }
        };

        row.Add (sceneIcon);

        // Scene name / Open button
        Button openButton = new (() => OpenScene (scenePath))
        {
            text = sceneName,
            style = { flexGrow = 1, height = 24, unityTextAlign = TextAnchor.MiddleLeft }
        };

        row.Add (openButton);

        // Locate button
        Button locateButton = new (() => LocateScene (scenePath))
        {
            tooltip = "Locate scene in Project window",
            style = { width = 24, height = 24, marginLeft = 4 }
        };

        Texture2D locateIcon = EditorGUIUtility.IconContent ("d_Search Icon").image as Texture2D;

        locateButton.style.backgroundImage = locateIcon;

        row.Add (locateButton);

        rootVisualElement.Add (row);
    }

    private void CreateFooter ()
    {
        VisualElement footer = new ()
        {
            style =
            {
                height = FOOTER_HEIGHT,
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                paddingLeft = 8,
                paddingRight = 8
            }
        };

        Label hint = new ("Click scene to open • 🔎 to locate")
        {
            style = { fontSize = 10, opacity = 0.6f }
        };

        footer.Add (hint);

        rootVisualElement.Add (footer);
    }

    private void OpenScene (string scenePath)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo ()) return;

        ClosePopup ();
        
        EditorSceneManager.OpenScene (scenePath, OpenSceneMode.Single);

        GUIUtility.ExitGUI ();
    }

    private void LocateScene (string scenePath)
    {
        Object sceneAsset = AssetDatabase.LoadAssetAtPath <Object> (scenePath);

        if (!sceneAsset) return;

        Selection.activeObject = sceneAsset;

        EditorGUIUtility.PingObject (sceneAsset);
        
        ClosePopup ();

        GUIUtility.ExitGUI ();
    }

    private static int GetEnabledSceneCount ()
    {
        return EditorBuildSettings.scenes.Count (scene => scene.enabled && !string.IsNullOrEmpty (scene.path));
    }

    private void ClosePopup () => Close ();
}

#endregion
