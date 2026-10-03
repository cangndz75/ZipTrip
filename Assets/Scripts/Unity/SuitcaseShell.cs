using System.Collections.Generic;
using UnityEngine;

namespace ZipTrip.Unity
{
    // ZT-040B presentation-only suitcase around the playable board: quilted lining, padded terracotta walls with a raised
    // rim, zipper and pull, a front carry handle, the lid standing open behind the hinge and a soft contact shadow on
    // the packing surface. Built from simple procedural meshes in board-local space. It never reads gameplay state and
    // has no colliders; touch still projects onto the y = 0 board plane exactly as before.
    public sealed class SuitcaseShell : MonoBehaviour
    {
        public const float Wall = 0.62f;
        public const float BodyRadius = 0.85f;
        public const float InnerRadius = 0.22f;
        public const float LiningY = -0.012f;
        public const float RimY = 0.30f;
        public const float Cushion = 0.07f;
        /// <summary>Height of the packing surface the suitcase and the loose source items rest on.</summary>
        public const float SurfaceY = -0.03f;
        /// <summary>How far the open lid leans back past vertical.</summary>
        public const float LidLeanDegrees = 4f;

        private readonly List<Object> _owned = new List<Object>();
        private Transform _root;

        /// <summary>Playable interior in contour space (x, z), board-local.</summary>
        public Rect Interior { get; private set; }
        /// <summary>Outer body rectangle in contour space (x, z), board-local.</summary>
        public Rect Body { get; private set; }
        public Transform Lid { get; private set; }

        /// <param name="interior">Union of the compartment rectangles in contour space (x, z).</param>
        /// <param name="fillers">Interior cells that are not playable (masked cells, gaps between compartments).</param>
        public void Build(Rect interior, IEnumerable<Rect> fillers, Material template)
        {
            Clear();
            template = PresentationKit.TemplateOrFallback(template);
            if (template == null)
                return;
            Interior = interior;
            Body = new Rect(interior.xMin - Wall, interior.yMin - Wall, interior.width + 2f * Wall, interior.height + 2f * Wall);
            _root = new GameObject("Suitcase Visual").transform;
            _root.SetParent(transform, false);

            var shell = Own(PresentationKit.Matte(template, PresentationKit.SuitcaseShell, null, 0.28f));
            var shellDark = Own(PresentationKit.Matte(template, PresentationKit.Shade(PresentationKit.SuitcaseShell, 0.78f), null, 0.2f));
            var quilt = Own(PresentationKit.QuiltTexture(256, 3, 4021));
            var lining = Own(PresentationKit.Matte(template, PresentationKit.SuitcaseLining, quilt, 0.06f));
            var piping = Own(PresentationKit.Matte(template, PresentationKit.LiningPiping, null, 0.1f));
            var tape = Own(PresentationKit.Matte(template, PresentationKit.ZipperTape, null, 0.2f));
            var metal = Own(PresentationKit.Matte(template, PresentationKit.Mustard, null, 0.55f));
            var strap = Own(PresentationKit.Matte(template, PresentationKit.Shade(PresentationKit.Mustard, 0.9f), null, 0.15f));

            var inner = PresentationKit.RoundedRect(interior, InnerRadius, 6);
            var outer = PresentationKit.OuterContour(inner, Body, BodyRadius);
            var zipperPath = PresentationKit.Blend(inner, outer, 0.45f);
            var rimTop = RimY + Cushion;

            Add("Contact Shadow", PresentationKit.Quad(Grow(Body, 0.38f, 0f, -0.2f), SurfaceY + 0.004f), ShadowMaterial(template));
            Add("Lining", PresentationKit.FlatFan(inner, LiningY), lining);
            Add("Lining Piping", PresentationKit.Ribbon(PresentationKit.RoundedRect(Shrink(interior, 0.09f), InnerRadius * 0.7f, 6),
                0.035f, LiningY + 0.003f, true), piping);
            Add("Padded Walls", PresentationKit.BackpackShell(inner, outer, LiningY, RimY, Cushion, SurfaceY), shell);
            Add("Zipper Tape", PresentationKit.Ribbon(zipperPath, 0.14f, rimTop + 0.004f, true), tape);
            Add("Zipper Teeth", PresentationKit.Ribbon(zipperPath, 0.045f, rimTop + 0.008f, true), metal);

            foreach (var filler in fillers)
                Add("Padded Filler", PresentationKit.Slab(Shrink(filler, 0.04f), 0.12f, LiningY, RimY * 0.8f), shellDark);

            // Zipper pull on the front edge, right third, hanging towards the viewer.
            var pullX = interior.xMin + interior.width * 0.72f;
            var pullZ = interior.yMin - Wall * 0.45f;
            Add("Zipper Slider", PresentationKit.Slab(new Rect(pullX - 0.13f, pullZ - 0.09f, 0.26f, 0.18f), 0.05f, rimTop, rimTop + 0.06f), metal);
            Add("Zipper Pull", PresentationKit.Slab(new Rect(pullX - 0.07f, pullZ - 0.4f, 0.14f, 0.32f), 0.06f, rimTop + 0.02f, rimTop + 0.05f), metal);

            // Carry handle on the front wall, centred so the suitcase stays symmetric for framing.
            var cx = interior.center.x;
            var front = Body.yMin;
            Add("Handle Grip", PresentationKit.Slab(new Rect(cx - 0.8f, front - 0.3f, 1.6f, 0.24f), 0.12f, 0.05f, 0.21f), shellDark);
            Add("Handle Mount L", PresentationKit.Slab(new Rect(cx - 0.86f, front - 0.16f, 0.24f, 0.2f), 0.06f, SurfaceY, 0.2f), shellDark);
            Add("Handle Mount R", PresentationKit.Slab(new Rect(cx + 0.62f, front - 0.16f, 0.24f, 0.2f), 0.06f, SurfaceY, 0.2f), shellDark);

            BuildLid(shell, shellDark, lining, tape, metal, strap);
        }

        // The lid is modelled lying flat behind the hinge with its lining facing up, then swung up about the hinge
        // until it leans just past vertical, so the camera sees its quilted inside like an open suitcase.
        private void BuildLid(Material shell, Material shellDark, Material lining, Material tape, Material metal, Material strap)
        {
            var depth = Body.height;
            Lid = new GameObject("Open Lid").transform;
            Lid.SetParent(_root, false);
            Lid.localPosition = new Vector3(0f, RimY * 0.55f, Body.yMax);
            Lid.localRotation = Quaternion.Euler(-(90f - LidLeanDegrees), 0f, 0f);

            var lidBody = new Rect(Body.xMin, 0f, Body.width, depth);
            var lidInner = new Rect(Interior.xMin, Wall, Interior.width, depth - 2f * Wall);
            var innerContour = PresentationKit.RoundedRect(lidInner, InnerRadius, 6);
            var outerContour = PresentationKit.OuterContour(innerContour, lidBody, BodyRadius);
            Add("Lid Shell", PresentationKit.Slab(lidBody, BodyRadius, -0.24f, -0.045f), shell, Lid);
            Add("Lid Rim", PresentationKit.BackpackShell(innerContour, outerContour, -0.03f, 0.04f, 0.05f, -0.06f), shell, Lid);
            Add("Lid Lining", PresentationKit.FlatFan(innerContour, -0.025f), lining, Lid);
            Add("Lid Zipper Tape", PresentationKit.Ribbon(PresentationKit.Blend(innerContour, outerContour, 0.45f), 0.14f, 0.095f, true), tape, Lid);

            // Crossed elastic straps with a buckle, the classic inside-of-the-lid detail.
            var a = Shrink(lidInner, 0.45f);
            Add("Lid Strap A", PresentationKit.Ribbon(new[] { new Vector2(a.xMin, a.yMin), new Vector2(a.xMax, a.yMax) }, 0.2f, -0.015f, false), strap, Lid);
            Add("Lid Strap B", PresentationKit.Ribbon(new[] { new Vector2(a.xMax, a.yMin), new Vector2(a.xMin, a.yMax) }, 0.2f, -0.012f, false), strap, Lid);
            var c = a.center;
            Add("Lid Buckle", PresentationKit.Slab(new Rect(c.x - 0.22f, c.y - 0.17f, 0.44f, 0.34f), 0.08f, -0.012f, 0.03f), metal, Lid);
            Add("Hinge", PresentationKit.Slab(new Rect(Interior.xMin + 0.4f, -0.12f, Interior.width - 0.8f, 0.2f), 0.08f, -0.2f, 0.02f), shellDark, Lid);
        }

        public void Clear()
        {
            if (_root != null)
            {
                // Same-frame bounds (camera framing) must not see the outgoing suitcase; Destroy is deferred.
                _root.gameObject.SetActive(false);
                Destroy(_root.gameObject);
                _root = null;
            }
            Lid = null;
            foreach (var asset in _owned)
                if (asset != null)
                    Destroy(asset);
            _owned.Clear();
        }

        private Material ShadowMaterial(Material template)
        {
            var texture = Own(PresentationKit.SoftRect(64, 0.3f));
            return Own(PresentationKit.Transparent(template, PresentationKit.WithAlpha(PresentationKit.Shadow, 0.32f), texture));
        }

        private void Add(string name, Mesh mesh, Material material, Transform parent = null)
        {
            Own(mesh);
            PresentationKit.MeshObject(name, parent != null ? parent : _root, mesh, material);
        }

        private T Own<T>(T asset) where T : Object
        {
            _owned.Add(asset);
            return asset;
        }

        private static Rect Shrink(Rect rect, float amount) =>
            new Rect(rect.xMin + amount, rect.yMin + amount, rect.width - 2f * amount, rect.height - 2f * amount);

        // Grows by `amount` and shifts by (dx, dz): the soft shadow falls away from the upper-left key light.
        private static Rect Grow(Rect rect, float amount, float dx, float dz) =>
            new Rect(rect.xMin - amount + dx, rect.yMin - amount + dz, rect.width + 2f * amount, rect.height + 2f * amount);

        private void OnDestroy() => Clear();
    }
}
