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
        public const float MatThickness = 0.05f;
        /// <summary>Felt margin around the loose items' row (the mat hugs the row; no off-screen bleed).</summary>
        public const float MatMargin = 0.42f;
        public static readonly Color TableLinen = PresentationKit.Hex(0xEADCC7);
        public static readonly Color TableShade = PresentationKit.Hex(0x4A3526);
        // ZT-040D.1: warm taupe felt, a mid value between the light table and the dark teal lining, so the mat
        // separates from the suitcase and the colourful loose items pop on it.
        public static readonly Color Felt = PresentationKit.Hex(0xB5A48D);
        public static readonly Color Stitch = PresentationKit.Hex(0xF7F1E6);
        public static readonly Color KeyColor = new Color(1f, 0.95f, 0.86f);
        public const float KeyIntensity = 1.35f;
        public static readonly Vector3 KeyEuler = new Vector3(52f, 135f, 0f); // from the top-left of the screen

        private readonly List<Object> _owned = new List<Object>();
        private Material _template;
        private Transform _matRoot;

        public GameObject Surface { get; private set; }
        public GameObject Vignette { get; private set; }
        public GameObject Mat { get; private set; }
        public Light KeyLight { get; private set; }
        public Volume PostVolume { get; private set; }

        public void Build(Material template, Camera camera)
        {
            if (Surface != null)
                return;
            _template = PresentationKit.TemplateOrFallback(template);
            if (_template == null)
                return;
            var linen = Own(PresentationKit.LinenTexture(128, 1709));
            var table = Own(PresentationKit.Matte(_template, TableLinen, linen, 0.04f));
            table.SetTextureScale(PresentationKit.BaseMapId, new Vector2(SurfaceSize / 2.4f, SurfaceSize / 2.4f));
            var half = SurfaceSize * 0.5f;
            Surface = PresentationKit.MeshObject("Packing Table", transform, Own(PresentationKit.Quad(new Rect(-half, -half, SurfaceSize, SurfaceSize), 0f)), table);
            Surface.GetComponent<MeshRenderer>().receiveShadows = true;

            var falloff = Own(PresentationKit.TableVignette(128, new Vector2(0.32f, 0.7f), 0.55f));
            Vignette = PresentationKit.MeshObject("Table Light Falloff", transform,
                Own(PresentationKit.Quad(new Rect(-13f, -22f, 26f, 44f), 0.004f)),
                Own(PresentationKit.Transparent(_template, TableShade, falloff)));

            BuildLighting();
            BuildPost(camera);
        }

        /// <summary>Centres the table light on the suitcase and lays the felt mat under the loose items.</summary>
        /// <param name="center">Suitcase centre (x, z).</param>
        /// <param name="matArea">Loose items' area (x, z), in table space; the mat grows by MatMargin and bleeds down.</param>
        public void Layout(Vector2 center, float surfaceY, Rect matArea)
        {
            if (Surface == null)
                return;
            Surface.transform.localPosition = new Vector3(center.x, surfaceY, center.y);
            Vignette.transform.localPosition = new Vector3(center.x, surfaceY, center.y - 3f);

            if (_matRoot != null)
            {
                _matRoot.gameObject.SetActive(false);
                Destroy(_matRoot.gameObject);
            }
            _matRoot = new GameObject("Packing Mat").transform;
            _matRoot.SetParent(transform, false);
            var rect = new Rect(matArea.xMin - MatMargin, matArea.yMin - MatMargin, matArea.width + 2f * MatMargin, matArea.height + 2f * MatMargin);
            var top = surfaceY + MatThickness;
            var feltTexture = Own(PresentationKit.FeltTexture(128, 3301));
            var felt = Own(PresentationKit.Matte(_template, Felt, feltTexture, 0.02f));
            felt.SetTextureScale(PresentationKit.BaseMapId, new Vector2(rect.width / 3f, rect.height / 3f));
            Mat = PresentationKit.MeshObject("Felt", _matRoot, Own(PresentationKit.Slab(rect, 0.6f, surfaceY, top)), felt);
            Mat.GetComponent<MeshRenderer>().receiveShadows = true;
            var stitch = Own(PresentationKit.Matte(_template, Stitch, null, 0.15f));
            var inset = new Rect(rect.xMin + 0.24f, rect.yMin + 0.24f, rect.width - 0.48f, rect.height - 0.48f);
            PresentationKit.MeshObject("Stitch", _matRoot,
                Own(PresentationKit.DashedPath(PresentationKit.RoundedRect(inset, 0.38f, 8), 0.17f, 0.11f, 0.045f, top + 0.003f, true)), stitch);
            var shadowTexture = Own(PresentationKit.SoftRect(64, 0.35f));
            PresentationKit.MeshObject("Mat Shadow", _matRoot, Own(PresentationKit.Quad(
                new Rect(rect.xMin - 0.2f, rect.yMin - 0.35f, rect.width + 0.5f, rect.height + 0.5f), surfaceY + 0.002f)),
                Own(PresentationKit.Transparent(_template, PresentationKit.WithAlpha(PresentationKit.Shadow, 0.3f), shadowTexture)));
        }

        public void SetMatVisible(bool visible)
        {
            if (_matRoot != null)
                _matRoot.gameObject.SetActive(visible);
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
            KeyLight.shadowStrength = 0.58f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.77f, 0.81f);
            RenderSettings.ambientEquatorColor = new Color(0.60f, 0.57f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.24f);
        }

        // Mobile-safe, restrained, all inside URP's single uber pass: neutral tonemapping, a little contrast/saturation and
        // a soft warm vignette. No bloom (extra passes, no visible gain at phone scale), no blur, no chromatic aberration.
        private void BuildPost(Camera camera)
        {
            if (camera == null)
                return;
            var profile = Own(ScriptableObject.CreateInstance<VolumeProfile>());
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0f);
            color.contrast.Override(16f);
            color.saturation.Override(8f);
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.2f);
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
            foreach (var asset in _owned)
                if (asset != null)
                    Destroy(asset);
        }
    }
}
