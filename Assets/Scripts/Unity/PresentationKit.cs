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
        public static Sprite RotateIcon(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, name = "Rotate icon"
            };
            var c = new Vector2(size * 0.5f, size * 0.5f);
            var r = size * 0.3f;
            var thickness = size * 0.1f;
            const float startDeg = 120f;
            const float sweepDeg = 280f;
            var endAngle = (startDeg - sweepDeg) * Mathf.Deg2Rad;
            var tip = c + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * r;
            var tangent = new Vector2(Mathf.Sin(endAngle), -Mathf.Cos(endAngle));
            var normal = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
            var headA = tip + normal * thickness * 1.6f;
            var headB = tip - normal * thickness * 1.6f;
            var headC = tip + tangent * thickness * 2.2f;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var v = p - c;
                    // Clockwise from startDeg: angle decreases.
                    var angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                    var along = Mathf.Repeat(startDeg - angle, 360f);
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
