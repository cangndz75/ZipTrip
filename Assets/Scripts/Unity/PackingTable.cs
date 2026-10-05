using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZipTrip.Unity
{
    // ZT-040D "packing table" presentation (Presentation Direction: The Packing Table). A warm linen table with a baked
    // top-left key and an edge vignette that darkens only the table, a warm taupe felt packing mat hugging the loose
    // items' row (ZT-040D.1: separated from the dark suitcase, no dead felt), one warm directional key with a trilight ambient fill
    // (key:fill ~2.5:1), and a restrained post stack. Presentation only: no colliders, no gameplay reads.
    public sealed class PackingTable : MonoBehaviour
    {
        public const float SurfaceSize = 90f;
        /// <summary>Source Tray card thickness: loose items rest on the card tops (tray presenter at surface + this).</summary>
        public const float MatThickness = 0.05f;
        /// <summary>UI-SLICE-01 compact tray: paper card margin around each loose item, and shell margin around the cards.</summary>
        public const float CardPad = 0.16f;
        public const float ShellPad = 0.2f;
        public static readonly Color ShellLinen = PresentationKit.Hex(0xD9C7A3);
        public static readonly Color CardEdge = PresentationKit.Hex(0xE4D7BF);
        public static readonly Color TableLinen = PresentationKit.Hex(0xEADCC7);
        public static readonly Color TableShade = PresentationKit.Hex(0x4A3526);
        public static readonly Color KeyColor = new Color(1f, 0.95f, 0.86f);
        public const float KeyIntensity = 1.65f;
        public static readonly Vector3 KeyEuler = new Vector3(52f, 135f, 0f); // from the top-left of the screen

        // BACKDROP-SLICE-01 (STYLE-FRAME-01 travel world). The authored surface covers SurfaceRect around the suitcase
        // centre without tiling and fades to SurfaceTone, which the plain table beyond it uses. Edge props are one atlas
        // (contact shadows and rotation baked in), anchored to the screen edges / suitcase front after camera framing
        // so the crop adapts per aspect while the suitcase never moves. No colliders, no UI: never in the input path.
        public static readonly Rect SurfaceRect = new Rect(-6f, -14f, 12f, 24f);
        public static readonly Color SurfaceTone = PresentationKit.Hex(0xE9DFCD);
        public const float PropLift = 0.006f;
        private const float AtlasSize = 1024f;

        // Texels (top-left origin) and world sizes from Tools/Art/build_backdrop_slice_01.py; anchors measured on the
        // locked frame (1080x2340: 136.4 px per world unit): inward offset from the screen edge and the screen-space
        // offset from the suitcase front (+ = up). Every prop hangs off the suitcase front, so the cluster keeps its
        // spacing on any aspect and shorter screens simply crop the lower props more.
        private static readonly (string Name, RectInt Texels, Vector2 Size, bool Right, float Inward, float Offset)[] Props =
        {
            ("Boarding Pass", new RectInt(366, 438, 396, 244), new Vector2(2.468f, 1.521f), false, 0.675f, -0.857f),
            ("Postcard", new RectInt(0, 438, 362, 284), new Vector2(2.256f, 1.770f), true, 0.587f, -0.857f),
            ("Folded Map", new RectInt(438, 0, 392, 335), new Vector2(2.443f, 2.088f), false, 0.44f, -3.058f),
            ("Straw Hat", new RectInt(0, 0, 434, 434), new Vector2(3.756f, 3.756f), true, -0.147f, -4.651f),
            ("Olive Sprig", new RectInt(0, 726, 406, 216), new Vector2(2.530f, 1.346f), false, 0.18f, 1.27f),
        };

        private readonly List<Object> _owned = new List<Object>();
        private readonly List<Renderer> _props = new List<Renderer>();
        private Material _template;
        private float _surfaceY;
        private Transform _trayRoot;
        private readonly List<Mesh> _trayMeshes = new List<Mesh>();
        private Material _cardMaterial;
        private Material _edgeMaterial;
        private Material _selectedMaterial;
        private Material _shellMaterial;
        private Material _trayShadowMaterial;
        private Material _trayArtMaterial;
        private Material _trayCardArtMaterial;
        private int _trayKey;
        private bool _vacationBackdrop;

        public GameObject Surface { get; private set; }
        /// <summary>Authored travel-world surface (null when no backdrop texture is configured).</summary>
        public GameObject Backdrop { get; private set; }
        /// <summary>Edge props (one shared transparent material); empty when no prop atlas is configured.</summary>
        public IReadOnlyList<Renderer> PropRenderers => _props;
        public GameObject Vignette { get; private set; }
        /// <summary>Compact Source Tray shell (hidden when no loose item is left).</summary>
        public bool TrayShellVisible => _trayRoot != null && _trayRoot.gameObject.activeSelf;
        /// <summary>One paper card per loose item currently in the Source Tray.</summary>
        public int TrayCardCount { get; private set; }
        /// <summary>Table-space XZ rect of the tray shell (zero when hidden).</summary>
        public Rect TrayShellRect { get; private set; }
        public Light KeyLight { get; private set; }
        public Volume PostVolume { get; private set; }

        public void Build(Material template, Camera camera, Texture2D backdrop = null, Texture2D props = null)
        {
            if (Surface != null)
                return;
            _template = PresentationKit.TemplateOrFallback(template);
            if (_template == null)
                return;
            Material table;
            // ART-CC02: the table uses an offline softened, desaturated copy so the suitcase stays dominant.
            var vacation = Resources.Load<Texture2D>("UiSlice011/santorini_vacation_soft")
                ?? Resources.Load<Texture2D>("UiSlice011/santorini_vacation");
            _vacationBackdrop = vacation != null;
            if (_vacationBackdrop)
                backdrop = vacation;
            if (backdrop != null)
            {
                table = Own(PresentationKit.Matte(_template, SurfaceTone, null, 0.04f));
                Backdrop = PresentationKit.MeshObject("Backdrop Surface", transform, Own(PresentationKit.Quad(_vacationBackdrop ? new Rect(0f, 0f, 1f, 1f) : SurfaceRect, 0.001f)),
                    Own(PresentationKit.Matte(_template, Color.white, backdrop, 0.04f)));
            }
            else
            {
                var linen = Own(PresentationKit.LinenTexture(128, 1709));
                table = Own(PresentationKit.Matte(_template, TableLinen, linen, 0.04f));
                table.SetTextureScale(PresentationKit.BaseMapId, new Vector2(SurfaceSize / 2.4f, SurfaceSize / 2.4f));
            }
            var half = SurfaceSize * 0.5f;
            Surface = PresentationKit.MeshObject("Packing Table", transform, Own(PresentationKit.Quad(new Rect(-half, -half, SurfaceSize, SurfaceSize), 0f)), table);
            Surface.GetComponent<MeshRenderer>().receiveShadows = true;
            if (props != null)
                BuildProps(props);

            var falloff = Own(PresentationKit.TableVignette(128, new Vector2(0.32f, 0.7f), 0.55f));
            Vignette = PresentationKit.MeshObject("Table Light Falloff", transform,
                Own(PresentationKit.Quad(new Rect(-13f, -22f, 26f, 44f), 0.004f)),
                Own(PresentationKit.Transparent(_template, TableShade, falloff)));

            _cardMaterial = Own(PresentationKit.Matte(_template, PaperUi.Paper, null, 0.08f));
            _edgeMaterial = Own(PresentationKit.Matte(_template, CardEdge, null, 0.08f));
            _selectedMaterial = Own(PresentationKit.Matte(_template, PaperUi.Mustard, null, 0.1f));
            _shellMaterial = Own(PresentationKit.Matte(_template, ShellLinen, null, 0.06f));
            _trayShadowMaterial = Own(PresentationKit.Transparent(_template, PresentationKit.WithAlpha(PresentationKit.Shadow, 0.28f),
                Own(PresentationKit.SoftRect(64, 0.35f))));
            var trayArt = Resources.Load<Texture2D>("UiSlice011/mission_leather");
            if (trayArt != null)
                _trayArtMaterial = Own(PresentationKit.Transparent(_template, Color.white, trayArt));
            var cardArt = Resources.Load<Texture2D>("UiSlice011/tray_slot");
            if (cardArt != null)
                _trayCardArtMaterial = Own(PresentationKit.Transparent(_template, PaperUi.Cream, cardArt));
            _trayRoot = new GameObject("Source Tray Shell").transform;
            _trayRoot.SetParent(transform, false);
            _trayRoot.gameObject.SetActive(false);

            BuildLighting();
            BuildPost(camera);
        }

        /// <summary>Centres the table surface and its light on the suitcase.</summary>
        /// <param name="center">Suitcase centre (x, z).</param>
        public void Layout(Vector2 center, float surfaceY)
        {
            if (Surface == null)
                return;
            _surfaceY = surfaceY;
            Surface.transform.localPosition = new Vector3(center.x, surfaceY, center.y);
            if (Backdrop != null)
                Backdrop.transform.localPosition = new Vector3(center.x, surfaceY, center.y);
            Vignette.transform.localPosition = new Vector3(center.x, surfaceY, center.y - 3f);
            _trayKey = 0;
        }

        /// <summary>
        /// UI-SLICE-01 compact Source Tray: one paper card under each loose item on a linen shell that hugs the cards, so
        /// the tray contracts and re-centres with the presenter's row (3 -> 2 -> 1) and disappears when it is empty. Pure
        /// presentation following the presenter's views; rebuilt only when their layout or the selection changes.
        /// </summary>
        public void LayoutTray(PuzzleTrayPresenter tray, Camera camera)
        {
            if (_trayRoot == null || tray == null)
                return;
            var key = 17;
            foreach (var view in tray.ItemViews.Values)
            {
                var p = tray.CardPosition(view.InstanceId);
                key = key * 31 + Mathf.RoundToInt(p.x * 100f);
                key = key * 31 + Mathf.RoundToInt(p.z * 100f);
                key = key * 31 + (int)view.Rotation + view.Footprint.CellCount * 7;
            }
            key = key * 31 + (tray.SelectedInstanceId?.GetHashCode() ?? 0) + Mathf.RoundToInt(tray.Scale * 1000f) + tray.ItemViews.Count;
            if (camera != null)
            {
                var safe = PuzzleHud.SafeAreaOverride ?? PuzzleHud.NormalizeSafeArea(Screen.safeArea, Screen.width, Screen.height);
                key = key * 31 + camera.pixelWidth + Mathf.RoundToInt(safe.width * 1000f);
                key = key * 31 + Mathf.RoundToInt(camera.orthographicSize * 100f)
                    + Mathf.RoundToInt(camera.transform.position.x * 100f);
                key = key * 31 + Mathf.RoundToInt(safe.xMin * 1000f);
            }
            if (key == _trayKey)
                return;
            _trayKey = key;
            foreach (Transform child in _trayRoot)
                Destroy(child.gameObject);
            foreach (var mesh in _trayMeshes)
                Destroy(mesh);
            _trayMeshes.Clear();
            TrayCardCount = 0;
            TrayShellRect = default;
            _trayRoot.gameObject.SetActive(tray.ItemViews.Count > 0);
            if (tray.ItemViews.Count == 0)
                return;

            var y = _surfaceY;
            var shell = new Rect();
            var index = 0;
            foreach (var view in tray.ItemViews.Values)
            {
                int width = 0, depth = 0;
                var cells = view.Footprint.OccupiedCells;
                for (var i = 0; i < cells.Count; i++)
                {
                    width = Mathf.Max(width, cells[i].X + 1);
                    depth = Mathf.Max(depth, cells[i].Y + 1);
                }
                var p = tray.CardPosition(view.InstanceId);
                var cardWidth = Mathf.Max(2, width);
                var card = new Rect(p.x - (cardWidth - width) * tray.Scale * 0.5f - CardPad, p.z - depth * tray.Scale - CardPad, cardWidth * tray.Scale + 2f * CardPad,
                    depth * tray.Scale + 2f * CardPad);
                // Equal-depth authored cards, not empty gameplay slots. Only existing tray views create a card.
                var rowDepth = 0f;
                foreach (var rowView in tray.ItemViews.Values)
                    foreach (var cell in rowView.Footprint.OccupiedCells)
                        rowDepth = Mathf.Max(rowDepth, (cell.Y + 1) * tray.Scale);
                var cardTop = p.z;
                card.yMax = cardTop + CardPad;
                card.yMin = cardTop - rowDepth - CardPad - 0.32f;
                shell = index == 0 ? card : Rect.MinMaxRect(Mathf.Min(shell.xMin, card.xMin), Mathf.Min(shell.yMin, card.yMin),
                    Mathf.Max(shell.xMax, card.xMax), Mathf.Max(shell.yMax, card.yMax));
                // Neighbouring cards may overlap slightly (as in the locked frame): each sits a hair lower and the selected
                // one on top with a mustard rim, so no two faces share a depth.
                var selected = view.InstanceId == tray.SelectedInstanceId;
                var top = y + (selected ? MatThickness : MatThickness - 0.003f * (index + 1));
                var rim = new Rect(card.xMin - 0.03f, card.yMin - 0.03f, card.width + 0.06f, card.height + 0.06f);
                TrayMesh("Card Edge " + view.InstanceId, PresentationKit.Slab(rim, 0.2f, y + 0.026f, top - 0.002f),
                    selected ? _selectedMaterial : _edgeMaterial);
                TrayMesh("Card " + view.InstanceId, PresentationKit.Slab(card, 0.18f, y + 0.026f, top), _cardMaterial);
                if (_trayCardArtMaterial != null)
                    TrayMesh("Card Liner " + view.InstanceId, PresentationKit.Quad(card, top + 0.0005f), _trayCardArtMaterial);
                if (selected)
                {
                    var tab = new Rect(card.xMin + 0.12f, card.yMax - 0.07f, Mathf.Min(0.42f, card.width - 0.24f), 0.15f);
                    TrayMesh("Selected Tab " + view.InstanceId, PresentationKit.Slab(tab, 0.06f, top, top + 0.018f),
                        _selectedMaterial);
                }
                index++;
            }
            TrayCardCount = index;
            shell = new Rect(shell.xMin - ShellPad, shell.yMin - ShellPad, shell.width + 2f * ShellPad, shell.height + 2f * ShellPad + 0.64f);
            if (camera != null)
            {
                var safe = PuzzleHud.SafeAreaOverride ?? PuzzleHud.NormalizeSafeArea(Screen.safeArea, Screen.width, Screen.height);
                var left = GridProjector.ScreenToWorld(camera, new Vector2(camera.pixelWidth * safe.xMin, camera.pixelHeight * 0.5f), y).x + 0.18f;
                var right = GridProjector.ScreenToWorld(camera, new Vector2(camera.pixelWidth * safe.xMax, camera.pixelHeight * 0.5f), y).x - 0.18f;
                shell = Rect.MinMaxRect(Mathf.Max(shell.xMin, left), shell.yMin, Mathf.Min(shell.xMax, right), shell.yMax);
            }
            TrayShellRect = shell;
            TrayMesh("Shell", PresentationKit.Slab(shell, 0.34f, y, y + 0.025f), _shellMaterial);
            if (_trayArtMaterial != null)
                TrayMesh("Organizer Liner", PresentationKit.Quad(shell, y + 0.0255f), _trayArtMaterial);
            TrayMesh("Shell Shadow", PresentationKit.Quad(new Rect(shell.xMin - 0.12f, shell.yMin - 0.3f, shell.width + 0.4f,
                shell.height + 0.4f), y + 0.002f), _trayShadowMaterial);
        }

        private void TrayMesh(string name, Mesh mesh, Material material)
        {
            _trayMeshes.Add(mesh);
            PresentationKit.MeshObject(name, _trayRoot, mesh, material);
        }

        /// <summary>
        /// Places the edge props after camera framing: x from the screen edge, depth from the suitcase front. World size
        /// never changes (no stretching); only the crop adapts to the aspect.
        /// </summary>
        public void LayoutProps(Camera camera, Rect body)
        {
            if (camera != null && _vacationBackdrop && Backdrop != null)
            {
                // Infinite orthographic projection also covers unusually large test boards where a viewport-edge
                // ray begins beyond the table plane. This backdrop has no picking or gameplay role.
                var bottom = camera.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
                var top = camera.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
                var forward = camera.transform.forward;
                bottom += forward * ((_surfaceY - bottom.y) / forward.y);
                top += forward * ((_surfaceY - top.y) / forward.y);
                Backdrop.transform.position = new Vector3(bottom.x, _surfaceY, bottom.z);
                Backdrop.transform.localScale = new Vector3(top.x - bottom.x, 1f, top.z - bottom.z);
            }
            if (camera == null || _props.Count == 0)
                return;
            var y = _surfaceY + PropLift;
            // Screen height per unit of depth relative to screen width per unit (sin pitch, times any vertical squash).
            var origin = camera.WorldToScreenPoint(Vector3.zero);
            var frontToScreen = Mathf.Abs(camera.WorldToScreenPoint(Vector3.forward).y - origin.y)
                / Mathf.Abs(camera.WorldToScreenPoint(Vector3.right).x - origin.x);
            var left = GridProjector.ScreenToWorld(camera, new Vector2(0f, camera.pixelHeight * 0.5f), y).x;
            var right = GridProjector.ScreenToWorld(camera, new Vector2(camera.pixelWidth, camera.pixelHeight * 0.5f), y).x;
            for (var i = 0; i < Props.Length; i++)
            {
                var prop = Props[i];
                var x = prop.Right ? right - prop.Inward : left + prop.Inward;
                // The wider authored dock owns the lower centre. Keep loose travel props at the outer edges.
                if (prop.Name == "Postcard")
                    x += 0.7f;
                // ART-CC02: the edge-to-edge suitcase widens the dock below it; props beside the dock only peek in from
                // the screen edge, never over the loose-item row (which spans the body width).
                if (prop.Offset > -1f)
                    x = prop.Right ? Mathf.Max(x, body.xMax + prop.Size.x * 0.5f) : Mathf.Min(x, body.xMin - prop.Size.x * 0.5f);
                var z = body.yMin + prop.Offset / frontToScreen;
                _props[i].transform.localPosition = new Vector3(x, y, z);
            }
        }

        private void BuildProps(Texture2D atlas)
        {
            var material = Own(PresentationKit.Transparent(_template, Color.white, atlas));
            var root = new GameObject("Backdrop Props").transform;
            root.SetParent(transform, false);
            foreach (var prop in Props)
            {
                var t = prop.Texels;
                var uv0 = new Vector2(t.xMin / AtlasSize, 1f - t.yMax / AtlasSize);
                var uv1 = new Vector2(t.xMax / AtlasSize, 1f - t.yMin / AtlasSize);
                var hx = prop.Size.x * 0.5f;
                var hz = prop.Size.y * 0.5f;
                var mesh = Own(new Mesh { name = prop.Name });
                mesh.vertices = new[] { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz) };
                mesh.uv = new[] { uv0, new Vector2(uv1.x, uv0.y), uv1, new Vector2(uv0.x, uv1.y) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateBounds();
                var renderer = PresentationKit.MeshObject(prop.Name, root, mesh, material).GetComponent<MeshRenderer>();
                renderer.receiveShadows = false;
                _props.Add(renderer);
            }
        }

        // One warm key from the top-left (shadows fall to the bottom-right, matching the contact shadows) and a
        // trilight ambient that keeps shadow sides readable instead of a second real-time light.
        private void BuildLighting()
        {
            KeyLight = null;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                    KeyLight = light;
            if (KeyLight == null)
            {
                KeyLight = new GameObject("Key Light").AddComponent<Light>();
                KeyLight.type = LightType.Directional;
                KeyLight.transform.SetParent(transform, false);
            }
            KeyLight.transform.rotation = Quaternion.Euler(KeyEuler);
            KeyLight.color = KeyColor;
            KeyLight.intensity = KeyIntensity;
            KeyLight.shadows = LightShadows.Soft;
            KeyLight.shadowStrength = 0.68f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.77f, 0.81f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.64f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.32f, 0.28f);
        }

        // Mobile-safe, restrained: neutral tonemapping, warm white balance, contrast/saturation and a soft warm vignette in
        // URP's uber pass, plus one subtle quarter-resolution bloom (ART-CC02) that only catches highlights above 1.0 (brass
        // speculars), never the cream paper. The HUD is a screen-space overlay and is never post-processed. No blur, no
        // chromatic aberration.
        private void BuildPost(Camera camera)
        {
            if (camera == null)
                return;
            var profile = Own(ScriptableObject.CreateInstance<VolumeProfile>());
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.22f);
            color.contrast.Override(20f);
            color.saturation.Override(20f);
            var balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(9f);
            balance.tint.Override(2f);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.55f);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(4);
            bloom.highQualityFiltering.Override(false);
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.2f, 0.13f, 0.08f));
            PostVolume = new GameObject("Presentation Post").AddComponent<Volume>();
            PostVolume.transform.SetParent(transform, false);
            PostVolume.isGlobal = true;
            PostVolume.priority = 10f;
            PostVolume.sharedProfile = profile;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
        }

        private T Own<T>(T asset) where T : Object
        {
            _owned.Add(asset);
            return asset;
        }

        private void OnDestroy()
        {
            foreach (var mesh in _trayMeshes)
                if (mesh != null)
                    Destroy(mesh);
            foreach (var asset in _owned)
                if (asset != null)
                    Destroy(asset);
        }
    }
}
