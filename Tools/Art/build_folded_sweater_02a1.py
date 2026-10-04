"""ART-GATE-02A.1: folded-sweater physical-readability review, Blender 5.2.

Run headlessly. Reads the prior Folded proposal and approved Open source;
writes only a new Folded review source/FBX and three review renders + stats.
"""

import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[2]
FOLDED_DIR = ROOT / "ArtSource/GoldenItems/SweaterFolded"
FOLDED_SOURCE = FOLDED_DIR / "SweaterFolded_Review.blend"
OPEN_SOURCE = ROOT / "ArtSource/GoldenItems/SweaterOpen/SweaterOpen_Review.blend"
OUT = ROOT / "Builds/art-gate-02a1"
OUT.mkdir(parents=True, exist_ok=True)


def triangles(obj):
    return sum(len(face.vertices) - 2 for face in obj.data.polygons)


def load_meshes(path, prefix):
    with bpy.data.libraries.load(str(path), link=False) as (source, target):
        target.objects = [name for name in source.objects if name.startswith(prefix)]
    result = []
    for obj in target.objects:
        if obj and obj.type == 'MESH':
            bpy.context.collection.objects.link(obj)
            result.append(obj)
    if not result:
        raise ValueError(f"No meshes found in {path}")
    return result


def rounded_outline(xmin, xmax, ymin, ymax, radius, steps=16):
    result = []
    corners = ((xmax-radius, ymax-radius, 0),
               (xmin+radius, ymax-radius, 90),
               (xmin+radius, ymin+radius, 180),
               (xmax-radius, ymin+radius, 270))
    for cx, cy, start in corners:
        for step in range(steps):
            angle = math.radians(start + step * 90 / steps)
            result.append((cx + radius*math.cos(angle),
                           cy + radius*math.sin(angle)))
    return result


def cloth_layer(name, limits, lower_z, upper_z, front_material, underside_material, top):
    # Each closed padded cloth layer has its own softened return edge. Their
    # overlapping silhouettes create a real folded side profile, while top UVs
    # continue to sample the approved abstract-knit image.
    xmin, xmax, ymin, ymax = limits
    outline = rounded_outline(*limits, radius=.19 if not top else .24)
    n = len(outline)
    verts, faces, face_mats = [], [], []

    def ring(scale, z=None):
        start = len(verts)
        for x, y in outline:
            px = 1 + (x-1)*scale
            py = -2 + (y+2)*scale
            if z is None:
                radial = max(abs((px-1)/.95), abs((py+2)/1.95))
                pz = upper_z - .019*radial**6
                pz += .006*math.sin(py*1.7 + px*2.2)
                if top:
                    neckline = ((px-1)/.60)**2 + ((py+.46)/.36)**2
                    pz -= .020*math.exp(-neckline*1.2)
                    pz -= .008*math.exp(-((py+3.57)/.11)**2)
            else:
                pz = z + .004*math.sin(py*2.1 + px*1.1) if z > lower_z else z
            verts.append((px, py, pz))
        return start

    side = [ring(.92, lower_z), ring(.985, lower_z+.018),
            ring(1.0, lower_z+.045), ring(.995, upper_z-.045),
            ring(.96, upper_z-.012), ring(.91)]
    surface = [side[-1]]
    for step in range(1, 12):
        surface.append(ring(.91*(1-step/12)))
    center = len(verts)
    verts.append((1, -2, upper_z + (.003 if top else 0)))
    bottom = len(verts)
    verts.append((1, -2, lower_z))
    for a, b in zip(side, side[1:]):
        for i in range(n):
            j = (i+1) % n
            faces.append((a+i, a+j, b+j, b+i))
            face_mats.append(0)
    for a, b in zip(surface, surface[1:]):
        for i in range(n):
            j = (i+1) % n
            faces.append((a+i, a+j, b+j, b+i))
            face_mats.append(0)
    for i in range(n):
        j = (i+1) % n
        faces.append((surface[-1]+i, surface[-1]+j, center))
        face_mats.append(0)
        faces.append((bottom, side[0]+j, side[0]+i))
        face_mats.append(1)
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update(calc_edges=True)
    data.materials.append(front_material)
    data.materials.append(underside_material)
    uv = data.uv_layers.new(name="TopReferenceUV")
    for polygon, material_index in zip(data.polygons, face_mats):
        polygon.material_index = material_index
        polygon.use_smooth = True
        for loop in polygon.loop_indices:
            v = data.vertices[data.loops[loop].vertex_index].co
            uv.data[loop].uv = (.28 + .44*(v.x-.05)/1.90,
                                .66 - .52*(-v.y-.05)/3.90)
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    return obj


def seat_collar(collar):
    for vertex in collar.data.vertices:
        x, y, z = vertex.co
        angle = math.atan2(y + .45, x - 1)
        back = (.5 + .5 * math.sin(angle))
        vertex.co.x += .009 * math.sin(angle)
        vertex.co.y += .012 * math.cos(angle)
        vertex.co.z = z - .004 - .004 * back
    collar.data.update()


def folded_sleeve(material):
    # A flattened sleeve return along one side. It crosses only the outer
    # graphic edge; UVs sample the existing approved 1024 knit image.
    length_steps, around = 34, 8
    vertices, faces, uv = [], [], []
    for i in range(length_steps):
        t = i / (length_steps - 1)
        y = -1.05 - 2.20 * t
        taper = .43 + .57 * min(1, t * 9, (1-t) * 9)
        cx = 1.68 + .012 * math.sin(t * math.pi * 1.4)
        cz = .354 + .006 * math.sin(t * math.pi)
        for j in range(around):
            angle = 2 * math.pi * j / around
            x = cx + .20 * taper * math.cos(angle)
            z = cz + .042 * taper * math.sin(angle)
            vertices.append((x, y, z))
            # Use the blue knit region of the approved image for the sleeve.
            uv.append((.285 + .025*math.cos(angle), .80 - .09*t))
    for i in range(length_steps - 1):
        for j in range(around):
            following = (j + 1) % around
            faces.append((i*around+j, i*around+following,
                          (i+1)*around+following, (i+1)*around+j))
    faces.append(tuple(reversed(tuple(range(around)))))
    faces.append(tuple((length_steps-1)*around+j for j in range(around)))
    data = bpy.data.meshes.new("FoldedSleeveReturn")
    data.from_pydata(vertices, [], faces)
    data.update(calc_edges=True)
    data.materials.append(material)
    layer = data.uv_layers.new(name="TopReferenceUV")
    for face in data.polygons:
        face.use_smooth = True
        for loop in face.loop_indices:
            vertex_index = data.loops[loop].vertex_index
            if face.index // around >= 27:
                # Loop-level UV seam makes the exposed cream cuff a distinct
                # knitted fold instead of stretching colors across the join.
                step = vertex_index // around
                angle = 2*math.pi*(vertex_index % around)/around
                layer.data[loop].uv = (.33 + .018*math.cos(angle),
                                       .44 + .025*(step-27)/6)
            else:
                layer.data[loop].uv = uv[vertex_index]
    obj = bpy.data.objects.new("M_Item_SweaterFolded_SleeveReturn", data)
    bpy.context.collection.objects.link(obj)
    return obj


def setup_camera(target, scale, azimuth, elevation, resolution):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new("Review world")
    world.color = (.24, .23, .22)
    scene.world = world
    light = bpy.data.lights.new("Softbox", 'AREA')
    light.energy, light.size = 240, 3
    lamp = bpy.data.objects.new("Softbox", light)
    scene.collection.objects.link(lamp)
    lamp.location = Vector(target) + Vector((-1.5, -1.2, 2.5))
    lamp.rotation_euler = (Vector(target)-lamp.location).to_track_quat('-Z', 'Y').to_euler()
    camera = bpy.data.objects.new("Review camera", bpy.data.cameras.new("Review camera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = scale
    a, e = math.radians(azimuth), math.radians(elevation)
    direction = Vector((math.sin(a)*math.cos(e), -math.cos(a)*math.cos(e), math.sin(e)))
    camera.location = Vector(target) + direction * 4
    camera.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    return camera


def render(name):
    bpy.context.scene.render.filepath = str(OUT / name)
    bpy.ops.render.render(write_still=True)


bpy.ops.wm.read_factory_settings(use_empty=True)
source_pieces = load_meshes(FOLDED_SOURCE, "M_Item_SweaterFolded")
body = next(obj for obj in source_pieces if obj.name.endswith("_Body"))
collar = next(obj for obj in source_pieces if obj.name.endswith("_Collar"))
front_material, underside_material = body.data.materials
bpy.data.objects.remove(body, do_unlink=True)
lower = cloth_layer("M_Item_SweaterFolded_LowerReturn",
                    (.05, 1.95, -3.95, -.06), 0, .188,
                    front_material, underside_material, False)
upper = cloth_layer("M_Item_SweaterFolded_UpperFold",
                    (.09, 1.91, -3.73, -.055), .145, .353,
                    front_material, underside_material, True)
seat_collar(collar)
sleeve = folded_sleeve(front_material)
pieces = [lower, upper, collar, sleeve]
total = sum(triangles(obj) for obj in pieces)
points = [obj.matrix_world @ vertex.co for obj in pieces for vertex in obj.data.vertices]
bounds = [[round(min(p[axis] for p in points), 5),
           round(max(p[axis] for p in points), 5)] for axis in range(3)]
assert 6000 <= total <= 8500, total
assert 0 <= bounds[0][0] and bounds[0][1] <= 2, bounds
assert -4 <= bounds[1][0] and bounds[1][1] <= 0, bounds
assert 0 <= bounds[2][0] and bounds[2][1] <= .45, bounds

# Save the review DCC source, then export transformed copies with the proven
# ZA-003 FBX axis mapping. The accepted runtime FBX is untouched.
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(FOLDED_DIR / "SweaterFolded_PhysicalReview.blend"))
bpy.ops.object.select_all(action='DESELECT')
axis_map = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0),
                   (0, 1, 0, 0), (0, 0, 0, 1))) @ Matrix.Rotation(math.radians(-90), 4, 'X')
copies = []
for obj in pieces:
    exported = obj.copy()
    exported.data = obj.data.copy()
    bpy.context.collection.objects.link(exported)
    exported.data.transform(axis_map)
    exported.select_set(True)
    copies.append(exported)
bpy.context.view_layer.objects.active = copies[0]
bpy.ops.export_scene.fbx(filepath=str(FOLDED_DIR / "SweaterFolded_PhysicalReview.fbx"),
                         use_selection=True, object_types={'MESH'}, bake_anim=False,
                         axis_forward='Y', axis_up='Z', global_scale=1,
                         path_mode='RELATIVE', embed_textures=False)
for obj in copies:
    bpy.data.objects.remove(obj, do_unlink=True)

camera = setup_camera((1, -2, .17), 4.75, 15, 72, (1100, 1100))
render("folded-gameplay-high.png")
target = Vector((1, -2, .18))
camera.location = target + Vector((3.5, -.25, 1.02))
camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.ortho_scale = 4.7
render("folded-side.png")

approved_open = load_meshes(OPEN_SOURCE, "M_Item_SweaterOpen")
for obj in approved_open:
    obj.location.x = -3.25
for obj in pieces:
    obj.location.x = .60
target = Vector((-.1, -1.9, .16))
camera.location = target + Vector((.85, -1.0, 4))
camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.ortho_scale = 10.6
bpy.context.scene.render.resolution_x = 1600
bpy.context.scene.render.resolution_y = 1000
render("open-folded-side-by-side.png")

materials = sorted({mat.name for obj in pieces for mat in obj.data.materials if mat})
textures = sorted({(node.image.name, tuple(node.image.size)) for mat in bpy.data.materials
                   if mat.use_nodes for node in mat.node_tree.nodes
                   if node.type == 'TEX_IMAGE' and node.image and mat.name in materials})
report = {"triangles": total, "meshes": {obj.name: triangles(obj) for obj in pieces},
          "materials": materials, "texture_images": textures, "bounds_blender_xyz": bounds,
          "source": str(FOLDED_DIR / "SweaterFolded_PhysicalReview.blend"),
          "review_fbx": str(FOLDED_DIR / "SweaterFolded_PhysicalReview.fbx")}
(OUT / "folded-stats.json").write_text(json.dumps(report, indent=2), encoding='utf-8')
print("FOLDED_PHYSICAL_REVIEW", json.dumps(report))

# Re-import the actual review FBX. All four authored pieces must be closed,
# textured, and within the established folded visual footprint.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FOLDED_DIR / "SweaterFolded_PhysicalReview.fbx"))
imported = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
assert len(imported) == 4 and sum(triangles(obj) for obj in imported) == total
for obj in imported:
    assert obj.data.uv_layers and obj.data.materials, obj.name
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    assert all(edge.is_manifold for edge in bm.edges), obj.name
    bm.free()
print("FOLDED_PHYSICAL_REIMPORT_PASS", total, "triangles, four closed meshes")
