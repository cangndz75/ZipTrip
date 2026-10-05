#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZipTrip.Unity
{
    /// <summary>
    /// ART-CC02 development-only composition check: the approved 9:16 target drawn over the game, fitted into the screen
    /// without stretching and centred (1080x2340 -> 1080x1920 with 210 px above and below). F8 toggles it. It creates
    /// itself, owns its own overlay canvas, ignores raycasts and is compiled out of production builds; gameplay never
    /// references it.
    /// </summary>
    public sealed class TargetCompositionOverlay : MonoBehaviour
    {
        public const string ReferencePath = "Assets/References/TargetComposition/approved-target-9x16.png";
        public const float DefaultOpacity = 0.5f;

        [SerializeField] private bool visible;
        [SerializeField, Range(0f, 1f)] private float opacity = DefaultOpacity;
        [SerializeField] private Texture2D reference;
        private Canvas _canvas;
        private RawImage _image;

        public static TargetCompositionOverlay Instance { get; private set; }
        public bool Visible { get => visible; set { visible = value; Apply(); } }
        public float Opacity { get => opacity; set { opacity = Mathf.Clamp01(value); Apply(); } }
        public Texture2D Reference { get => reference; set { reference = value; Apply(); } }
        public RectTransform Frame => _image.rectTransform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Instance != null)
                return;
            var host = new GameObject("Target Composition Overlay (dev)");
            DontDestroyOnLoad(host);
            host.AddComponent<TargetCompositionOverlay>();
        }

        private void Awake()
        {
            Instance = this;
#if UNITY_EDITOR
            if (reference == null)
                reference = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
#endif
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue;
            _image = new GameObject("Target", typeof(RectTransform)).AddComponent<RawImage>();
            _image.transform.SetParent(transform, false);
            _image.raycastTarget = false;
            // Native fit: largest 9:16 rect inside the screen, centred, never stretched.
            var fitter = _image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = PuzzleCameraFraming.ReferenceWidth / PuzzleCameraFraming.ReferenceHeight;
            Apply();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                Visible = !visible;
        }

        /// <summary>Capture only: draw through a camera so off-screen renders include the overlay (null = overlay).</summary>
        public Camera RenderThrough(Camera camera) => CaptureUi.Attach(_canvas, camera, 0.5f);

        private void Apply()
        {
            if (_image == null)
                return;
            _image.texture = reference;
            _image.color = new Color(1f, 1f, 1f, opacity);
            _image.enabled = visible && reference != null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
#endif
