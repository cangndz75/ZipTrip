"""Validate the exported Unity FBX by re-importing it into a clean scene, then render it.

  blender -b --factory-startup -P Tools/Art/render_cabin_suitcase.py -- <out_dir>

Exits non-zero if any check fails.
"""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector
from io_scene_fbx import parse_fbx

REPO = Path(__file__).resolve().parents[2]
FBX = REPO / "Assets/Art/Models/Containers/CabinSuitcase/Models/CabinSuitcase_Golden.fbx"
OUT = Path(sys.argv[sys.argv.index("--") + 1])
OUT.mkdir(parents=True, exist_ok=True)
EXPECTED_TRIS = {"Base": 22629, "Lid": 13972, "Interior": 2}
EXPECTED_SLOTS = {"Base": ["M_CabinSuitcase_Exterior", "M_CabinSuitcase_Lining"],
                  "Lid": ["M_CabinSuitcase_Exterior", "M_CabinSuitcase_Lining"],
                  "Interior": ["M_CabinSuitcase_Lining"]}
failures = []


def check(ok, msg):
    print("[check]", "PASS" if ok else "FAIL", msg)
    if not ok:
        failures.append(msg)


# ---- raw FBX: what Unity actually reads (units, node transforms, animation)
root_elem, _ = parse_fbx.parse(str(FBX))


def child(e, eid):
    return next((c for c in e.elems if c.id == eid), None)


def props70(e):
    p = child(e, b"Properties70")
    return {c.props[0].decode(): c.props[4:] for c in p.elems} if p else {}


gs = props70(child(root_elem, b"GlobalSettings"))
unit = gs["UnitScaleFactor"][0]
print("[raw] UnitScaleFactor", unit, "UpAxis", gs["UpAxis"][0], "FrontAxis", gs["FrontAxis"][0],
      "FrontAxisSign", gs["FrontAxisSign"][0], "CoordAxis", gs["CoordAxis"][0])
raw_nodes = {}
for c in child(root_elem, b"Objects").elems:
    if c.id == b"Model":
        name = c.props[1].split(b"\x00")[0].decode()
        p = props70(c)
        # Properties70 omits defaults: scaling defaults to 1, the rest to 0.
        raw_nodes[name] = {k: tuple(round(float(x), 4) for x in p.get(k, d)) for k, d in
                           (("Lcl Translation", (0, 0, 0)), ("Lcl Rotation", (0, 0, 0)),
                            ("Lcl Scaling", (1, 1, 1)), ("PreRotation", (0, 0, 0)))}
        print("[raw]", name, c.props[2].decode(), raw_nodes[name])
    if c.id == b"Geometry":
        v = child(c, b"Vertices").props[0]
        xs, ys, zs = v[0::3], v[1::3], v[2::3]
        print("[raw] geometry", c.props[1].split(b"\x00")[0].decode(), "extent",
              tuple(round(max(a) - min(a), 4) for a in (xs, ys, zs)))
anim = [c for c in child(root_elem, b"Objects").elems if c.id in (b"AnimationCurve", b"AnimationCurveNode")]
check(not anim, f"no baked animation curves ({len(anim)})")
check(all(n["Lcl Scaling"] == (1, 1, 1) for n in raw_nodes.values()), "all FBX node scales are 1")
check(raw_nodes["CabinSuitcase_Golden"]["Lcl Rotation"] == (0, 0, 0), "root has no axis-conversion rotation")
# Unity (Convert Units on) scales file units by UnitScaleFactor/100, so 100 == metres, factor 1.
check(unit == 100.0, f"FBX units are metres (UnitScaleFactor {unit})")

# ---- clean-scene re-import
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FBX))
sc = bpy.context.scene


def tree(o, d=0):
    extra = ""
    if o.type == 'MESH':
        me = o.data
        extra = f" v={len(me.vertices)} f={len(me.polygons)} mats={[m.name for m in me.materials]} uv={len(me.uv_layers)}"
    print("[render] " + "  " * d + f"{o.name} ({o.type}) loc={tuple(round(a, 4) for a in o.location)} "
          f"rot={tuple(round(math.degrees(a), 2) for a in o.matrix_basis.to_euler())} scale={tuple(round(a, 3) for a in o.scale)}{extra}")
    for c in o.children:
        tree(c, d + 1)


for o in sc.objects:
    if o.parent is None:
        tree(o)
print("[render] images", [(i.name, tuple(i.size)) for i in bpy.data.images])

names = {o.name for o in sc.objects}
for n in ("CabinSuitcase_Golden", "Base", "Lid", "Interior", "InteriorAnchor", "InteriorMin", "InteriorMax", "HingeAnchor"):
    check(n in names and bpy.data.objects[n].parent == (None if n == "CabinSuitcase_Golden" else bpy.data.objects["CabinSuitcase_Golden"]),
          f"{n} present under root")
base, lid, interior = (bpy.data.objects[n] for n in ("Base", "Lid", "Interior"))
check(base.data != lid.data and base.type == lid.type == 'MESH', "Base and Lid are separate meshes")
for o in sc.objects:
    check(all(abs(s - 1) < 1e-4 for s in o.scale), f"{o.name} scale 1 after import")
for o in (base, lid, interior):
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    check(tris == EXPECTED_TRIS[o.name], f"{o.name} tris {tris} == {EXPECTED_TRIS[o.name]}")
    slots = [m.name if m else None for m in o.data.materials]
    check(slots == EXPECTED_SLOTS[o.name], f"{o.name} material slots {slots}")
    check(len(o.data.uv_layers) == 1, f"{o.name} has UVs")
    # normals: imported split normals should agree with face winding
    flipped = 0
    for p in o.data.polygons:
        for li in p.loop_indices:
            flipped += o.data.corner_normals[li].vector.dot(p.normal) < 0
    check(flipped == 0, f"{o.name} split normals opposing face normal: {flipped}")
ext = bpy.data.materials["M_CabinSuitcase_Exterior"]
tex = [n.image for n in ext.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
check(bool(tex) and tuple(tex[0].size) == (2048, 2048), f"exterior texture bound: {[(t.name, tuple(t.size)) for t in tex]}")

hinge = bpy.data.objects["HingeAnchor"].matrix_world.translation
check((lid.matrix_world.translation - hinge).length < 1e-4,
      f"Lid pivot on HingeAnchor {tuple(round(a, 4) for a in hinge)}")
lid.rotation_mode = 'XYZ'
open_x = lid.rotation_euler.x
print("[render] lid open rotation (deg)", tuple(round(math.degrees(a), 3) for a in lid.rotation_euler))
check(abs(lid.rotation_euler.y) < 1e-4 and abs(lid.rotation_euler.z) < 1e-4, "lid open rotation is about X only")


def side_wall(o):
    return [o.matrix_world @ v.co for v in o.data.vertices if 0.40 < abs((o.matrix_world @ v.co).x) < 0.445]


lid.rotation_euler = (0, 0, 0); bpy.context.view_layer.update()
lw, bw = side_wall(lid), side_wall(base)   # |x| band already excludes the centred handle
ly = (min(p.y for p in lw), max(p.y for p in lw)); by = (min(p.y for p in bw), max(p.y for p in bw))
rim = max(p.z for p in bw if p.z < 0.30); lz = min(p.z for p in lw)
print(f"[render] closed lid y {ly} base y {by}; lid rim z {lz:.4f} base rim z {rim:.4f}")
check(abs(ly[0] - by[0]) < 0.002 and abs(ly[1] - by[1]) < 0.002 and abs(lz - rim) < 0.002,
      "identity Lid rotation == closed flush on base")
allp = [o.matrix_world @ v.co for o in (base, lid, interior) for v in o.data.vertices]
dims = tuple(round(max(p[i] for p in allp) - min(p[i] for p in allp), 4) for i in range(3))
print("[render] closed dimensions (m, Blender XYZ)", dims)
check(0.5 < max(dims) < 1.5, "dimensions sensible (metres)")
lid.rotation_euler.x = open_x; bpy.context.view_layer.update()
allp = [o.matrix_world @ v.co for o in (base, lid, interior) for v in o.data.vertices]
print("[render] open dimensions (m, Blender XYZ)", tuple(round(max(p[i] for p in allp) - min(p[i] for p in allp), 4) for i in range(3)))
print("[render] totals verts", sum(len(o.data.vertices) for o in (base, lid, interior)),
      "tris", sum(len(p.vertices) - 2 for o in (base, lid, interior) for p in o.data.polygons),
      "objects", len(sc.objects))

sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x = sc.render.resolution_y = 1000
sc.view_settings.view_transform = 'Standard'
world = bpy.data.worlds.new("w"); sc.world = world
world.color = (0.35, 0.35, 0.37)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(35), math.radians(10), math.radians(-25))
sc.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
target = Vector((0, 0.02, 0.25))


def shoot(name, elev, az, dist=3.2):
    e, a = math.radians(elev), math.radians(az)
    d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.location = target + d * dist
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = str(OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)


shoot("01_gameplay_72deg", 72, 0)
shoot("01b_gameplay_72deg_close", 72, 0, 2.2)
shoot("02_side_hinge", 5, 90)
shoot("02b_three_quarter", 35, 35)
lid.rotation_euler.x = open_x * 0.4
shoot("03_lid_closing_60pct", 30, 40)
lid.rotation_euler.x = 0
shoot("03b_lid_closed", 30, 40)
shoot("03c_lid_closed_side", 5, 90)
lid.rotation_euler.x = open_x

# Material-separated: flat viewport colours per material, workbench.
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'MATERIAL'
palette = {"M_CabinSuitcase_Exterior": (0.8, 0.35, 0.2, 1), "M_CabinSuitcase_Lining": (0.1, 0.5, 0.5, 1)}
for m in bpy.data.materials:
    m.diffuse_color = palette.get(m.name, (1, 0, 1, 1))
shoot("05_material_split", 55, 25)
shoot("05b_material_split_top", 89, 0)

# Object-separated colours (Base / Lid / Interior).
sc.display.shading.color_type = 'OBJECT'
for o, c in (("Base", (0.8, 0.5, 0.2, 1)), ("Lid", (0.2, 0.5, 0.85, 1)), ("Interior", (0.2, 0.8, 0.3, 1))):
    bpy.data.objects[o].color = c
shoot("06_object_split", 35, 35)

# Wireframe.
sc.display.shading.color_type = 'SINGLE'
sc.display.shading.single_color = (0.85, 0.85, 0.85)
for o in sc.objects:
    if o.type == 'MESH':
        m = o.modifiers.new("wf", 'WIREFRAME'); m.thickness = 0.0012; m.use_replace = True
shoot("04_wireframe", 55, 25)

print("[check] FAILURES:", failures if failures else "none")
sys.exit(1 if failures else 0)
