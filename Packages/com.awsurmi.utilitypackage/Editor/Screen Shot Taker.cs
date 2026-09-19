using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class ScreenShotTaker : MonoBehaviour
{
    public enum KeyBindingMode
    {
        KeyboardKey,
        InputAction
    }

    [Header ("Key Binding")]
    [Tooltip ("KeyboardKey: single-key trigger.\nInputAction: supports gamepad / combos.")]
    [SerializeField]
    private KeyBindingMode bindingMode = KeyBindingMode.KeyboardKey;

    [Tooltip ("Key used when Binding Mode is KeyboardKey.")] [SerializeField]
    private Key key = Key.M;

    [Tooltip ("Button-type InputAction used when Binding Mode is InputAction.")] [SerializeField]
    private InputAction screenshotAction = new (
        name: "Screenshot", type: InputActionType.Button, binding: "<Keyboard>/m"
    );

    [Header ("Output")] [Tooltip ("Folder where screenshots are saved.")] [SerializeField]
    private string outputPath = Environment.GetFolderPath (Environment.SpecialFolder.MyPictures);

    public KeyBindingMode BindingMode
    {
        get => bindingMode;
        set => bindingMode = value;
    }

    public Key Key
    {
        get => key;
        set => key = value;
    }

    public string OutputPath
    {
        get => outputPath;
        set => outputPath = value;
    }

    private void OnEnable ()
    {
        if (bindingMode != KeyBindingMode.InputAction || screenshotAction == null) return;

        screenshotAction.performed += OnActionPerformed;
        screenshotAction.Enable ();
    }

    private void OnDisable ()
    {
        if (bindingMode != KeyBindingMode.InputAction || screenshotAction == null) return;

        screenshotAction.performed -= OnActionPerformed;
        screenshotAction.Disable ();
    }

    private void Update ()
    {
        if (bindingMode != KeyBindingMode.KeyboardKey) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current [key].wasPressedThisFrame) TakeScreenshot ();
    }

    private void OnActionPerformed (InputAction.CallbackContext _) => TakeScreenshot ();

    public void TakeScreenshot ()
    {
        string filePath = Path.Combine (outputPath, $"Screenshot_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.png");

        ScreenCapture.CaptureScreenshot (filePath);
        print ($"<color=cyan> [ScreenShotTaker] Saved → {filePath} </color>");
    }
}

#if UNITY_EDITOR

internal static class ScreenshotCapture
{
    // ─── Public entry points ────────────────────────────────────────────────

    public static void CaptureSceneView (string outputPath, Action onComplete = null)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (!sceneView)
        {
            Debug.LogWarning ("[ScreenshotTaker] No SceneView found.");
            return;
        }

        EnsureDirectory (outputPath);

        Camera sceneCam = sceneView.camera;
        if (!sceneCam)
        {
            Debug.LogWarning ("[ScreenshotTaker] SceneView camera missing.");
            return;
        }

        int width = Mathf.Max (1, (int) sceneView.position.width);
        int height = Mathf.Max (1, (int) sceneView.position.height);
        string filePath = Path.Combine (outputPath, $"Scene_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.png");

        RenderSceneAsync (sceneCam, width, height, filePath, onComplete);
    }

    public static void CaptureGameCamera (
        Camera cam, int width, int height, string outputPath,
        string filePrefix = "Game", Action onComplete = null)
    {
        if (!cam)
        {
            Debug.LogWarning ("[ScreenshotTaker] No camera provided for Game View capture.");
            return;
        }

        EnsureDirectory (outputPath);

        string filePath = Path.Combine (outputPath, $"{filePrefix}_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.png");

        OffscreenRenderAsync (cam, width, height, filePath, onComplete);
    }

    // ─── Scene capture (async) ───────────────────────────────────────────────

    /// <summary>
    /// Replaces the old synchronous RenderSceneManually.
    /// GPU readback is non-blocking; file write runs on a thread-pool thread.
    /// </summary>
    private static void RenderSceneAsync (
        Camera sceneCam, int width, int height, string filePath,
        Action onComplete)
    {
        // 1. Spin up a temporary camera that mirrors the scene view camera.
        GameObject tempGo = new ("TempSceneCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
        Camera tempCam = tempGo.AddComponent <Camera> ();

        tempCam.transform.position = sceneCam.transform.position;
        tempCam.transform.rotation = sceneCam.transform.rotation;
        tempCam.fieldOfView = sceneCam.fieldOfView;
        tempCam.orthographic = sceneCam.orthographic;
        tempCam.orthographicSize = sceneCam.orthographicSize;
        tempCam.nearClipPlane = sceneCam.nearClipPlane;
        tempCam.farClipPlane = sceneCam.farClipPlane;
        tempCam.clearFlags = sceneCam.clearFlags;
        tempCam.backgroundColor = sceneCam.backgroundColor;
        tempCam.cullingMask = sceneCam.cullingMask;
        tempCam.allowHDR = sceneCam.allowHDR;
        tempCam.allowMSAA = true;

        // 2. Render into an RT (no CPU stall here).
        RenderTexture renderTexture = new (width, height, 24, RenderTextureFormat.ARGB32)
            { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
        tempCam.targetTexture = renderTexture;
        tempCam.Render ();
        tempCam.targetTexture = null;

        Object.DestroyImmediate (tempGo); // camera no longer needed

        // 3. Read pixels back from GPU without stalling the main thread.
        AsyncGPUReadback.Request (renderTexture, 0, TextureFormat.RGB24, req =>
        {
            // Callback fires on the main thread once the GPU is done.
            Object.DestroyImmediate (renderTexture);

            if (req.hasError)
            {
                Debug.LogWarning ("[ScreenshotTaker] AsyncGPUReadback failed (Scene).");
                onComplete?.Invoke ();
                return;
            }

            // 4. Encode to PNG on the main thread (Unity API requirement).
            Texture2D texture2D = new (width, height, TextureFormat.RGB24, false)
                { hideFlags = HideFlags.HideAndDontSave };
            texture2D.LoadRawTextureData (req.GetData <byte> ());
            texture2D.Apply (false);
            byte [] pngBytes = texture2D.EncodeToPNG ();
            Object.DestroyImmediate (texture2D);

            // 5. Write to disk on a background thread — no more main-thread stall.
            WriteAsync (pngBytes, filePath, onComplete);
        });
    }

    // ─── Game / off-screen capture (async) ──────────────────────────────────

    /// <summary>
    /// Replaces the old synchronous OffscreenRender.
    /// </summary>
    private static void OffscreenRenderAsync (
        Camera camera, int width, int height, string filePath,
        Action onComplete)
    {
        width = Mathf.Max (1, width);
        height = Mathf.Max (1, height);

        RenderTexture renderTexture = new (width, height, 24, RenderTextureFormat.ARGB32)
            { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };

        // Render (queues GPU work, does NOT stall).
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = renderTexture;
        camera.Render ();
        camera.targetTexture = previousTarget;

        // Non-blocking GPU readback.
        AsyncGPUReadback.Request (renderTexture, 0, TextureFormat.RGB24, req =>
        {
            renderTexture.Release ();
            Object.DestroyImmediate (renderTexture);

            if (req.hasError)
            {
                Debug.LogWarning ("[ScreenshotTaker] AsyncGPUReadback failed (Game).");
                onComplete?.Invoke ();
                return;
            }

            // Encode on main thread.
            Texture2D texture2D = new (width, height, TextureFormat.RGB24, false)
                { hideFlags = HideFlags.HideAndDontSave };
            texture2D.LoadRawTextureData (req.GetData <byte> ());
            texture2D.Apply (false);
            byte [] pngBytes = texture2D.EncodeToPNG ();
            Object.DestroyImmediate (texture2D);

            // Write on background thread.
            WriteAsync (pngBytes, filePath, onComplete);
        });
    }

    // ─── Shared async file writer ────────────────────────────────────────────

    /// <summary>
    /// Writes <paramref name="bytes"/> to disk on a thread-pool thread,
    /// then marshals the log plus callback back to the main thread.
    /// </summary>
    private static void WriteAsync (byte [] bytes, string filePath, Action onComplete)
    {
        Task.Run (() => File.WriteAllBytes (filePath, bytes))
            .ContinueWith (t =>
            {
                // ContinueWith with the editor's synchronization context
                // → runs on the main thread.
                if (t.IsFaulted)
                    Debug.LogWarning (
                        $"[ScreenshotTaker] Write failed: {t.Exception?.InnerException?.Message}");
                else
                    Debug.Log ($"<color=cyan> [ScreenshotTaker] Saved → {filePath} </color>");

                onComplete?.Invoke ();
            }, TaskScheduler.FromCurrentSynchronizationContext ());
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    internal static Camera FindMainCamera ()
    {
        Camera cam = Camera.main;
        return cam ? cam : Object.FindAnyObjectByType <Camera> ();
#if UNITY_2023_1_OR_NEWER
#else
#pragma warning disable CS0618
        return Object.FindObjectOfType<Camera> ();
#pragma warning restore CS0618
#endif
    }

    private static void EnsureDirectory (string path)
    {
        if (!string.IsNullOrEmpty (path) && !Directory.Exists (path))
            Directory.CreateDirectory (path);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  ThumbnailCache  — async file load + async GPU readback
// ═══════════════════════════════════════════════════════════════════════════

internal sealed class ThumbnailCache
{
    private readonly Dictionary <string, Texture2D> _cache = new ();
    private readonly Queue <string> _queue = new ();
    private readonly HashSet <string> _pending = new ();

    // Tracks whether an async load is already in flight.
    private bool _loadingThumb;

    public int ThumbSize { get; set; } = 96;

    public Texture2D Get (string path)
    {
        if (_cache.TryGetValue (path, out Texture2D texture2D) && texture2D) return texture2D;
        if (_pending.Add (path)) _queue.Enqueue (path);
        return null;
    }

    /// <summary>
    /// Kicks off ONE async thumbnail load per call.
    /// Call this from OnGUI / Repaint — it returns immediately.
    /// Returns true when work was queued (so the caller can request a Repaint).
    /// </summary>
    public bool ProcessOne ()
    {
        // Only one load in flight at a time to avoid hammering the disk.
        if (_loadingThumb) return false;

        while (_queue.Count > 0)
        {
            string path = _queue.Dequeue ();
            _pending.Remove (path);

            if (_cache.ContainsKey (path) || !File.Exists (path)) continue;

            _loadingThumb = true;
            int size = Mathf.Max (ThumbSize, 64);

            // --- Step 1: read raw bytes on a background thread (no main-thread stall) ---
            Task.Run (() => File.ReadAllBytes (path))
                .ContinueWith (fileTask =>
                {
                    // --- Step 2: back on the main thread for Unity texture API ---
                    // (ContinueWith with the editor sync context)
                    if (fileTask.IsFaulted)
                    {
                        _loadingThumb = false;
                        return;
                    }

                    Texture2D full = new (2, 2, TextureFormat.RGB24, false)
                        { hideFlags = HideFlags.HideAndDontSave };
                    full.LoadImage (fileTask.Result); // compressed → decompressed in RAM

                    // Blit to thumb-sized RT.
                    RenderTexture renderTexture =
                        RenderTexture.GetTemporary (size, size, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit (full, renderTexture);
                    Object.DestroyImmediate (full);

                    // --- Step 3: async GPU readback — no stall ---
                    AsyncGPUReadback.Request (renderTexture, 0, TextureFormat.RGB24, req =>
                    {
                        RenderTexture.ReleaseTemporary (renderTexture);

                        if (!req.hasError)
                        {
                            Texture2D thumb = new (size, size, TextureFormat.RGB24, false)
                                { hideFlags = HideFlags.HideAndDontSave };
                            thumb.LoadRawTextureData (req.GetData <byte> ());
                            thumb.Apply (false);
                            _cache [path] = thumb;
                        }

                        _loadingThumb = false;
                        // The window's ProcessOne loop will pick up the next item on the
                        // next Repaint, keeping the UI responsive.
                    });
                }, TaskScheduler.FromCurrentSynchronizationContext ());

            return true; // work was queued; caller should Repaint
        }

        return false;
    }

    public void Remove (string path)
    {
        if (_cache.TryGetValue (path, out Texture2D texture2D) && texture2D) Object.DestroyImmediate (texture2D);
        _cache.Remove (path);
        _pending.Remove (path);
    }

    public void Flush ()
    {
        foreach (KeyValuePair <string, Texture2D> cacheEntry in _cache.Where (cacheEntry => cacheEntry.Value))
            Object.DestroyImmediate (cacheEntry.Value);
        _cache.Clear ();
        _queue.Clear ();
        _pending.Clear ();
        _loadingThumb = false;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  GalleryCache  — unchanged, no heavy work happens here
// ═══════════════════════════════════════════════════════════════════════════

internal sealed class GalleryCache
{
    public struct Item
    {
        public string path;
        public string fileName;
        public string displayName;
        public DateTime writeTime;
        public string relativeTime;
        public string sizeLabel;
    }

    public Item [] Items { get; private set; } = Array.Empty <Item> ();
    public string Summary { get; private set; } = "";

    private double _lastRefresh = -99;
    private const double TTL = 2.0;
    private int _thumbSize;

    public void Invalidate () => _lastRefresh = -99;

    public void Refresh (string folder, int thumbSize)
    {
        double now = EditorApplication.timeSinceStartup;
        bool thumbChanged = thumbSize != _thumbSize;
        _thumbSize = thumbSize;

        if (!thumbChanged && now - _lastRefresh < TTL) return;
        _lastRefresh = now;

        if (string.IsNullOrEmpty (folder) || !Directory.Exists (folder))
        {
            Items = Array.Empty <Item> ();
            Summary = "No folder selected.";
            return;
        }

        string [] files = Directory.GetFiles (folder, "*.png");
        if (files.Length == 0)
        {
            Items = Array.Empty <Item> ();
            Summary = "No screenshots yet.";
            return;
        }

        Item [] items = new Item [files.Length];
        long totalBytes = 0;

        for (int i = 0; i < files.Length; i++)
        {
            FileInfo info = new (files [i]);
            totalBytes += info.Length;
            items [i] = new Item
            {
                path = files [i],
                fileName = info.Name,
                displayName = TruncateName (info.Name, thumbSize),
                writeTime = info.LastWriteTime,
                relativeTime = RelativeTime (info.LastWriteTime),
                sizeLabel = FormatBytes (info.Length),
            };
        }

        Array.Sort (items, (a, b) => b.writeTime.CompareTo (a.writeTime));
        Items = items;
        Summary =
            $"{items.Length} image{(items.Length != 1 ? "s" : "")}  ·  {FormatBytes (totalBytes)}  (newest first)";
    }

    private static string TruncateName (string name, int thumbPx)
    {
        int limit = Mathf.Max (6, thumbPx / 6);
        if (name.Length <= limit) return name;
        string ext = Path.GetExtension (name);
        string body = Path.GetFileNameWithoutExtension (name);
        int keep = Mathf.Max (1, limit - ext.Length - 1);
        return body [..keep] + "…" + ext;
    }

    private static string RelativeTime (DateTime dt)
    {
        TimeSpan elapsed = DateTime.Now - dt;
        if (elapsed.TotalSeconds < 60) return "just now";
        if (elapsed.TotalMinutes < 60) return $"{(int) elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours < 24) return $"{(int) elapsed.TotalHours} hr ago";
        return elapsed.TotalDays switch
        {
            < 2 => "yesterday",
            < 7 => $"{(int) elapsed.TotalDays} days ago",
            _ => dt.ToString ("dd MMM yyyy")
        };
    }

    private static string FormatBytes (long bytes)
    {
        return bytes switch
        {
            < 1_024 => $"{bytes} B",
            < 1_048_576 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / 1_048_576.0:F1} MB"
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  ScreenShotTakerInspector  — unchanged
// ═══════════════════════════════════════════════════════════════════════════

[CustomEditor (typeof (ScreenShotTaker))]
public sealed class ScreenShotTakerInspector : Editor
{
    private SerializedProperty _propScreenshotAction;

    private void OnEnable () => _propScreenshotAction = serializedObject.FindProperty ("_screenshotAction");

    private ScreenShotTaker T => (ScreenShotTaker) target;

    private void Change (string label, Action mutate)
    {
        Undo.RecordObject (target, label);
        mutate ();
        EditorUtility.SetDirty (target);
    }

    public override void OnInspectorGUI ()
    {
        EditorGUILayout.BeginVertical ("HelpBox");
        EditorGUILayout.BeginHorizontal ();
        GUILayout.Label (
            EditorGUIUtility.IconContent ("d_SceneViewCamera"),
            GUILayout.Width (32), GUILayout.Height (32)
        );
        EditorGUILayout.BeginVertical ();
        GUILayout.Space (4);
        GUILayout.Label ("ScreenShotTaker", EditorStyles.boldLabel);
        GUILayout.Label ("Runtime hotkey capture component", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical ();
        EditorGUILayout.EndHorizontal ();
        EditorGUILayout.EndVertical ();
        EditorGUILayout.Space (6);

        GUILayout.Label ("Key Binding", EditorStyles.boldLabel);
        EditorGUILayout.LabelField (string.Empty, GUI.skin.horizontalSlider);

        EditorGUI.BeginChangeCheck ();
        ScreenShotTaker.KeyBindingMode newMode = (ScreenShotTaker.KeyBindingMode) EditorGUILayout.EnumPopup (
            new GUIContent (
                "Binding Mode",
                "KeyboardKey — single keyboard key.\n" +
                "InputAction — full Input System action (gamepad, mouse, combos)."
            ),
            T.BindingMode
        );
        if (EditorGUI.EndChangeCheck ())
            Change ("Change Binding Mode", () => T.BindingMode = newMode);

        EditorGUILayout.Space (2);

        if (T.BindingMode == ScreenShotTaker.KeyBindingMode.KeyboardKey)
        {
            EditorGUI.BeginChangeCheck ();
            Key newKey = (Key) EditorGUILayout.EnumPopup (
                new GUIContent ("Key", "Keyboard key that triggers a screenshot."), T.Key
            );
            if (EditorGUI.EndChangeCheck ())
                Change ("Change Screenshot Key", () => T.Key = newKey);

            EditorGUILayout.HelpBox ($"Press [{T.Key}] during Play Mode to capture.", MessageType.Info);
        }
        else
        {
            serializedObject.Update ();
            EditorGUILayout.PropertyField (
                _propScreenshotAction,
                new GUIContent (
                    "Screenshot Action",
                    "Button-type InputAction. Enabled/disabled with this component."
                ),
                includeChildren: true
            );
            serializedObject.ApplyModifiedProperties ();
            EditorGUILayout.HelpBox (
                "Action type must be 'Button'. Enabled/disabled with this component.", MessageType.Info
            );
        }

        EditorGUILayout.Space (6);
        GUILayout.Label ("Output", EditorStyles.boldLabel);
        EditorGUILayout.LabelField (string.Empty, GUI.skin.horizontalSlider);

        EditorGUILayout.BeginHorizontal ();
        EditorGUI.BeginChangeCheck ();
        string newPath = EditorGUILayout.TextField (
            new GUIContent ("Save Path", "Folder where screenshots are saved."), T.OutputPath
        );
        if (EditorGUI.EndChangeCheck ())
            Change ("Change Save Path", () => T.OutputPath = newPath);

        if (GUILayout.Button ("Browse", GUILayout.Width (70)))
        {
            string chosen = EditorUtility.OpenFolderPanel ("Select Output Folder", T.OutputPath, "");
            if (!string.IsNullOrEmpty (chosen)) Change ("Change Save Path", () => T.OutputPath = chosen);
        }

        if (GUILayout.Button ("▶", GUILayout.Width (24)) && Directory.Exists (T.OutputPath))
            EditorUtility.RevealInFinder (T.OutputPath);
        EditorGUILayout.EndHorizontal ();

        EditorGUILayout.Space (6);
        EditorGUILayout.LabelField (string.Empty, GUI.skin.horizontalSlider);
        if (GUILayout.Button ("Open Screenshot Taker Window", GUILayout.Height (28)))
            ScreenShotTakerWindow.OpenWindow ();

        GUILayout.Label (
            "Tools  ▶  Screenshot Taker  or  Ctrl+Alt+Shift+S",
            EditorStyles.centeredGreyMiniLabel
        );
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  ScreenShotTakerWindow  — unchanged except calls to renamed capture methods
// ═══════════════════════════════════════════════════════════════════════════

public sealed class ScreenShotTakerWindow : EditorWindow
{
    [MenuItem ("Tools/Screenshot Taker %&#s")]
    public static void OpenWindow ()
    {
        ScreenShotTakerWindow win = GetWindow <ScreenShotTakerWindow> ("Screenshot Taker");
        win.titleContent = new GUIContent (
            "Screenshot Taker",
            EditorGUIUtility.IconContent ("d_SceneViewCamera").image
        );
        win.minSize = new Vector2 (320, 520);
        win.Show ();
    }

    private const string PREF_PATH = "SST_OutputPath";
    private const string PREF_TEMPLATE = "SST_FileTemplate";
    private const string PREF_PRESET = "SST_PresetIdx";
    private const string PREF_MODE = "SST_CaptureMode";
    private const string PREF_CUSTOM_W = "SST_CustomW";
    private const string PREF_CUSTOM_H = "SST_CustomH";
    private const string PREF_FILE_NAME_OPEN = "SST_FileNameFoldout";
    private const string PREF_GALLERY_OPEN = "SST_GalleryFoldout";
    private const string PREF_KEY_MODE = "SST_KeyMode";
    private const string PREF_KEY = "SST_Key";
    private const string PREF_THUMB_SIZE = "SST_ThumbSize";

    private static readonly string DefaultPath =
        Environment.GetFolderPath (Environment.SpecialFolder.MyPictures);

    private const string DEFAULT_TEMPLATE = "Screenshot_{date}_{time}";
    private const int DEFAULT_PRESET = 2;
    private const int DEFAULT_MODE = 0;
    private const int DEFAULT_W = 1920;
    private const int DEFAULT_H = 1080;
    private const int DEFAULT_KEY_MODE = 0;
    private const int DEFAULT_KEY = (int) Key.M;
    private const int DEFAULT_THUMB_SZ = 96;
    private const int MIN_THUMB_SZ = 56;
    private const int MAX_THUMB_SZ = 144;
    private const int CARD_PAD = 4;
    private const int LABEL_H = 34;
    private const int GRID_GAP = 6;

    private static readonly string [] ToolbarLabels = { "Scene", "Game" };
    private static readonly string [] KeyModeLabels = { "Keyboard Key", "Input Action" };
    private static readonly string [] CaptureModeLabels = { "Scene View", "Game View" };

    private static readonly (string Label, int W, int H) [] Presets =
    {
        ("── Landscape ──", 0, 0),
        ("HD  1280×720", 1280, 720),
        ("FHD  1920×1080", 1920, 1080),
        ("QHD  2560×1440", 2560, 1440),
        ("4K  3840×2160", 3840, 2160),
        ("── Portrait ──", 0, 0),
        ("720×1280", 720, 1280),
        ("1080×1920", 1080, 1920),
        ("1440×2560", 1440, 2560),
        ("2160×3840", 2160, 3840),
        ("── Square ──", 0, 0),
        ("1080×1080", 1080, 1080),
        ("2048×2048", 2048, 2048),
        ("Custom…", 0, 0),
    };

    private static readonly string [] PresetLabels =
        Array.ConvertAll (Presets, p => p.Label);

    private enum CaptureMode
    {
        SceneView,
        GameView
    }

    private string _outputPath = "";
    private string _fileTemplate = DEFAULT_TEMPLATE;
    private CaptureMode _captureMode = CaptureMode.SceneView;
    private int _presetIdx = DEFAULT_PRESET;
    private int _gameViewW = DEFAULT_W;
    private int _gameViewH = DEFAULT_H;
    private bool _fileNameFoldout;
    private bool _galleryFoldout = true;
    private bool _resetPending;

    private ScreenShotTaker.KeyBindingMode _keyMode = ScreenShotTaker.KeyBindingMode.KeyboardKey;
    private Key _hotKey = Key.M;
    private bool _hotKeyWasDown;
    private KeyControl _hotKeyControl;

    private string _hotkeyHint;
    private Key _hotkeyHintKey = Key.None;

    private static string [] _keyNames;
    private static Key [] _keyValues;
    private static Dictionary <Key, int> _keyIndexMap;

    private readonly ThumbnailCache _thumbCache = new ();
    private readonly GalleryCache _gallery = new ();

    private Camera _cachedCamera;
    private double _cameraCacheTime = -99;
    private const double CAM_TTL = 1.0;

    private ScreenShotTaker _cachedComponent;
    private bool _componentValid;

    private Vector2 _mainScroll;
    private Vector2 _galleryScroll;

    private GUIStyle _gUIStyleCard, _gUIStyleHeader, _gUIStyleDim, _gUIStylePreview;
    private GUIStyle _gUIStyleBoldButton, _gUIStyleDangerButton;
    private GUIStyle _gUIStyleGalleryCard, _gUIStyleGalleryName, _gUIStyleGalleryDate, _gUIStyleOverlayButton;
    private bool _stylesReady;

    private Texture2D _overlayNormalTexture2D, _overlayHoverTexture2D, _overlayActiveTexture2D;

    private static readonly Color ColorPlaceholder = new (0.15f, 0.15f, 0.15f);
    private static readonly Color ColorOverlay = new (0f, 0f, 0f, 0.58f);

    private void OnEnable ()
    {
        wantsMouseMove = true;
        LoadPrefs ();
        SubscribeEvents ();
        RebuildHotKeyControl ();
    }

    private void OnDisable ()
    {
        SaveAllPrefs ();
        UnsubscribeEvents ();
        _thumbCache.Flush ();
        FlushStyleTextures ();
    }

    private void SubscribeEvents ()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        InputSystem.onEvent += OnInputEvent;
    }

    private void UnsubscribeEvents ()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
        AssemblyReloadEvents.afterAssemblyReload -= OnAfterReload;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        InputSystem.onEvent -= OnInputEvent;
    }

    private void OnBeforeReload ()
    {
        _thumbCache.Flush ();
        FlushStyleTextures ();
        _stylesReady = false;
    }

    private void OnAfterReload ()
    {
        _stylesReady = false;
        Repaint ();
    }

    private void OnPlayModeChanged (PlayModeStateChange state)
    {
        if (state is not (PlayModeStateChange.EnteredEditMode or PlayModeStateChange.EnteredPlayMode))
            return;

        _thumbCache.Flush ();
        _gallery.Invalidate ();
        InvalidateCameraCache ();
        InvalidateComponentCache ();
        RebuildHotKeyControl ();
        FlushStyleTextures ();
        _stylesReady = false;
        Repaint ();
    }

    private void OnInputEvent (InputEventPtr evPtr, InputDevice device)
    {
        if (_keyMode != ScreenShotTaker.KeyBindingMode.KeyboardKey) return;
        if (_hotKeyControl == null) return;
        if (!evPtr.IsA <StateEvent> () && !evPtr.IsA <DeltaStateEvent> ()) return;
        if (device != _hotKeyControl.device) return;

        bool isDown = _hotKeyControl.isPressed;
        if (isDown && !_hotKeyWasDown)
            EditorApplication.delayCall += TriggerCapture;
        _hotKeyWasDown = isDown;
    }

    private void RebuildHotKeyControl ()
    {
        _hotKeyControl = Keyboard.current? [_hotKey];
        _hotKeyWasDown = false;
    }

    private void TriggerCapture ()
    {
        if (_captureMode == CaptureMode.GameView) CaptureGame ();
        else CaptureScene ();
    }

    private void OnGUI ()
    {
        EnsureStyles ();
        DrawToolbar ();

        _mainScroll = EditorGUILayout.BeginScrollView (_mainScroll);
        EditorGUILayout.Space (4);

        DrawSettingsCard ();
        EditorGUILayout.Space (2);
        DrawKeyBindingCard ();
        EditorGUILayout.Space (2);
        DrawFileNameCard ();
        EditorGUILayout.Space (2);
        DrawCaptureModeCard ();
        EditorGUILayout.Space (2);
        DrawCaptureButtonRow ();
        EditorGUILayout.Space (2);
        DrawGallerySection ();
        EditorGUILayout.Space (2);
        DrawResetCard ();
        EditorGUILayout.Space (8);

        EditorGUILayout.EndScrollView ();

        // ProcessOne is now async — it returns quickly; request a repaint
        // whenever a thumbnail just finished loading.
        if (_thumbCache.ProcessOne ()) Repaint ();
    }

    private void LoadPrefs ()
    {
        _outputPath = EditorPrefs.GetString (PREF_PATH, DefaultPath);
        _fileTemplate = EditorPrefs.GetString (PREF_TEMPLATE, DEFAULT_TEMPLATE);
        _presetIdx = EditorPrefs.GetInt (PREF_PRESET, DEFAULT_PRESET);
        _captureMode = (CaptureMode) EditorPrefs.GetInt (PREF_MODE, DEFAULT_MODE);
        _gameViewW = EditorPrefs.GetInt (PREF_CUSTOM_W, DEFAULT_W);
        _gameViewH = EditorPrefs.GetInt (PREF_CUSTOM_H, DEFAULT_H);
        _fileNameFoldout = EditorPrefs.GetBool (PREF_FILE_NAME_OPEN, false);
        _galleryFoldout = EditorPrefs.GetBool (PREF_GALLERY_OPEN, true);
        _keyMode = (ScreenShotTaker.KeyBindingMode) EditorPrefs.GetInt (PREF_KEY_MODE, DEFAULT_KEY_MODE);
        _hotKey = (Key) EditorPrefs.GetInt (PREF_KEY, DEFAULT_KEY);
        _thumbCache.ThumbSize = EditorPrefs.GetInt (PREF_THUMB_SIZE, DEFAULT_THUMB_SZ);
        ApplyPreset (_presetIdx);
    }

    private void SaveAllPrefs ()
    {
        EditorPrefs.SetString (PREF_PATH, _outputPath);
        EditorPrefs.SetString (PREF_TEMPLATE, _fileTemplate);
        EditorPrefs.SetInt (PREF_PRESET, _presetIdx);
        EditorPrefs.SetInt (PREF_MODE, (int) _captureMode);
        EditorPrefs.SetInt (PREF_CUSTOM_W, _gameViewW);
        EditorPrefs.SetInt (PREF_CUSTOM_H, _gameViewH);
        EditorPrefs.SetBool (PREF_FILE_NAME_OPEN, _fileNameFoldout);
        EditorPrefs.SetBool (PREF_GALLERY_OPEN, _galleryFoldout);
        EditorPrefs.SetInt (PREF_KEY_MODE, (int) _keyMode);
        EditorPrefs.SetInt (PREF_KEY, (int) _hotKey);
        EditorPrefs.SetInt (PREF_THUMB_SIZE, _thumbCache.ThumbSize);
    }

    private void ResetToDefaults ()
    {
        _outputPath = DefaultPath;
        _fileTemplate = DEFAULT_TEMPLATE;
        _presetIdx = DEFAULT_PRESET;
        _captureMode = DEFAULT_MODE;
        _gameViewW = DEFAULT_W;
        _gameViewH = DEFAULT_H;
        _fileNameFoldout = false;
        _galleryFoldout = true;
        _resetPending = false;
        _keyMode = DEFAULT_KEY_MODE;
        _hotKey = (Key) DEFAULT_KEY;
        _thumbCache.ThumbSize = DEFAULT_THUMB_SZ;
        _thumbCache.Flush ();
        _gallery.Invalidate ();
        InvalidateCameraCache ();
        FlushStyleTextures ();
        _stylesReady = false;
        SaveAllPrefs ();
        Repaint ();
        Debug.Log ("<color=cyan> [ScreenshotTaker] Settings reset to defaults. </color>");
    }

    private void ApplyPreset (int presetIndex)
    {
        if ((uint) presetIndex >= (uint) Presets.Length) return;
        (string _, int presetWidth, int presetHeight) = Presets [presetIndex];
        if (presetWidth <= 0 || presetHeight <= 0) return;
        _gameViewW = presetWidth;
        _gameViewH = presetHeight;
    }

    private void InvalidateCameraCache () => _cameraCacheTime = -99;

    private Camera GetMainCamera ()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - _cameraCacheTime < CAM_TTL) return _cachedCamera;
        _cameraCacheTime = now;
        _cachedCamera = ScreenshotCapture.FindMainCamera ();
        return _cachedCamera;
    }

    private void InvalidateComponentCache ()
    {
        _componentValid = false;
        _cachedComponent = null;
    }

    private ScreenShotTaker GetCachedComponent ()
    {
        if (_componentValid) return _cachedComponent;
        _componentValid = true;
#if UNITY_2023_1_OR_NEWER
        _cachedComponent = FindAnyObjectByType <ScreenShotTaker> ();
#else
#pragma warning disable CS0618
        _cachedComponent = FindObjectOfType<ScreenShotTaker> ();
#pragma warning restore CS0618
#endif
        return _cachedComponent;
    }

    private void SyncSettingsToComponent ()
    {
        ScreenShotTaker comp = GetCachedComponent ();
        if (!comp) return;
        Undo.RecordObject (comp, "Sync Hotkey Settings");
        comp.BindingMode = _keyMode;
        comp.Key = _hotKey;
        EditorUtility.SetDirty (comp);
    }

    private void DrawToolbar ()
    {
        EditorGUILayout.BeginHorizontal (EditorStyles.toolbar);
        GUILayout.Label ("Screenshot Taker", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace ();
        GUILayout.Label ("Mode:", EditorStyles.toolbarButton);

        EditorGUI.BeginChangeCheck ();
        int newMode = GUILayout.Toolbar (
            (int) _captureMode, ToolbarLabels,
            EditorStyles.toolbarButton, GUILayout.Width (110)
        );
        if (EditorGUI.EndChangeCheck ())
        {
            _captureMode = (CaptureMode) newMode;
            InvalidateCameraCache ();
            SaveAllPrefs ();
        }

        EditorGUILayout.EndHorizontal ();
    }

    private void DrawSettingsCard ()
    {
        BeginCard ("Output Settings");
        GUILayout.Label ("Output Path", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal ();
        EditorGUI.BeginChangeCheck ();
        _outputPath = EditorGUILayout.TextField (_outputPath);
        if (EditorGUI.EndChangeCheck ())
        {
            _thumbCache.Flush ();
            _gallery.Invalidate ();
            SaveAllPrefs ();
        }

        if (GUILayout.Button ("Browse", GUILayout.Width (70)))
        {
            string chosen = EditorUtility.OpenFolderPanel ("Select Output Folder", _outputPath, "");
            if (!string.IsNullOrEmpty (chosen))
            {
                _outputPath = chosen;
                _thumbCache.Flush ();
                _gallery.Invalidate ();
                SaveAllPrefs ();
            }
        }

        if (GUILayout.Button ("▶", GUILayout.Width (24)) && Directory.Exists (_outputPath))
            EditorUtility.RevealInFinder (_outputPath);

        EditorGUILayout.EndHorizontal ();
        EditorGUILayout.Space (2);
        EditorGUILayout.LabelField (ShortenPath (_outputPath, 60), _gUIStyleDim);
        EndCard ();
    }

    private void DrawKeyBindingCard ()
    {
        if (_keyNames == null) BuildKeyLookup ();

        BeginCard ("Hotkey (Edit + Play Mode)");

        EditorGUI.BeginChangeCheck ();
        int newMode = GUILayout.Toolbar ((int) _keyMode, KeyModeLabels);
        if (EditorGUI.EndChangeCheck ())
        {
            _keyMode = (ScreenShotTaker.KeyBindingMode) newMode;
            SaveAllPrefs ();
        }

        EditorGUILayout.Space (4);

        if (_keyMode == ScreenShotTaker.KeyBindingMode.KeyboardKey)
        {
            _keyIndexMap.TryGetValue (_hotKey, out int curIdx);

            EditorGUILayout.BeginHorizontal ();
            GUILayout.Label ("Key", GUILayout.Width (60));
            EditorGUI.BeginChangeCheck ();
            int picked = EditorGUILayout.Popup (curIdx, _keyNames);
            if (EditorGUI.EndChangeCheck ())
            {
                _hotKey = _keyValues [picked];
                RebuildHotKeyControl ();
                _hotkeyHint = null;
                SaveAllPrefs ();
            }

            EditorGUILayout.EndHorizontal ();

            if (_hotkeyHint == null || _hotkeyHintKey != _hotKey)
            {
                _hotkeyHintKey = _hotKey;
                _hotkeyHint = $"Press  [{_hotKey}]  to capture (Edit + Play Mode).";
            }

            EditorGUILayout.HelpBox (_hotkeyHint, MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox (
                "InputAction bindings are configured on the ScreenShotTaker component.",
                MessageType.Info
            );
        }

        ScreenShotTaker comp = GetCachedComponent ();
        EditorGUILayout.Space (2);
        EditorGUILayout.BeginHorizontal ();
        GUILayout.Label (
            comp ? $"Component: {comp.gameObject.name}" : "No ScreenShotTaker in scene.", _gUIStyleDim
        );
        GUILayout.FlexibleSpace ();
        if (comp && GUILayout.Button ("Sync →", EditorStyles.miniButton, GUILayout.Width (52)))
            SyncSettingsToComponent ();
        EditorGUILayout.EndHorizontal ();

        EndCard ();
    }

    private static void BuildKeyLookup ()
    {
        string [] names = Enum.GetNames (typeof (Key));
        Key [] values = (Key []) Enum.GetValues (typeof (Key));

        List <string> nl = new (values.Length);
        List <Key> vl = new (values.Length);

        for (int i = 0; i < values.Length; i++)
            if (values [i] != Key.None)
            {
                nl.Add (names [i]);
                vl.Add (values [i]);
            }

        _keyNames = nl.ToArray ();
        _keyValues = vl.ToArray ();
        _keyIndexMap = new Dictionary <Key, int> (_keyValues.Length);
        for (int i = 0; i < _keyValues.Length; i++)
            _keyIndexMap [_keyValues [i]] = i;
    }

    private void DrawFileNameCard ()
    {
        BeginCard ("File Naming");

        EditorGUILayout.BeginHorizontal ();
        EditorGUI.BeginChangeCheck ();
        _fileNameFoldout = EditorGUILayout.Foldout (_fileNameFoldout, "Template Settings", true);
        if (EditorGUI.EndChangeCheck ()) SaveAllPrefs ();
        GUILayout.FlexibleSpace ();
        GUILayout.Label ($"{BuildPreviewName ()}.png", _gUIStylePreview);
        EditorGUILayout.EndHorizontal ();

        if (_fileNameFoldout)
        {
            EditorGUILayout.Space (4);
            EditorGUI.indentLevel++;

            GUILayout.Label ("Template", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal ();
            EditorGUI.BeginChangeCheck ();
            _fileTemplate = EditorGUILayout.TextField (_fileTemplate);
            if (EditorGUI.EndChangeCheck ()) SaveAllPrefs ();
            if (GUILayout.Button ("Reset", GUILayout.Width (46)))
            {
                _fileTemplate = DEFAULT_TEMPLATE;
                SaveAllPrefs ();
            }

            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space (4);
            GUILayout.Label ("Insert Token:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal ();
            DrawTokenButton ("{date}", "Date (dd-MM-yyyy)");
            DrawTokenButton ("{time}", "Time (HH-mm-ss)");
            DrawTokenButton ("{mode}", "Capture mode");
            DrawTokenButton ("{res}", "Resolution WxH");
            GUILayout.FlexibleSpace ();
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space (4);
            EditorGUILayout.LabelField ("Preview:", $"{BuildPreviewName ()}.png");
            EditorGUILayout.LabelField ("Invalid characters are stripped automatically.", _gUIStyleDim);
            EditorGUI.indentLevel--;
        }

        EndCard ();
    }

    private void DrawCaptureModeCard ()
    {
        BeginCard ("Capture Mode");

        EditorGUI.BeginChangeCheck ();
        int newMode = GUILayout.Toolbar ((int) _captureMode, CaptureModeLabels);
        if (EditorGUI.EndChangeCheck ())
        {
            _captureMode = (CaptureMode) newMode;
            InvalidateCameraCache ();
            SaveAllPrefs ();
        }

        EditorGUILayout.Space (6);

        if (_captureMode == CaptureMode.SceneView)
        {
            EditorGUILayout.HelpBox (
                "Reads the already-composited Scene View frame — includes Gizmos and overlays. Play Mode not required.",
                MessageType.Info
            );
        }
        else
        {
            EditorGUILayout.HelpBox (
                "Off-screen render through the Main Camera. Play Mode not required.",
                MessageType.Info
            );
            EditorGUILayout.Space (4);

            EditorGUILayout.BeginHorizontal ();
            GUILayout.Label ("Resolution", GUILayout.Width (80));
            EditorGUI.BeginChangeCheck ();
            int rawIdx = EditorGUILayout.Popup (_presetIdx, PresetLabels);
            if (EditorGUI.EndChangeCheck ())
            {
                bool isSep = Presets [rawIdx].W == 0 && Presets [rawIdx].H == 0 && rawIdx != Presets.Length - 1;
                if (!isSep)
                {
                    _presetIdx = rawIdx;
                    ApplyPreset (_presetIdx);
                    SaveAllPrefs ();
                }
            }

            EditorGUILayout.EndHorizontal ();

            if (_presetIdx == Presets.Length - 1)
            {
                EditorGUI.BeginChangeCheck ();
                EditorGUILayout.BeginHorizontal ();
                GUILayout.Space (84);
                GUILayout.Label ("W", GUILayout.Width (14));
                _gameViewW = Mathf.Max (1, EditorGUILayout.IntField (_gameViewW, GUILayout.Width (64)));
                GUILayout.Space (6);
                GUILayout.Label ("H", GUILayout.Width (14));
                _gameViewH = Mathf.Max (1, EditorGUILayout.IntField (_gameViewH, GUILayout.Width (64)));
                GUILayout.Label ("px");
                EditorGUILayout.EndHorizontal ();
                if (EditorGUI.EndChangeCheck ()) SaveAllPrefs ();
            }
            else
            {
                EditorGUILayout.LabelField ($"  {_gameViewW} × {_gameViewH} px", _gUIStyleDim);
            }

            EditorGUILayout.Space (4);
            Camera cam = GetMainCamera ();
            if (cam) EditorGUILayout.LabelField ($"Camera: {cam.name}", _gUIStyleDim);
            else
                EditorGUILayout.HelpBox ("No Main Camera found. Tag a camera as 'MainCamera'.",
                    MessageType.Warning);
        }

        EndCard ();
    }

    private void DrawCaptureButtonRow ()
    {
        BeginCard (null);

        bool isGame = _captureMode == CaptureMode.GameView;
        string label = isGame ? "Capture Game View" : "Capture Scene View";

        EditorGUILayout.BeginHorizontal ();

        if (GUILayout.Button (label, _gUIStyleBoldButton, GUILayout.Height (34)))
        {
            if (isGame) CaptureGame ();
            else CaptureScene ();
        }

        if (GUILayout.Button ("Open Folder", GUILayout.Height (34), GUILayout.Width (96))
            && Directory.Exists (_outputPath))
            EditorUtility.RevealInFinder (_outputPath);

        EditorGUILayout.EndHorizontal ();
        EditorGUILayout.Space (2);
        EditorGUILayout.LabelField (
            "Play-Mode hotkey configured on the ScreenShotTaker component.", _gUIStyleDim
        );

        EndCard ();
    }

    private void CaptureScene () =>
        ScreenshotCapture.CaptureSceneView (_outputPath, OnCaptureComplete);

    private void CaptureGame ()
    {
        Camera cam = GetMainCamera ();
        if (!cam)
        {
            Debug.LogWarning ("[ScreenshotTaker] No camera found.");
            return;
        }

        string prefix = ResolveFileName ("Game", _gameViewW, _gameViewH);
        ScreenshotCapture.CaptureGameCamera (cam, _gameViewW, _gameViewH, _outputPath, prefix, OnCaptureComplete);
    }

    private void OnCaptureComplete ()
    {
        _thumbCache.Flush ();
        _gallery.Invalidate ();
        Repaint ();
        EditorApplication.delayCall += Repaint;
    }

    private void DrawGallerySection ()
    {
        _gallery.Refresh (_outputPath, _thumbCache.ThumbSize);
        GalleryCache.Item [] items = _gallery.Items;

        BeginCard (null);

        EditorGUILayout.BeginHorizontal ();
        EditorGUI.BeginChangeCheck ();
        _galleryFoldout = EditorGUILayout.Foldout (
            _galleryFoldout, "Gallery", true, EditorStyles.foldoutHeader
        );
        if (EditorGUI.EndChangeCheck ()) SaveAllPrefs ();

        GUILayout.FlexibleSpace ();

        if (items.Length > 0 && GUILayout.Button (
                "Refresh", EditorStyles.miniButton, GUILayout.Width (54), GUILayout.Height (18)))
        {
            _thumbCache.Flush ();
            _gallery.Invalidate ();
            Repaint ();
        }

        EditorGUILayout.EndHorizontal ();

        if (!_galleryFoldout)
        {
            if (items.Length > 0) EditorGUILayout.LabelField (_gallery.Summary, _gUIStyleDim);
            EndCard ();
            return;
        }

        EditorGUILayout.Space (3);
        EditorGUILayout.BeginHorizontal ();
        GUILayout.Label (_gallery.Summary, _gUIStyleDim);
        GUILayout.FlexibleSpace ();
        GUILayout.Label ("Size", _gUIStyleDim, GUILayout.Width (28));
        EditorGUI.BeginChangeCheck ();
        int newThumbSz = (int) GUILayout.HorizontalSlider (
            _thumbCache.ThumbSize, MIN_THUMB_SZ, MAX_THUMB_SZ, GUILayout.Width (72)
        );
        if (EditorGUI.EndChangeCheck ())
        {
            _thumbCache.ThumbSize = newThumbSz;
            _thumbCache.Flush ();
            SaveAllPrefs ();
            Repaint ();
        }

        EditorGUILayout.EndHorizontal ();
        EditorGUILayout.Space (4);

        if (items.Length == 0)
        {
            Rect emptyRect = GUILayoutUtility.GetRect (0, 56, GUILayout.ExpandWidth (true));
            EditorGUI.DrawRect (emptyRect, new Color (0f, 0f, 0f, 0.07f));
            GUI.Label (emptyRect, "No screenshots yet — capture one above!",
                new GUIStyle (EditorStyles.centeredGreyMiniLabel));
            EndCard ();
            return;
        }

        int sz = _thumbCache.ThumbSize;
        int itemW = sz + CARD_PAD * 2 + GRID_GAP;
        int cols = Mathf.Max (1, Mathf.FloorToInt ((position.width - 28f) / itemW));
        int rows = Mathf.CeilToInt ((float) items.Length / cols);
        int itemH = sz + LABEL_H + CARD_PAD * 2;
        float gridH = Mathf.Min (rows * (itemH + GRID_GAP) + 8f, 360f);

        _galleryScroll = EditorGUILayout.BeginScrollView (_galleryScroll, GUILayout.Height (gridH));

        int itemIndex = 0;
        for (int rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            EditorGUILayout.BeginHorizontal ();
            for (int columnIndex = 0; columnIndex < cols && itemIndex < items.Length; columnIndex++, itemIndex++)
            {
                DrawGalleryItem (itemIndex);
                if (columnIndex < cols - 1) GUILayout.Space (GRID_GAP);
            }

            GUILayout.FlexibleSpace ();
            EditorGUILayout.EndHorizontal ();
            GUILayout.Space (GRID_GAP);
        }

        EditorGUILayout.EndScrollView ();

        EditorGUILayout.Space (2);
        EditorGUILayout.LabelField (
            $"Hover thumbnail to Reveal/Delete  ·  {ShortenPath (_outputPath, 40)}", _gUIStyleDim
        );

        EndCard ();
    }

    private void DrawGalleryItem (int itemIndex)
    {
        GalleryCache.Item item = _gallery.Items [itemIndex];
        int sz = _thumbCache.ThumbSize;

        Rect card = EditorGUILayout.BeginVertical (
            _gUIStyleGalleryCard,
            GUILayout.Width (sz + CARD_PAD * 2),
            GUILayout.Height (sz + LABEL_H + CARD_PAD * 2)
        );

        Rect thumbRect = GUILayoutUtility.GetRect (sz, sz, GUILayout.Width (sz), GUILayout.Height (sz));
        Texture2D texture2D = _thumbCache.Get (item.path);
        if (texture2D)
            GUI.DrawTexture (thumbRect, texture2D, ScaleMode.ScaleAndCrop);
        else
        {
            EditorGUI.DrawRect (thumbRect, ColorPlaceholder);
            GUI.Label (thumbRect, "⏳", new GUIStyle (GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } });
        }

        GUILayout.Label (item.displayName, _gUIStyleGalleryName, GUILayout.Width (sz));
        GUILayout.Label (item.relativeTime, _gUIStyleGalleryDate, GUILayout.Width (sz));

        EditorGUILayout.EndVertical ();

        if (!(card.width > 0) || !card.Contains (Event.current.mousePosition)) return;

        Rect overlay = new (card.x + CARD_PAD, card.y + CARD_PAD, sz, sz);
        GUI.color = ColorOverlay;
        GUI.DrawTexture (overlay, EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;

        const float btnH = 26f;
        float btnW = (sz - 6f) * 0.5f;
        float btnY = overlay.y + (overlay.height - btnH) * 0.5f;

        GUIContent iconReveal = EditorGUIUtility.IconContent ("d_FolderOpened Icon");
        GUIContent iconDelete = EditorGUIUtility.IconContent ("d_TreeEditor.Trash");

        if (GUI.Button (new Rect (overlay.x + 1, btnY, btnW, btnH),
                new GUIContent (iconReveal.image, "Reveal in Explorer"), _gUIStyleOverlayButton))
            EditorUtility.RevealInFinder (item.path);

        if (GUI.Button (new Rect (overlay.x + btnW + 5, btnY, btnW, btnH),
                new GUIContent (iconDelete.image, "Delete screenshot"), _gUIStyleOverlayButton))
        {
            if (EditorUtility.DisplayDialog ("Delete Screenshot",
                    $"Permanently delete:\n{item.fileName}?", "Delete", "Cancel"))
            {
                _thumbCache.Remove (item.path);
                try
                {
                    File.Delete (item.path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning ($"[ScreenshotTaker] Delete failed: {e.Message}");
                }

                _gallery.Invalidate ();
                Repaint ();
            }
        }

        Rect tipRect = new (overlay.x, overlay.yMax - 18, overlay.width, 18);
        GUI.color = new Color (0f, 0f, 0f, 0.7f);
        GUI.DrawTexture (tipRect, EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;
        GUI.Label (tipRect, $" {item.sizeLabel}",
            new GUIStyle (EditorStyles.miniLabel)
                { normal = { textColor = new Color (0.9f, 0.9f, 0.9f) } });

        if (Event.current.type == EventType.MouseMove) Repaint ();
    }

    private void DrawResetCard ()
    {
        BeginCard ("Preferences");

        if (!_resetPending)
        {
            EditorGUILayout.BeginHorizontal ();
            GUILayout.Label ("Settings saved to EditorPrefs.", _gUIStyleDim);
            GUILayout.FlexibleSpace ();
            if (GUILayout.Button ("Reset to Defaults", _gUIStyleDangerButton,
                    GUILayout.Width (120), GUILayout.Height (22)))
                _resetPending = true;
        }
        else
        {
            EditorGUILayout.HelpBox (
                "Reset ALL settings to defaults? This cannot be undone.", MessageType.Warning
            );
            EditorGUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Cancel", GUILayout.Height (28))) _resetPending = false;
            if (GUILayout.Button ("Yes, Reset Everything", _gUIStyleDangerButton, GUILayout.Height (28)))
                ResetToDefaults ();
        }

        EditorGUILayout.EndHorizontal ();
        EndCard ();
    }

    private string ResolveFileName (string mode, int w, int h)
    {
        DateTime now = DateTime.Now;
        return SanitizeFileName (
            _fileTemplate
                .Replace ("{date}", now.ToString ("dd-MM-yyyy"))
                .Replace ("{time}", now.ToString ("HH-mm-ss"))
                .Replace ("{mode}", mode)
                .Replace ("{res}", $"{w}x{h}")
        );
    }

    private string BuildPreviewName () =>
        ResolveFileName (_captureMode == CaptureMode.GameView ? "Game" : "Scene", _gameViewW, _gameViewH);

    private static string SanitizeFileName (string name)
    {
        name = Path.GetInvalidFileNameChars ()
            .Aggregate (name, (current, c) => current.Replace (c.ToString (), ""));
        name = Regex.Replace (name, @"_{2,}", "_").Trim ('_', ' ');
        return string.IsNullOrEmpty (name) ? "Screenshot" : name;
    }

    private void BeginCard (string header)
    {
        EditorGUILayout.BeginVertical (_gUIStyleCard);
        if (string.IsNullOrEmpty (header)) return;
        GUILayout.Label (header, _gUIStyleHeader);
        EditorGUILayout.Space (2);
    }

    private static void EndCard ()
    {
        EditorGUILayout.Space (2);
        EditorGUILayout.EndVertical ();
    }

    private void DrawTokenButton (string token, string tooltip)
    {
        if (!GUILayout.Button (new GUIContent (token, tooltip), EditorStyles.miniButton)) return;
        _fileTemplate += token;
        SaveAllPrefs ();
        GUI.FocusControl (null);
    }

    private void EnsureStyles ()
    {
        if (_stylesReady) return;

        _gUIStyleCard = new GUIStyle ("HelpBox")
            { padding = new RectOffset (10, 10, 8, 8), margin = new RectOffset (4, 4, 2, 2) };
        _gUIStyleHeader = new GUIStyle (EditorStyles.boldLabel) { fontSize = 11 };
        _gUIStyleDim = new GUIStyle (EditorStyles.miniLabel) { wordWrap = true };
        _gUIStylePreview = new GUIStyle (EditorStyles.miniLabel)
            { fontStyle = FontStyle.Italic, normal = { textColor = new Color (0.35f, 0.75f, 0.55f) } };
        _gUIStyleBoldButton = new GUIStyle (GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
        _gUIStyleDangerButton = new GUIStyle (GUI.skin.button)
        {
            fontSize = 11, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color (0.85f, 0.25f, 0.25f) },
            hover = { textColor = new Color (1.00f, 0.40f, 0.40f) },
            focused = { textColor = new Color (1.00f, 0.40f, 0.40f) },
        };
        _gUIStyleGalleryCard = new GUIStyle ("HelpBox")
        {
            padding = new RectOffset (CARD_PAD, CARD_PAD, CARD_PAD, CARD_PAD), margin = new RectOffset (0, 0, 0, 0)
        };
        _gUIStyleGalleryName = new GUIStyle (EditorStyles.miniLabel)
            { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.UpperCenter };
        _gUIStyleGalleryDate = new GUIStyle (EditorStyles.miniLabel)
        {
            wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.UpperCenter,
            normal = { textColor = new Color (0.5f, 0.5f, 0.5f) }
        };

        FlushStyleTextures ();
        _overlayNormalTexture2D = MakeSolidTexture2D (new Color (0.2f, 0.2f, 0.2f, 0.85f));
        _overlayHoverTexture2D = MakeSolidTexture2D (new Color (0.4f, 0.4f, 0.4f, 0.95f));
        _overlayActiveTexture2D = MakeSolidTexture2D (new Color (0.1f, 0.1f, 0.1f, 0.95f));

        _gUIStyleOverlayButton = new GUIStyle (GUI.skin.button)
        {
            padding = new RectOffset (2, 2, 2, 2),
            normal = { background = _overlayNormalTexture2D },
            hover = { background = _overlayHoverTexture2D },
            active = { background = _overlayActiveTexture2D },
        };

        _stylesReady = true;
    }

    private void FlushStyleTextures ()
    {
        if (_overlayNormalTexture2D) DestroyImmediate (_overlayNormalTexture2D);
        if (_overlayHoverTexture2D) DestroyImmediate (_overlayHoverTexture2D);
        if (_overlayActiveTexture2D) DestroyImmediate (_overlayActiveTexture2D);
        _overlayNormalTexture2D = _overlayHoverTexture2D = _overlayActiveTexture2D = null;
    }

    private static Texture2D MakeSolidTexture2D (Color color)
    {
        Texture2D texture2D = new (1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture2D.SetPixel (0, 0, color);
        texture2D.Apply ();
        return texture2D;
    }

    private static string ShortenPath (string path, int max) =>
        string.IsNullOrEmpty (path) ? "── (no folder)" : (path.Length <= max ? path : "…" + path [^max..]);
}

#endif