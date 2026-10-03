"""ART-GATE-01B: build CabinSuitcase_Golden from the immutable Tripo source GLB.

Run headless:
  blender -b --factory-startup -P Tools/Art/clean_cabin_suitcase.py

Every destructive step logs what it changed. The source GLB is only read.
Coordinates in this script are Blender Z-up; the FBX/glTF exports convert to Y-up.
"""
import bpy, bmesh, math, sys
from pathlib import Path
from collections import Counter
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

REPO = Path(__file__).resolve().parents[2]
ART = REPO / "ArtSource/CabinSuitcase"          # DCC / non-runtime, outside Unity
UNITY = REPO / "Assets/Art/Models/Containers/CabinSuitcase"
SRC = ART / "Tripo/vintage_suitcase_3d_model.glb"
OUT_BLEND = ART / "Blender/CabinSuitcase_Golden.blend"
OUT_GLB = ART / "Blender/CabinSuitcase_Golden.glb"     # interchange copy, not imported by Unity
OUT_FBX = UNITY / "Models/CabinSuitcase_Golden.fbx"
OUT_TEX = UNITY / "Textures/T_CabinSuitcase_Exterior_BaseColor.jpg"

# Measured from the source during inspection (see report).
HINGE_Y, HINGE_Z = 0.3285, 0.280      # centre of the four hinge knuckle barrels
LID_SPLIT_Z = 0.28                    # island centroid above this -> Lid
LINING_SRGB = (0.10, 0.27, 0.28)      # dark teal
TEX_SIZE = 2048
TARGET_INTERIOR_RATIO = 0.80          # usable interior width:depth (ART-GATE-01C, portrait boards)


def log(*a):
    print("[clean]", *a, flush=True)


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def mesh_stats(objs):
    v = f = t = 0
    for o in objs:
        me = o.data
        v += len(me.vertices); f += len(me.polygons); t += sum(len(p.vertices) - 2 for p in me.polygons)
    return v, f, t


def islands(bm):
    """Vertex-connected components -> list of vert lists."""
    seen, out = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        stack, comp = [v], []
        seen.add(v)
        while stack:
            x = stack.pop(); comp.append(x)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y not in seen:
                    seen.add(y); stack.append(y)
        out.append(comp)
    return out


def bbox(points):
    return (Vector([min(p[i] for p in points) for i in range(3)]),
            Vector([max(p[i] for p in points) for i in range(3)]))


# ---------------------------------------------------------------- 0. import + inspect
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SRC))
src = [o for o in bpy.data.objects if o.type == 'MESH']
assert len(src) == 1, f"expected one source mesh, got {len(src)}"
src = src[0]
log("source", SRC.name, "objects", [o.name for o in bpy.data.objects],
    "materials", [m.name for m in bpy.data.materials],
    "images", [(i.name, tuple(i.size)) for i in bpy.data.images])
log("source stats v/f/tris", mesh_stats([src]), "uv", [u.name for u in src.data.uv_layers])

# Unparent from glTF RootNode (identity transform) and drop it.
mw = src.matrix_world.copy()
for o in [o for o in bpy.data.objects if o.type == 'EMPTY']:
    bpy.data.objects.remove(o)
src.matrix_world = mw
src.rotation_mode = 'XYZ'
src.data.transform(src.matrix_world); src.matrix_world = Matrix.Identity(4)

bm = bmesh.new(); bm.from_mesh(src.data)

# ---------------------------------------------------------------- 1. cleanup slivers / degenerates
# Tripo left a few long, near-zero-area triangle strips spanning the hinge (seen as spikes).
removed = []
for comp in islands(bm):
    faces = {f for v in comp for f in v.link_faces}
    lo, hi = bbox([v.co for v in comp])
    ext = max(hi - lo)
    area = sum(f.calc_area() for f in faces)
    if len(comp) <= 12 and ext > 0.05 and area / (ext * ext) < 0.002:
        removed.append((len(comp), round(ext, 3), round(area, 6)))
        bmesh.ops.delete(bm, geom=comp, context='VERTS')
log("DESTRUCTIVE removed sliver islands (verts, extent, area):", removed)
nf = len(bm.faces)
bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=1e-6)
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
log(f"DESTRUCTIVE degenerate dissolve: faces {nf} -> {len(bm.faces)}, loose verts removed {len(loose)}")

# ---------------------------------------------------------------- 2. material classification
img = bpy.data.images[0]
W, H = img.size
px = np.empty(W * H * 4, dtype=np.float32); img.pixels.foreach_get(px)
px = px.reshape(H, W, 4)
uvl = bm.loops.layers.uv.active


def sample(uv):
    x = min(W - 1, max(0, int((uv.x % 1.0) * W))); y = min(H - 1, max(0, int((uv.y % 1.0) * H)))
    return px[y, x, :3]


def is_navy(c):
    r, g, b = c
    return b > r + 0.05 and b >= g - 0.02 and b > 0.12


def is_gold(c):
    r, g, b = c
    return r > 0.35 and g > 0.6 * r and b < 0.75 * g


def in_base_cavity(c):
    # Base packing cavity (floor + inner walls + cushion), inside the zipper ring.
    return abs(c.x) < 0.385 and -0.355 < c.y < 0.30 and 0.17 < c.z < 0.276


exterior = src.data.materials[0]
exterior.name = "M_CabinSuitcase_Exterior"
lining = bpy.data.materials.new("M_CabinSuitcase_Lining")
bsdf = lining.node_tree.nodes.get("Principled BSDF")
lin = [srgb_to_linear(c) for c in LINING_SRGB]
bsdf.inputs["Base Color"].default_value = (*lin, 1.0)
bsdf.inputs["Roughness"].default_value = 0.85
lining.diffuse_color = (*lin, 1.0)
exterior.diffuse_color = (0.55, 0.20, 0.13, 1.0)
src.data.materials.append(lining)

vote = Counter()
for f in bm.faces:
    uvs = [l[uvl].uv for l in f.loops]
    ctr = sum(uvs, Vector((0, 0))) / len(uvs)
    cols = [sample(ctr)] + [sample(ctr.lerp(u, 0.5)) for u in uvs]
    navy = sum(map(is_navy, cols)) >= 2
    gold = sum(map(is_gold, cols)) >= 2
    spatial = in_base_cavity(f.calc_center_median()) and not gold
    if navy or spatial:
        f.material_index = 1
        vote["navy" if navy else "spatial_override"] += 1
log("lining faces by rule", dict(vote), "of", len(bm.faces))

# ---------------------------------------------------------------- 3. Base / Lid split by island centroid
lid_faces, base_faces = set(), set()
for comp in islands(bm):
    faces = {f for v in comp for f in v.link_faces}
    cz = sum(v.co.z for v in comp) / len(comp)
    (lid_faces if cz > LID_SPLIT_Z else base_faces).update(faces)
log(f"split: base faces {len(base_faces)}, lid faces {len(lid_faces)}")
bm.to_mesh(src.data)

base = src; base.name = "Base"; base.data.name = "Base"
lid = base.copy(); lid.data = base.data.copy(); lid.name = lid.data.name = "Lid"
bpy.context.scene.collection.objects.link(lid)
lid_idx = {f.index for f in lid_faces}
for ob, keep_lid in ((base, False), (lid, True)):
    b = bmesh.new(); b.from_mesh(ob.data); b.faces.ensure_lookup_table()
    kill = [f for f in b.faces if (f.index in lid_idx) != keep_lid]
    bmesh.ops.delete(b, geom=kill, context='FACES')
    bmesh.ops.delete(b, geom=[v for v in b.verts if not v.link_faces], context='VERTS')
    b.to_mesh(ob.data); b.free()
bm.free()

# ---------------------------------------------------------------- 4. Lid to closed pose about the hinge
hinge = Vector((0.0, HINGE_Y, HINGE_Z))
lb = bmesh.new(); lb.from_mesh(lid.data)
n = Vector();
for f in lb.faces:
    c = f.calc_center_median()
    if c.y > 0.46 and abs(c.x) < 0.3 and c.z > 0.35:   # outer lid panel in open pose
        n += f.normal * f.calc_area()
n.normalize()
theta = math.atan2(n.y, n.z)   # +X rotation that turns the outer panel to face +Z
log(f"lid outer normal {tuple(round(a, 4) for a in n)} -> close angle {math.degrees(theta):.2f} deg about +X")


def rotate(bmx, ang, ctr):
    bmesh.ops.rotate(bmx, verts=bmx.verts, cent=ctr, matrix=Matrix.Rotation(ang, 3, 'X'))


rotate(lb, theta, hinge)


def side_wall(verts):
    return [v.co for v in verts if 0.40 < abs(v.co.x) < 0.445]


def rim_flatness(vs):
    """Spread of the lowest lid-shell z per y-bin along the side walls (closed pose)."""
    bins = {}
    for p in vs:
        k = round(p.y, 2); bins[k] = min(bins.get(k, 9), p.z)
    zs = list(bins.values())
    return max(zs) - min(zs)


best = (rim_flatness(side_wall(lb.verts)), 0.0)
for step in range(-60, 61):
    a = math.radians(step / 10)
    m = Matrix.Rotation(a, 3, 'X')
    vs = [m @ (p - hinge) + hinge for p in side_wall(lb.verts)]
    best = min(best, (rim_flatness(vs), a))
rotate(lb, best[1], hinge)
theta_total = theta + best[1]
log(f"rim tilt correction {math.degrees(best[1]):+.2f} deg (rim z spread {best[0]:.4f}); total close angle {math.degrees(theta_total):.2f}")

bb = bmesh.new(); bb.from_mesh(base.data)
base_wall = [p for p in side_wall(bb.verts) if p.y > -0.43]
B0, B1 = bbox(base_wall)
L0, L1 = bbox(side_wall(lb.verts))
log(f"closed lid y [{L0.y:.3f},{L1.y:.3f}] len {L1.y - L0.y:.3f}; base y [{B0.y:.3f},{B1.y:.3f}] len {B1.y - B0.y:.3f}")

# ---------------------------------------------------------------- 5. Segmented stretch (approved)
# Back (hinge) end fixed, front end translated by -delta, flat middle band stretched linearly.
# Islands shorter than RIGID_LEN (hardware, rivets, latches, buckle) move rigidly with their centroid.
RIGID_LEN = 0.12


def segmented_stretch(bmx, band_lo, band_hi, delta):
    def fy(y):
        if y >= band_hi:
            return y
        if y <= band_lo:
            return y - delta
        return band_hi - (band_hi - y) * (band_hi - band_lo + delta) / (band_hi - band_lo)

    rigid = stretched = 0
    for comp in islands(bmx):
        lo, hi = bbox([v.co for v in comp])
        if hi.y - lo.y < RIGID_LEN:
            cy = (lo.y + hi.y) / 2; d = fy(cy) - cy
            for v in comp:
                v.co.y += d
            rigid += 1
        else:
            for v in comp:
                v.co.y = fy(v.co.y)
            stretched += 1
    return rigid, stretched, (band_hi - band_lo + delta) / (band_hi - band_lo)


delta = (B1.y - B0.y) - (L1.y - L0.y)
band_lo, band_hi = L0.y + 0.13, L1.y - 0.13
rigid, stretched, factor = segmented_stretch(lb, band_lo, band_hi, delta)
log(f"DESTRUCTIVE lid stretch delta {delta:.4f} over band y[{band_lo:.3f},{band_hi:.3f}] "
    f"(factor {factor:.3f}); islands rigid {rigid}, stretched {stretched}")

# Seat: hinge-end flush with base back, rim on base rim.
L0, L1 = bbox(side_wall(lb.verts))
base_rim = max(p.z for p in base_wall if p.z < 0.30)
seat = Vector((0, B1.y - L1.y, base_rim - L0.z))
bmesh.ops.translate(lb, verts=lb.verts, vec=seat)
L0, L1 = bbox(side_wall(lb.verts))
log(f"seat offset {tuple(round(a, 4) for a in seat)}; closed lid y [{L0.y:.3f},{L1.y:.3f}] vs base [{B0.y:.3f},{B1.y:.3f}], rim z {base_rim:.4f}")

# ---------------------------------------------------------------- 5b. Depth correction (ART-GATE-01C)
# The source interior is landscape; boards are portrait (Lv1 5x7). Deepen Base and Lid together, in the closed
# pose, with the same mapping: hinge end fixed, front hardware zone translated, middle band stretched, small
# hardware islands rigid. Width is unchanged, so identity Lid rotation still closes flush.
probe_z = HINGE_Z - 0.082                     # just above the source floor (z 0.188)
cavity = BVHTree.FromBMesh(bb)
probe = Vector((0, (B0.y + B1.y) / 2, probe_z))
hit = {k: cavity.ray_cast(probe, Vector(d))[0] for k, d in
       {"x-": (-1, 0, 0), "x+": (1, 0, 0), "y-": (0, -1, 0), "y+": (0, 1, 0)}.items()}
cav_w, cav_d = hit["x+"].x - hit["x-"].x, hit["y+"].y - hit["y-"].y
depth_delta = cav_w / TARGET_INTERIOR_RATIO - cav_d
d_lo, d_hi = B0.y + 0.13, B1.y - 0.13
assert hit["y-"].y < d_lo and hit["y+"].y > d_hi, "cavity walls must sit in the rigid end zones"
for name, bmx in (("Base", bb), ("Lid", lb)):
    rigid, stretched, factor = segmented_stretch(bmx, d_lo, d_hi, depth_delta)
    log(f"DESTRUCTIVE {name} depth correction delta {depth_delta:.4f} over band y[{d_lo:.3f},{d_hi:.3f}] "
        f"(factor {factor:.3f}); islands rigid {rigid}, stretched {stretched}")
log(f"cavity before depth correction {cav_w:.4f} x {cav_d:.4f} (ratio {cav_w / cav_d:.3f}) -> target ratio {TARGET_INTERIOR_RATIO}")
base_wall = [p for p in side_wall(bb.verts) if p.y > -0.43 - depth_delta]
B0, B1 = bbox(base_wall)
L0, L1 = bbox(side_wall(lb.verts))
log(f"after depth correction: base y [{B0.y:.3f},{B1.y:.3f}] len {B1.y - B0.y:.3f}; closed lid y [{L0.y:.3f},{L1.y:.3f}]")
lb.to_mesh(lid.data); bb.to_mesh(base.data); lb.free(); bb.free()

# ---------------------------------------------------------------- 6. Normalize: base footprint centred, bottom at 0
cx, cy = (B0.x + B1.x) / 2, (B0.y + B1.y) / 2
zmin = min(v.co.z for v in base.data.vertices)
off = Vector((-cx, -cy, -zmin))
for ob in (base, lid):
    ob.data.transform(Matrix.Translation(off))
hinge += off
# Lid origin on the hinge axis; mesh authored closed, node rotation holds the open pose.
lid.data.transform(Matrix.Translation(-hinge))
lid.location = hinge
lid.rotation_mode = 'XYZ'
lid.rotation_euler = (-theta_total, 0, 0)
log(f"recentre offset {tuple(round(a, 4) for a in off)}; hinge pivot {tuple(round(a, 4) for a in hinge)}; lid open rot X {-math.degrees(theta_total):.2f} deg")

# ---------------------------------------------------------------- 7. Normals / shading
for ob in (base, lid):
    me = ob.data
    if me.has_custom_normals:
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    b = bmesh.new(); b.from_mesh(me)
    # Winding consistency: edge used twice in the same direction == flipped neighbour.
    bad = 0
    for e in b.edges:
        if len(e.link_loops) == 2:
            l1, l2 = e.link_loops
            bad += l1.vert == l2.vert
    log(f"{ob.name}: inconsistent-winding edges {bad}")
    if bad:
        bmesh.ops.recalc_face_normals(b, faces=b.faces[:])
        log(f"{ob.name}: DESTRUCTIVE recalculated face normals")
    b.to_mesh(me); b.free()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35), keep_sharp_edges=True)

# ---------------------------------------------------------------- 8. Texture budget
if img.size[0] > TEX_SIZE:
    img.scale(TEX_SIZE, TEX_SIZE)
    log(f"DESTRUCTIVE exterior texture downscaled to {TEX_SIZE}")
# The glTF importer wires texture -> Mix(MULTIPLY, baseColorFactor) -> Base Color. FBX only exports a
# texture linked straight to Base Color, so bake the factor into the pixels (linear space) and drop the Mix.
nt = exterior.node_tree
tex_node = next(n for n in nt.nodes if n.type == 'TEX_IMAGE')
mix = next((n for n in nt.nodes if n.type == 'MIX'), None)
factor = 1.0
if mix:
    sock = {i.identifier: i for i in mix.inputs}
    fac_rgb = tuple(sock["B_Color"].default_value)[:3]
    assert mix.blend_type == 'MULTIPLY' and sock["Factor_Float"].default_value == 1.0 and len(set(fac_rgb)) == 1, \
        f"unexpected base colour mix {mix.blend_type} {fac_rgb}"
    factor = fac_rgb[0]
    nt.nodes.remove(mix)
nt.links.new(tex_node.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
w, h = img.size
a = np.empty(w * h * 4, dtype=np.float32); img.pixels.foreach_get(a)
rgb = a.reshape(-1, 4)[:, :3]
lin_px = np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4) * factor
rgb[:] = np.where(lin_px <= 0.0031308, lin_px * 12.92, 1.055 * np.power(lin_px, 1 / 2.4) - 0.055)
log(f"DESTRUCTIVE baked baseColorFactor {factor} into exterior texture (linear multiply), removed Mix node")
tmp = bpy.data.images.new("tmp_export", w, h)
tmp.pixels.foreach_set(a)
tmp.file_format = 'JPEG'
OUT_TEX.parent.mkdir(parents=True, exist_ok=True)
tmp.save(filepath=str(OUT_TEX), quality=90)
bpy.data.images.remove(tmp); bpy.data.images.remove(img)
# Reload so .blend/GLB/FBX all carry exactly the runtime file's pixels.
img = bpy.data.images.load(str(OUT_TEX))
img.name = "T_CabinSuitcase_Exterior_BaseColor"
tex_node.image = img
img.pack()                    # .blend stays self-contained; FBX references OUT_TEX relatively
log("wrote texture", OUT_TEX, tuple(img.size))

# ---------------------------------------------------------------- 9. Anchors (presentation-only)
# The source floor is two loose triangles that leave most of the cavity open (the "coral
# bleed" is the bottom panel seen through that hole). Replace them with a clean insert.
b = bmesh.new(); b.from_mesh(base.data)
broken = [f for f in b.faces if f.normal.z > 0.99 and f.calc_area() > 0.05
          and abs(f.calc_center_median().x) < 0.4 and 0.1 < f.calc_center_median().z < 0.25]
floor_z = sum(f.calc_center_median().z for f in broken) / len(broken)
log(f"DESTRUCTIVE removed {len(broken)} broken floor triangles at z {floor_z:.4f}")
bmesh.ops.delete(b, geom=broken, context='FACES')
bmesh.ops.delete(b, geom=[v for v in b.verts if not v.link_faces], context='VERTS')
b.to_mesh(base.data); b.free()

dg = bpy.context.evaluated_depsgraph_get()
tree = BVHTree.FromObject(base, dg)
probe = Vector((0, 0, floor_z + 0.01))
walls = {}
for k, d in {"x-": (-1, 0, 0), "x+": (1, 0, 0), "y-": (0, -1, 0), "y+": (0, 1, 0)}.items():
    h = tree.ray_cast(probe, Vector(d))
    walls[k] = h[0]
log("cavity walls at floor height", {k: tuple(round(a, 3) for a in v) for k, v in walls.items()})
pad = 0.004   # tuck the insert edges into the walls
ix0, ix1 = walls["x-"].x - pad, walls["x+"].x + pad
iy0, iy1 = walls["y-"].y - pad, walls["y+"].y + pad
ime = bpy.data.meshes.new("Interior")
ime.from_pydata([(ix0, iy0, floor_z), (ix1, iy0, floor_z), (ix1, iy1, floor_z), (ix0, iy1, floor_z)], [], [(0, 1, 2, 3)])
uv = ime.uv_layers.new(name="UVMap")
for li, (u, v) in enumerate([(0, 0), (1, 0), (1, 1), (0, 1)]):
    uv.data[li].uv = (u, v)
ime.materials.append(lining)
interior = bpy.data.objects.new("Interior", ime)
bpy.context.scene.collection.objects.link(interior)
log(f"interior floor insert x[{ix0:.3f},{ix1:.3f}] y[{iy0:.3f},{iy1:.3f}] z {floor_z:.4f}")

dg = bpy.context.evaluated_depsgraph_get()
trees = [BVHTree.FromObject(base, dg), BVHTree.FromObject(interior, dg)]
hits = {}
for x in range(-90, 91):
    for y in range(-90, 91):
        best = None
        for t in trees:
            h = t.ray_cast(Vector((x / 200, y / 200, 1.0)), Vector((0, 0, -1)))
            if h[0] is not None and (best is None or h[0].z > best):
                best = h[0].z
        hits[(x / 200, y / 200)] = best
floor_pts = [p for p, z in hits.items() if z is not None and abs(z - floor_z) < 0.002
             and ix0 <= p[0] <= ix1 and iy0 <= p[1] <= iy1]
fx0, fx1 = min(p[0] for p in floor_pts), max(p[0] for p in floor_pts)
fy0, fy1 = min(p[1] for p in floor_pts), max(p[1] for p in floor_pts)
holes = [p for p, z in hits.items() if ix0 <= p[0] <= ix1 and iy0 <= p[1] <= iy1 and (z is None or z < floor_z - 0.002)]
log(f"visible floor rect x[{fx0:.3f},{fx1:.3f}] y[{fy0:.3f},{fy1:.3f}] ({len(floor_pts)} samples); "
    f"top-down rays below floor inside insert: {len(holes)}")

root = bpy.data.objects.new("CabinSuitcase_Golden", None)
bpy.context.scene.collection.objects.link(root)
anchors = {
    "InteriorAnchor": Vector(((fx0 + fx1) / 2, (fy0 + fy1) / 2, floor_z)),
    "InteriorMin": Vector((fx0, fy0, floor_z)),
    "InteriorMax": Vector((fx1, fy1, base_rim + off.z)),
    "HingeAnchor": hinge.copy(),
}
for name, p in anchors.items():
    e = bpy.data.objects.new(name, None); e.empty_display_size = 0.03; e.location = p
    bpy.context.scene.collection.objects.link(e); e.parent = root
    log(f"anchor {name} {tuple(round(a, 4) for a in p)}")
for ob in (base, lid, interior):
    ob.parent = root

# ---------------------------------------------------------------- 10. Save + export
for p in (OUT_GLB, OUT_BLEND, OUT_FBX):
    p.parent.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0   # no .blend1 backups next to the asset
assert SRC.resolve() not in {p.resolve() for p in (OUT_GLB, OUT_BLEND, OUT_FBX, OUT_TEX)}
bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND), compress=True)
bpy.ops.export_scene.gltf(filepath=str(OUT_GLB), export_format='GLB', export_yup=True,
                          export_apply=False, export_image_format='JPEG', export_cameras=False,
                          export_lights=False, export_extras=False)
# Unity FBX: Y up, -Z forward, metres (FBX_SCALE_ALL -> no 100x root scale), axis conversion baked
# into the data so the root carries no -90 X rotation. Lid keeps its open rotation on its node
# (identity == closed). No animation, cameras or lights.
bpy.ops.export_scene.fbx(filepath=str(OUT_FBX), object_types={'EMPTY', 'MESH'},
                         axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         use_mesh_modifiers=False, mesh_smooth_type='OFF', use_tspace=False,
                         add_leaf_bones=False, bake_anim=False, path_mode='RELATIVE',
                         embed_textures=False, use_custom_props=False)
log("final stats v/f/tris", mesh_stats([base, lid, interior]),
    "base", mesh_stats([base]), "lid", mesh_stats([lid]),
    "materials", [m.name for m in bpy.data.materials if m.users],
    "images", [(i.name, tuple(i.size)) for i in bpy.data.images if i.users])
log("wrote", OUT_FBX, OUT_BLEND, OUT_GLB)
