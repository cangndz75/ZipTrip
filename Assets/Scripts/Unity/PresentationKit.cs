using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    // Procedural presentation geometry, textures and sprites. Never reads or writes gameplay state.
    public static class PresentationKit
    {
        public static readonly Color WarmOffWhite = Hex(0xE9E5DD);
        public static readonly Color DeepBlueGreen = Hex(0x31515D);
        public static readonly Color Teal = Hex(0x4E9FA2);
        public static readonly Color Terracotta = Hex(0xC36F58);
        public static readonly Color Mustard = Hex(0xD7AA58);
        public static readonly Color Paper = Hex(0xF7F4EE);
        public static readonly Color TrayShelf = Hex(0xDDD6CA);
        public static readonly Color TrayCard = Hex(0xF2EEE6);
        // ZT-040B suitcase scene: terracotta shell, deep blue-green quilted lining, warm linen packing surface.
        public static readonly Color SuitcaseShell = Hex(0xC36F58);
        public static readonly Color SuitcaseLining = Hex(0x355A66);
        public static readonly Color LiningPiping = Hex(0x5E8590);
        public static readonly Color ZipperTape = Hex(0x26414A);
        public static readonly Color Linen = Hex(0xEDE7DC);
        public static readonly Color Shadow = Hex(0x1F3138);

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Material Colored(Material template, Color color)
        {
            var material = new Material(template);
            material.SetColor(BaseColorId, color);
            material.SetColor(ColorId, color);
            return material;
        }

        /// <summary>Opaque matte material with an optional albedo texture (fabric, linen).</summary>
        public static Material Matte(Material template, Color color, Texture texture = null, float smoothness = 0.12f)
        {
            var material = Colored(template, color);
            material.SetTexture(BaseMapId, texture);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        /// <summary>Colour scaled in RGB only (alpha kept).</summary>
        public static Color Shade(Color color, float factor) =>
            new Color(color.r * factor, color.g * factor, color.b * factor, color.a);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static Material _fallbackLit;

        /// <summary>The given template, or a plain URP Lit material when a caller has none (tests, harness).</summary>
        public static Material TemplateOrFallback(Material template)
        {
            if (template != null)
                return template;
            if (_fallbackLit == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                    _fallbackLit = new Material(shader) { name = "Fallback Lit" };
            }
            return _fallbackLit;
        }

        public static Material Transparent(Material template, Color color, Texture texture = null)
        {
            var material = new Material(template.shader);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor(BaseColorId, color);
            material.SetColor(ColorId, color);
            if (texture != null)
                material.SetTexture(BaseMapId, texture);
            return material;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // Contours live in the XZ plane as (x, z).
        public static List<Vector2> RoundedRect(Rect rect, float radius, int segments = 8)
        {
            radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
            var points = new List<Vector2>();
            var centers = new[]
            {
                new Vector2(rect.xMax - radius, rect.yMax - radius),
                new Vector2(rect.xMin + radius, rect.yMax - radius),
                new Vector2(rect.xMin + radius, rect.yMin + radius),
                new Vector2(rect.xMax - radius, rect.yMin + radius)
            };
            for (var c = 0; c < 4; c++)
                for (var s = 0; s <= segments; s++)
                {
                    var angle = (c * 90f + s * 90f / segments) * Mathf.Deg2Rad;
                    points.Add(centers[c] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            return points;
        }

        // Boundary of the valid-cell union, counter-clockwise in (x, z); assumes one simply connected region.
        public static List<Vector2> MaskOutline(ContainerMask mask)
        {
            var next = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var cell in mask.GetValidCells())
            {
                // Cell (x, y) spans x..x+1 and z = -y..-(y+1); edges walk counter-clockwise seen from above.
                var a = new Vector2Int(cell.X, -cell.Y - 1);
                var b = new Vector2Int(cell.X + 1, -cell.Y - 1);
                var c = new Vector2Int(cell.X + 1, -cell.Y);
                var d = new Vector2Int(cell.X, -cell.Y);
                if (!mask.IsValid(new Cell(cell.X, cell.Y + 1))) next[a] = b;
                if (!mask.IsValid(new Cell(cell.X + 1, cell.Y))) next[b] = c;
                if (!mask.IsValid(new Cell(cell.X, cell.Y - 1))) next[c] = d;
                if (!mask.IsValid(new Cell(cell.X - 1, cell.Y))) next[d] = a;
            }
            var start = default(Vector2Int);
            foreach (var key in next.Keys)
            {
                start = key;
                break;
            }
            var raw = new List<Vector2Int> { start };
            for (var current = next[start]; current != start; current = next[current])
                raw.Add(current);
            var points = new List<Vector2>();
            for (var i = 0; i < raw.Count; i++)
            {
                var prev = raw[(i + raw.Count - 1) % raw.Count];
                var after = raw[(i + 1) % raw.Count];
                if ((raw[i] - prev) != (after - raw[i]))
                    points.Add(raw[i]);
            }
            return points;
        }

        public static List<Vector2> Fillet(IReadOnlyList<Vector2> polygon, float radius, int segments = 5)
        {
            var points = new List<Vector2>();
            for (var i = 0; i < polygon.Count; i++)
            {
                var prev = polygon[(i + polygon.Count - 1) % polygon.Count];
                var corner = polygon[i];
                var next = polygon[(i + 1) % polygon.Count];
                var r = Mathf.Min(radius, Vector2.Distance(prev, corner) * 0.5f,
                    Vector2.Distance(next, corner) * 0.5f);
                var from = corner + (prev - corner).normalized * r;
                var to = corner + (next - corner).normalized * r;
                for (var s = 0; s <= segments; s++)
                {
                    var t = s / (float)segments;
                    points.Add(Vector2.Lerp(Vector2.Lerp(from, corner, t), Vector2.Lerp(corner, to, t), t));
                }
            }
            return points;
        }

        public static Vector2 Centroid(IReadOnlyList<Vector2> contour)
        {
            var sum = Vector2.zero;
            for (var i = 0; i < contour.Count; i++)
                sum += contour[i];
            return sum / contour.Count;
        }

        public static Mesh FlatFan(IReadOnlyList<Vector2> contour, float y)
        {
            var builder = new MeshBuilder();
            var center = Centroid(contour);
            for (var i = 0; i < contour.Count; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Count];
                builder.Triangle(At(center, y), At(a, y), At(b, y), Vector3.up);
            }
            return builder.Build("Flat fan");
        }

        // For each inner point, the matching point on a rounded body seen from the inner centroid.
        public static List<Vector2> OuterContour(IReadOnlyList<Vector2> inner, Rect body, float radius)
        {
            var center = Centroid(inner);
            var outer = new List<Vector2>(inner.Count);
            for (var i = 0; i < inner.Count; i++)
                outer.Add(RayToRoundedRect(center, inner[i] - center, body, radius));
            return outer;
        }

        public static List<Vector2> Blend(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b, float t)
        {
            var result = new List<Vector2>(a.Count);
            for (var i = 0; i < a.Count; i++)
                result.Add(Vector2.Lerp(a[i], b[i], t));
            return result;
        }

        // Padded backpack shell: cushioned rim between the canonical cell outline and a rounded body.
        public static Mesh BackpackShell(IReadOnlyList<Vector2> inner, IReadOnlyList<Vector2> outer,
            float liningY, float rimY, float cushion, float baseY)
        {
            var center = Centroid(inner);
            var mid = Blend(inner, outer, 0.45f);
            var builder = new MeshBuilder();
            for (var i = 0; i < inner.Count; i++)
            {
                var j = (i + 1) % inner.Count;
                var inward = (center - (inner[i] + inner[j]) * 0.5f).normalized;
                builder.Quad(At(inner[i], liningY), At(inner[j], liningY), At(inner[j], rimY),
                    At(inner[i], rimY), new Vector3(inward.x, 0f, inward.y));
            }
            var cushionStart = builder.VertexCount;
            for (var i = 0; i < inner.Count; i++)
            {
                builder.Vertex(At(inner[i], rimY));
                builder.Vertex(At(mid[i], rimY + cushion));
                builder.Vertex(At(outer[i], rimY - cushion * 0.5f));
            }
            for (var i = 0; i < inner.Count; i++)
            {
                var a = cushionStart + i * 3;
                var b = cushionStart + (i + 1) % inner.Count * 3;
                builder.IndexedQuad(a, b, b + 1, a + 1, Vector3.up);
                builder.IndexedQuad(a + 1, b + 1, b + 2, a + 2, Vector3.up);
            }
            var wallStart = builder.VertexCount;
            for (var i = 0; i < inner.Count; i++)
            {
                builder.Vertex(At(outer[i], rimY - cushion * 0.5f));
                builder.Vertex(At(outer[i], baseY));
            }
            for (var i = 0; i < inner.Count; i++)
            {
                var a = wallStart + i * 2;
                var b = wallStart + (i + 1) % inner.Count * 2;
                var outward = ((outer[i] + outer[(i + 1) % inner.Count]) * 0.5f - center).normalized;
                builder.IndexedQuad(a, b, b + 1, a + 1, new Vector3(outward.x, 0f, outward.y));
            }
            return builder.Build("Backpack shell");
        }

        // Thin band following a closed contour (zipper trim, seams).
        /// <summary>Stitch-like dashes of <paramref name="dash"/> length every dash + gap along a path (ZT-040D mat stitch).</summary>
        public static Mesh DashedPath(IReadOnlyList<Vector2> path, float dash, float gap, float width, float y, bool closed)
        {
            var builder = new MeshBuilder();
            var count = closed ? path.Count : path.Count - 1;
            var phase = 0f; // distance into the current dash+gap period
            for (var i = 0; i < count; i++)
            {
                var a = path[i];
                var b = path[(i + 1) % path.Count];
                var length = (b - a).magnitude;
                if (length <= 1e-5f)
                    continue;
                var direction = (b - a) / length;
                var n = new Vector2(-direction.y, direction.x) * width * 0.5f;
                var walked = 0f;
                while (walked < length)
                {
                    var inDash = phase < dash;
                    var step = Mathf.Min(length - walked, (inDash ? dash : dash + gap) - phase);
                    if (inDash)
                    {
                        var p = a + direction * walked;
                        var q = a + direction * (walked + step);
                        builder.Quad(At(p - n, y), At(q - n, y), At(q + n, y), At(p + n, y), Vector3.up);
                    }
                    walked += step;
                    phase = (phase + step) % (dash + gap);
                }
            }
            return builder.Build("Dashed path");
        }

        public static Mesh Ribbon(IReadOnlyList<Vector2> path, float width, float y, bool closed)
        {
            var builder = new MeshBuilder();
            var count = closed ? path.Count : path.Count - 1;
            for (var i = 0; i < count; i++)
            {
                var a = path[i];
                var b = path[(i + 1) % path.Count];
                var n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
                builder.Quad(At(a - n, y), At(b - n, y), At(b + n, y), At(a + n, y), Vector3.up);
            }
            return builder.Build("Ribbon");
        }

        // Thin lines on edges shared by two valid cells: the stitched lining, not a debug grid.
        public static Mesh Seams(ContainerMask mask, float width, float y)
        {
            var builder = new MeshBuilder();
            var half = width * 0.5f;
            foreach (var cell in mask.GetValidCells())
            {
                var x = cell.X;
                var z = -cell.Y;
                if (mask.IsValid(new Cell(cell.X + 1, cell.Y)))
                    builder.Quad(new Vector3(x + 1 - half, y, z - 1 + 0.12f), new Vector3(x + 1 + half, y, z - 1 + 0.12f),
                        new Vector3(x + 1 + half, y, z - 0.12f), new Vector3(x + 1 - half, y, z - 0.12f), Vector3.up);
                if (mask.IsValid(new Cell(cell.X, cell.Y + 1)))
                    builder.Quad(new Vector3(x + 0.12f, y, z - 1 - half), new Vector3(x + 1 - 0.12f, y, z - 1 - half),
                        new Vector3(x + 1 - 0.12f, y, z - 1 + half), new Vector3(x + 0.12f, y, z - 1 + half), Vector3.up);
            }
            return builder.Build("Seams");
        }

        public static Mesh Quad(Rect rect, float y)
        {
            var builder = new MeshBuilder();
            builder.Quad(new Vector3(rect.xMin, y, rect.yMin), new Vector3(rect.xMax, y, rect.yMin),
                new Vector3(rect.xMax, y, rect.yMax), new Vector3(rect.xMin, y, rect.yMax), Vector3.up,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            return builder.Build("Quad");
        }

        // Extruded rounded slab (lid flap).
        public static Mesh Slab(Rect rect, float radius, float bottom, float top)
        {
            var contour = RoundedRect(rect, radius);
            var builder = new MeshBuilder();
            var center = Centroid(contour);
            for (var i = 0; i < contour.Count; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Count];
                builder.Triangle(At(center, top), At(a, top), At(b, top), Vector3.up);
                var outward = ((a + b) * 0.5f - center).normalized;
                builder.Quad(At(a, top), At(b, top), At(b, bottom), At(a, bottom),
                    new Vector3(outward.x, 0f, outward.y));
            }
            return builder.Build("Slab");
        }

        private static Vector2 RayToRoundedRect(Vector2 origin, Vector2 direction, Rect rect, float radius)
        {
            direction.Normalize();
            var lo = 0f;
            var hi = rect.width + rect.height;
            for (var i = 0; i < 32; i++)
            {
                var mid = (lo + hi) * 0.5f;
                if (RoundedRectDistance(origin + direction * mid, rect, radius) < 0f) lo = mid; else hi = mid;
            }
            return origin + direction * lo;
        }

        private static float RoundedRectDistance(Vector2 p, Rect rect, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x - rect.center.x), Mathf.Abs(p.y - rect.center.y)) -
                    rect.size * 0.5f + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        // ---------- textures and sprites ----------

        public static Texture2D SoftRect(int size, float softness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, name = "Soft rect"
            };
            var rect = new Rect(0.5f, 0.5f, size - 1f, size - 1f);
            var inset = size * softness;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = RoundedRectDistance(new Vector2(x, y), new Rect(rect.x + inset, rect.y + inset,
                        rect.width - 2f * inset, rect.height - 2f * inset), inset);
                    var a = Mathf.Clamp01(1f - d / inset);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            texture.Apply(false, true);
            return texture;
        }

        // Quilted lining: soft diagonal channels (not aligned to the cell grid) with slight puff and weave noise.
        // White-ish so the material colour carries the hue.
        public static Texture2D QuiltTexture(int size, int channels, int seed)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat, name = "Quilted lining"
            };
            var random = new System.Random(seed);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var u = (float)x / size;
                    var v = (float)y / size;
                    var a = Mathf.Repeat((u + v) * channels, 1f);
                    var b = Mathf.Repeat((u - v) * channels, 1f);
                    var da = Mathf.Min(a, 1f - a);
                    var db = Mathf.Min(b, 1f - b);
                    var stitch = Mathf.Exp(-da * da / 0.0012f) + Mathf.Exp(-db * db / 0.0012f);
                    var puff = 0.5f * (Mathf.Sin(a * Mathf.PI) + Mathf.Sin(b * Mathf.PI));
                    var weave = 0.012f * Mathf.Sin(x * 2.1f) * Mathf.Sin(y * 2.3f);
                    var noise = ((float)random.NextDouble() - 0.5f) * 0.035f;
                    var value = Mathf.Clamp01(0.86f + 0.1f * puff - 0.16f * Mathf.Min(1f, stitch) + weave + noise);
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            texture.Apply(true, true);
            return texture;
        }

        // Fine low-contrast woven linen for the packing surface.
        public static Texture2D LinenTexture(int size, int seed)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat, name = "Linen"
            };
            var random = new System.Random(seed);
            var slubX = new float[size];
            var slubY = new float[size];
            for (var i = 0; i < size; i++)
            {
                slubX[i] = ((float)random.NextDouble() - 0.5f) * 0.03f;
                slubY[i] = ((float)random.NextDouble() - 0.5f) * 0.03f;
            }
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var warp = (x & 1) == 0 ? 0.012f : -0.012f;
                    var weft = (y & 1) == 0 ? 0.012f : -0.012f;
                    var noise = ((float)random.NextDouble() - 0.5f) * 0.03f;
                    var value = Mathf.Clamp01(0.95f + warp * weft * 40f * 0.5f + slubX[x] + slubY[y] + noise);
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>Soft packing felt: fine fibres and faint mottling, white-ish so the material colour carries the hue.</summary>
        public static Texture2D FeltTexture(int size, int seed)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat, name = "Felt"
            };
            var random = new System.Random(seed);
            var noise = new float[size * size];
            for (var i = 0; i < noise.Length; i++)
                noise[i] = (float)random.NextDouble();
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var fibre = noise[y * size + x] * 0.5f + noise[y * size + (x + 1) % size] * 0.25f + noise[((y + 1) % size) * size + x] * 0.25f;
                    var mottle = 0.03f * Mathf.Sin(x * 0.11f + 1.3f) * Mathf.Sin(y * 0.09f + 0.4f);
                    var value = Mathf.Clamp01(0.9f + (fibre - 0.5f) * 0.12f + mottle);
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>
        /// Table light falloff: transparent around the warm key (<paramref name="keyCenter"/>, UV space), rising to
        /// <paramref name="strength"/> alpha towards the edges. Used tinted dark over the table only (never over objects).
        /// </summary>
        public static Texture2D TableVignette(int size, Vector2 keyCenter, float strength)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, name = "Table vignette"
            };
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    var d = Mathf.Clamp01((Vector2.Distance(uv, keyCenter) - 0.18f) / 0.62f);
                    var edge = Mathf.Clamp01(Mathf.Max(Mathf.Abs(uv.x - 0.5f), Mathf.Abs(uv.y - 0.5f)) * 2f - 0.55f) / 0.45f;
                    var a = Mathf.Clamp01(0.75f * d * d * (3f - 2f * d) + 0.45f * edge * edge) * strength;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// Soft contact shadow for a footprint: alpha mask of the occupied cells, blurred. The texture covers the
        /// footprint bounding box plus <paramref name="padCells"/> on every side.
        /// </summary>
        public static Texture2D FootprintShadow(ItemShape footprint, int pixelsPerCell, float padCells, float blurCells) =>
            CellMask(footprint.OccupiedCells, pixelsPerCell, padCells, blurCells);

        /// <summary>
        /// Soft alpha mask of a set of non-negative cells (rows grow towards -z), covering their bounding box plus
        /// <paramref name="padCells"/>; adjacent cells merge into one shape with no per-cell seams.
        /// </summary>
        public static Texture2D CellMask(IReadOnlyCollection<Cell> cells, int pixelsPerCell, float padCells, float blurCells)
        {
            int width = 0, depth = 0;
            foreach (var cell in cells)
            {
                width = Mathf.Max(width, cell.X + 1);
                depth = Mathf.Max(depth, cell.Y + 1);
            }
            var w = Mathf.CeilToInt((width + 2f * padCells) * pixelsPerCell);
            var h = Mathf.CeilToInt((depth + 2f * padCells) * pixelsPerCell);
            var occupied = new HashSet<Cell>(cells);
            var mask = new float[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    // Row 0 is the near (-z) edge: the quad maps v = 0 to z = -(depth + pad).
                    var cx = (x + 0.5f) / pixelsPerCell - padCells;
                    var cy = depth + padCells - (y + 0.5f) / pixelsPerCell;
                    if (occupied.Contains(new Cell(Mathf.FloorToInt(cx), Mathf.FloorToInt(cy))))
                        mask[y * w + x] = 1f;
                }
            var radius = Mathf.Max(1, Mathf.RoundToInt(blurCells * pixelsPerCell));
            mask = BoxBlur(BoxBlur(mask, w, h, radius), w, h, radius);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, name = "Cell mask"
            };
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, mask[y * w + x]));
            texture.Apply(false, true);
            return texture;
        }

        private static float[] BoxBlur(float[] source, int w, int h, int radius)
        {
            var temp = new float[source.Length];
            var result = new float[source.Length];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                        sum += source[y * w + Mathf.Clamp(x + k, 0, w - 1)];
                    temp[y * w + x] = sum / (2 * radius + 1);
                }
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var sum = 0f;
                    for (var k = -radius; k <= radius; k++)
                        sum += temp[Mathf.Clamp(y + k, 0, h - 1) * w + x];
                    result[y * w + x] = sum / (2 * radius + 1);
                }
            return result;
        }

        public static Sprite RoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, name = "Rounded"
            };
            var rect = new Rect(0f, 0f, size, size);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = RoundedRectDistance(new Vector2(x + 0.5f, y + 0.5f), rect, radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
                }
            texture.Apply(false, true);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        // Clockwise arrow on a ring, drawn as an anti-aliased mask.
        public static Sprite RotateIcon(int size) => ArcArrowIcon(size, 120f, 280f, true, "Rotate icon");

        // Counter-clockwise hook arrow (undo glyph).
        public static Sprite UndoIcon(int size) => ArcArrowIcon(size, -30f, 210f, false, "Undo icon");

        // Counter-clockwise ring arrow (restart glyph).
        public static Sprite RestartIcon(int size) => ArcArrowIcon(size, 70f, 290f, false, "Restart icon");

        // UI-SLICE-01 glyphs: anti-aliased white masks tinted by the Image colour.
        public static Sprite RingSprite(int size, float thickness) => Mask(size, "Ring", p =>
            Mathf.Clamp01(0.5f - (Mathf.Abs(p.magnitude - (size * 0.5f - thickness * 0.5f - 1f)) - thickness * 0.5f)));

        public static Sprite CheckIcon(int size) => Mask(size, "Check icon", p =>
        {
            var t = size * 0.11f;
            var a = new Vector2(-0.26f, 0.0f) * size;
            var b = new Vector2(-0.06f, -0.2f) * size;
            var c = new Vector2(0.28f, 0.2f) * size;
            return Mathf.Clamp01(0.5f - (Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c)) - t * 0.5f));
        });

        // Suitcase glyph: rounded body with a handle loop on top.
        public static Sprite SuitcaseIcon(int size) => Mask(size, "Suitcase icon", p =>
        {
            var body = RoundedRectDistance(p, new Rect(-0.36f * size, -0.32f * size, 0.72f * size, 0.5f * size), 0.1f * size);
            var handle = Mathf.Abs(RoundedRectDistance(p, new Rect(-0.15f * size, 0.12f * size, 0.3f * size, 0.22f * size), 0.07f * size))
                - 0.035f * size;
            var band = Mathf.Abs(p.y + 0.07f * size) < 0.035f * size && body < 0f ? 1f : 0f;
            return Mathf.Max(Mathf.Clamp01(0.5f - Mathf.Min(body, handle)) - band, 0f);
        });

        private static Sprite Mask(int size, string name, System.Func<Vector2, float> coverage)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            var c = new Vector2(size * 0.5f, size * 0.5f);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage(new Vector2(x + 0.5f, y + 0.5f) - c)));
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        // Arrow on an arc from startDeg sweeping sweepDeg (clockwise or counter-clockwise), head at the end.
        public static Sprite ArcArrowIcon(int size, float startDeg, float sweepDeg, bool clockwise, string name)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, name = name
            };
            var c = new Vector2(size * 0.5f, size * 0.5f);
            var r = size * 0.3f;
            var thickness = size * 0.1f;
            var endAngle = (clockwise ? startDeg - sweepDeg : startDeg + sweepDeg) * Mathf.Deg2Rad;
            var tip = c + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * r;
            var tangent = clockwise
                ? new Vector2(Mathf.Sin(endAngle), -Mathf.Cos(endAngle))
                : new Vector2(-Mathf.Sin(endAngle), Mathf.Cos(endAngle));
            var normal = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
            var headA = tip + normal * thickness * 1.6f;
            var headB = tip - normal * thickness * 1.6f;
            var headC = tip + tangent * thickness * 2.2f;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var v = p - c;
                    // Distance travelled along the arc from startDeg in the arrow's direction.
                    var angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                    var along = Mathf.Repeat(clockwise ? startDeg - angle : angle - startDeg, 360f);
                    var ring = Mathf.Abs(v.magnitude - r) - thickness * 0.5f;
                    var a = along <= sweepDeg ? Mathf.Clamp01(0.5f - ring) : 0f;
                    a = Mathf.Max(a, TriangleCoverage(p, headA, headB, headC));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float TriangleCoverage(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) < 0f)
                (b, c) = (c, b);
            var d = Mathf.Max(EdgeDistance(p, a, b), Mathf.Max(EdgeDistance(p, b, c), EdgeDistance(p, c, a)));
            return Mathf.Clamp01(0.5f - d);
        }

        private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var e = b - a;
            return ((p.x - a.x) * e.y - (p.y - a.y) * e.x) / e.magnitude;
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _triangles = new List<int>();

            public int VertexCount => _vertices.Count;

            // Default UVs are planar in world XZ so fabric textures tile at a stable density.
            public void Vertex(Vector3 position, Vector2? uv = null)
            {
                _vertices.Add(position);
                _uvs.Add(uv ?? new Vector2(position.x, position.z) * 0.35f);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
            {
                var start = _vertices.Count;
                Vertex(a);
                Vertex(b);
                Vertex(c);
                IndexedTriangle(start, start + 1, start + 2, facing);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing,
                Vector2? ua = null, Vector2? ub = null, Vector2? uc = null, Vector2? ud = null)
            {
                var start = _vertices.Count;
                Vertex(a, ua);
                Vertex(b, ub);
                Vertex(c, uc);
                Vertex(d, ud);
                IndexedQuad(start, start + 1, start + 2, start + 3, facing);
            }

            public void IndexedQuad(int a, int b, int c, int d, Vector3 facing)
            {
                IndexedTriangle(a, b, c, facing);
                IndexedTriangle(a, c, d, facing);
            }

            // Unity front faces are clockwise as seen by the viewer; orient each triangle toward `facing`.
            private void IndexedTriangle(int a, int b, int c, Vector3 facing)
            {
                var normal = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
                if (Vector3.Dot(normal, facing) < 0f)
                    (b, c) = (c, b);
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
