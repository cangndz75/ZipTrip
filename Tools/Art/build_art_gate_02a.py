"""ART-GATE-02A review assets. Run with Blender 5.2 in background mode.

  blender -b --factory-startup -P Tools/Art/build_art_gate_02a.py

This writes only ArtSource review assets and Builds previews. It never edits the
accepted suitcase or Unity sweater assets.
"""

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Builds/art-gate-02a"
INTERIOR = ROOT / "ArtSource/CabinSuitcase/Interior"
SWEATERS = ROOT / "ArtSource/GoldenItems"
GOLDEN_FBX = ROOT / "Assets/Art/Models/Containers/CabinSuitcase/Models/CabinSuitcase_Golden.fbx"
ITEM_SOURCE = ROOT / "Assets/Art/Models/Items/Source"
for folder in (OUT, INTERIOR, SWEATERS / "SweaterOpen", SWEATERS / "SweaterFolded"):
    folder.mkdir(parents=True, exist_ok=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh(name, verts, faces, material, uvs=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update(calc_edges=True)
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(material)
    layer = data.uv_layers.new(name="FabricUV")
    for polygon in data.polygons:
        polygon.use_smooth = True
        for loop in polygon.loop_indices:
            v = data.vertices[data.loops[loop].vertex_index].co
            layer.data[loop].uv = uvs[data.loops[loop].vertex_index] if uvs else (
                (v.x + 0.36) / 0.72, (v.y + 0.45) / 0.90)
    return obj


def tris(obj):
    return sum(len(poly.vertices) - 2 for poly in obj.data.polygons)


def fabric_material():
    image = bpy.data.images.new("T_CabinInterior_Review_BaseColor", 1024, 1024)
    normal_image = bpy.data.images.new("T_CabinInterior_Review_Normal", 1024, 1024)
    normal_image.colorspace_settings.name = 'Non-Color'
    pixels = [0.0] * (1024 * 1024 * 4)
    normals = [0.0] * (1024 * 1024 * 4)
    for y in range(1024):
        for x in range(1024):
            u, v = x / 1023, y / 1023
            # Broad, slightly bowed fabric panels and two soft inset seams; no cell-sized repeat.
            bowed = u + 0.028 * math.sin(v * math.tau)
            loft = max(0, math.sin(v * math.pi)) ** 0.7
            panels = 0.034 * (0.35 + 0.65 * loft) * math.cos(bowed * math.tau * 2.2)
            seam = sum(math.exp(-((bowed - center) / 0.02) ** 2) for center in (0.34, 0.66))
            edge = min(u, 1-u, v, 1-v)
            cavity = math.exp(-((edge / 0.13) ** 2))
            inset = math.exp(-(((edge - 0.11) / 0.018) ** 2))
            shoulder = math.exp(-(((edge - 0.15) / 0.028) ** 2))
            # Readable weave at phone scale without a noisy high-frequency contrast field.
            warp = math.cos(x * math.pi / 3) * 0.008
            weft = math.cos(y * math.pi / 3) * 0.006
            drift = math.sin(x * 0.011 + y * 0.006) * 0.006
            d = panels + drift + warp + weft - seam * 0.026 - cavity * 0.037 - inset * 0.018 + shoulder * 0.009
            i = (y * 1024 + x) * 4
            pixels[i:i + 4] = (0.075 + d * 0.65, 0.225 + d, 0.235 + d, 1)
            # Tangent-space relief follows the wide cushion lobes and soft seams.
            slope = 0.62 * loft * math.sin(bowed * math.tau * 2.2)
            slope += sum(0.25 * (bowed - center) / 0.02
                         * math.exp(-((bowed - center) / 0.02) ** 2)
                         for center in (0.34, 0.66))
            nx = slope
            ny = 0.23 * math.cos(bowed * math.tau * 2.2) * math.cos(v * math.pi)
            length = math.sqrt(nx * nx + ny * ny + 1)
            normals[i:i + 4] = (0.5 + nx / length * 0.5,
                                0.5 + ny / length * 0.5, 0.5 + 0.5 / length, 1)
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(INTERIOR / "T_CabinInterior_Review_BaseColor.png")
    image.file_format = 'PNG'
    image.save()
    normal_image.pixels.foreach_set(normals)
    normal_image.filepath_raw = str(INTERIOR / "T_CabinInterior_Review_Normal.png")
    normal_image.file_format = 'PNG'
    normal_image.save()
    mat = bpy.data.materials.new("M_CabinInterior_Review")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.91
    bsdf.inputs["Metallic"].default_value = 0
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = 'Linear'
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    normal_tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    normal_tex.image = normal_image
    normal = mat.node_tree.nodes.new("ShaderNodeNormalMap")
    normal.inputs["Strength"].default_value = 0.8
    mat.node_tree.links.new(normal_tex.outputs["Color"], normal.inputs["Color"])
    mat.node_tree.links.new(normal.outputs["Normal"], bsdf.inputs["Normal"])
    bump = mat.node_tree.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.07
    bump.inputs["Distance"].default_value = 0.0007
    mat.node_tree.links.new(tex.outputs["Color"], bump.inputs["Height"])
    mat.node_tree.links.new(normal.outputs["Normal"], bump.inputs["Normal"])
    mat.node_tree.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def rounded_path(hx, hy, radius, steps=12):
    result = []
    for cx, cy, start in ((hx-radius, hy-radius, 0), (-hx+radius, hy-radius, 90),
                          (-hx+radius, -hy+radius, 180), (hx-radius, -hy+radius, 270)):
        for k in range(steps):
            a = math.radians(start + 90 * k / steps)
            result.append((cx + radius * math.cos(a), cy + radius * math.sin(a)))
    return result


def ring_mesh(name, profiles, material):
    verts, faces = [], []
    for hx, hy, radius, z in profiles:
        verts.extend((x, y, z) for x, y in rounded_path(hx, hy, radius))
    n = 48
    for r in range(len(profiles) - 1):
        for k in range(n):
            a, b = r*n+k, r*n+(k+1) % n
            faces.append((a, b, b+n, a+n))
    return mesh(name, verts, faces, material)


def build_interior():
    reset()
    material = fabric_material()
    root = bpy.data.objects.new("CabinInterior_Review", None)
    bpy.context.collection.objects.link(root)
    verts, faces = [], []
    nx, ny = 31, 39
    for j in range(ny):
        y = -.432 + .864 * j / (ny-1)
        for i in range(nx):
            x = -.347 + .694 * i / (nx-1)
            u, v = x/.347, y/.432
            edge = max(abs(u), abs(v))
            rise = max(0, min(1, (edge - .35) / .65))
            rise = rise * rise * (3 - 2 * rise)
            bowed = u + .085 * math.sin(v * math.pi)
            panel = .0024 * max(0, math.sin((v + 1) * math.pi / 2)) * math.cos(bowed * math.pi * 2.2)
            inset = sum(math.exp(-((bowed - center) / .11) ** 2) for center in (-.32, .32))
            z = .198 + .0035 * rise + panel - .0008 * inset
            verts.append((x, y, z))
    for j in range(ny-1):
        for i in range(nx-1):
            a = j*nx+i
            faces.append((a, a+1, a+1+nx, a+nx))
    floor = mesh("PaddedFloor", verts, faces, material)
    bolster = ring_mesh("SoftPerimeter", [
        (.310, .395, .027, .194), (.325, .409, .030, .202),
        (.335, .420, .033, .231), (.344, .431, .038, .242),
        (.350, .437, .040, .230), (.352, .439, .040, .189)], material)
    seams = ring_mesh("InsetStitchSeam", [
        (.310, .395, .027, .204), (.311, .396, .027, .2055),
        (.313, .398, .027, .204)], material)
    pieces = (floor, bolster, seams)
    for obj in pieces:
        obj.parent = root
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(INTERIOR / "CabinInterior_Review.blend"))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in pieces:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = floor
    bpy.ops.export_scene.fbx(filepath=str(INTERIOR / "CabinInterior_Review.fbx"),
                             use_selection=True, object_types={'MESH'}, bake_anim=False,
                             axis_forward='-Z', axis_up='Y', global_scale=1,
                             path_mode='RELATIVE', embed_textures=False)
    report = {obj.name: {"triangles": tris(obj), "vertices": len(obj.data.vertices)} for obj in pieces}
    report["total_triangles"] = sum(tris(obj) for obj in pieces)
    report["material_count"] = 1
    report["texture"] = "1024x1024 RGBA PNG basecolor and normal; high roughness; no transparency"
    assert report["total_triangles"] < 3000, report
    (OUT / "interior-stats.json").write_text(json.dumps(report, indent=2), encoding='utf-8')
    print("INTERIOR_REPORT", json.dumps(report))
    return pieces


def setup_render(target, distance, azimuth, elevation, resolution=(1000, 1000)):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new("Warm studio")
    world.color = (.24, .23, .22)
    scene.world = world
    key = bpy.data.lights.new("Large softbox", 'AREA')
    key.energy = 220
    key.shape = 'DISK'
    key.size = 3
    lamp = bpy.data.objects.new("Large softbox", key)
    scene.collection.objects.link(lamp)
    lamp.location = Vector(target) + Vector((-1.5, -1.2, 2.5))
    lamp.rotation_euler = (Vector(target)-lamp.location).to_track_quat('-Z','Y').to_euler()
    camera = bpy.data.objects.new("Review camera", bpy.data.cameras.new("Review camera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = distance
    a, e = math.radians(azimuth), math.radians(elevation)
    direction = Vector((math.sin(a)*math.cos(e), -math.cos(a)*math.cos(e), math.sin(e)))
    camera.location = Vector(target) + direction * 3
    camera.rotation_euler = (-direction).to_track_quat('-Z','Y').to_euler()
    scene.render.film_transparent = False
    return camera


def shoot(name):
    bpy.context.scene.render.filepath = str(OUT / name)
    bpy.ops.render.render(write_still=True)


def render_interior(pieces):
    camera = setup_render((0, 0, .205), 1.25, 30, 58)
    shoot("interior-alone.png")
    # Import the existing golden FBX only into the review scene.
    bpy.ops.import_scene.fbx(filepath=str(GOLDEN_FBX))
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH' and obj.name in ('Base', 'Lid', 'Interior'):
            for mat in obj.data.materials:
                if mat and mat.name.startswith('M_CabinSuitcase_Lining'):
                    mat.diffuse_color = (.035, .22, .23, 1)
    camera.data.ortho_scale = 1.4
    shoot("interior-in-golden-suitcase.png")
    camera.location = Vector((0, -.75, 2.8))
    camera.rotation_euler = (Vector((0, 0, .2))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale = 1.34
    shoot("interior-gameplay-angle.png")
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH' and obj not in pieces:
            obj.hide_render = True
    bpy.context.scene.render.engine = 'BLENDER_WORKBENCH'
    bpy.context.scene.display.shading.light = 'STUDIO'
    bpy.context.scene.display.shading.color_type = 'SINGLE'
    bpy.context.scene.display.shading.single_color = (.24, .55, .56)
    bpy.context.scene.display.shading.show_shadows = False
    for obj in pieces:
        wire = obj.modifiers.new("Review wire", 'WIREFRAME')
        wire.thickness = .00065
        wire.use_replace = True
    shoot("interior-wireframe.png")


def sweater(kind, proposed):
    reset()
    source = ITEM_SOURCE / f"M_Item_Sweater{kind}_Source.blend"
    with bpy.data.libraries.load(str(source), link=False) as (src, dst):
        dst.objects = [name for name in src.objects if name.startswith(f"M_Item_Sweater{kind}")]
    pieces = []
    for obj in dst.objects:
        if obj and obj.type == 'MESH':
            bpy.context.collection.objects.link(obj)
            pieces.append(obj)
    if not pieces:
        raise ValueError(f"No source mesh in {source}")
    before = sum(tris(obj) for obj in pieces)
    if proposed:
        target = 8816 if kind == 'Open' else 7900
        for obj in pieces:
            if kind == 'Folded' and 'Collar' in obj.name:
                continue  # Preserve the collar shape and its strong identity cue.
            if kind == 'Open':
                continue  # The accepted silhouette is worth its small budget excess.
            ratio = min(1, (target - (before - tris(obj))) / tris(obj))
            bpy.ops.object.select_all(action='DESELECT')
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            mod = obj.modifiers.new("Review retopo reduction", 'DECIMATE')
            mod.ratio = ratio
            bpy.ops.object.modifier_apply(modifier=mod.name)
        for mat in bpy.data.materials:
            if not mat.use_nodes:
                continue
            bsdf = mat.node_tree.nodes.get("Principled BSDF")
            if bsdf:
                bsdf.inputs["Roughness"].default_value = .9
                bsdf.inputs["Metallic"].default_value = 0
        folder = SWEATERS / f"Sweater{kind}"
        if kind == 'Open':
            for image in bpy.data.images:
                if image.name.startswith('M_Item_SweaterOpen_MeshyRetopo_8k_'):
                    image.scale(1024, 1024)
                    image.filepath_raw = str(folder / image.name.replace('_8k_', '_Review_'))
                    image.file_format = 'JPEG'
                    image.save()
        bpy.ops.file.pack_all()
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(folder / f"Sweater{kind}_Review.blend"))
        # Match the proven ZA-003 Blender-to-Unity mesh-axis conversion on
        # export-only copies. The saved review .blend remains flat in XY.
        bpy.ops.object.select_all(action='DESELECT')
        axis_map = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0),
                           (0, 1, 0, 0), (0, 0, 0, 1)))
        if kind == 'Folded':
            axis_map = axis_map @ Matrix.Rotation(math.radians(-90), 4, 'X')
        copies = []
        for obj in pieces:
            exported = obj.copy()
            exported.data = obj.data.copy()
            bpy.context.collection.objects.link(exported)
            exported.data.transform(axis_map)
            exported.select_set(True)
            copies.append(exported)
        bpy.context.view_layer.objects.active = copies[0]
        bpy.ops.export_scene.fbx(filepath=str(folder / f"Sweater{kind}_Review.fbx"),
                                 use_selection=True, object_types={'MESH'}, bake_anim=False,
                                 axis_forward='Y', axis_up='Z', global_scale=1,
                                 path_mode='RELATIVE', embed_textures=False)
        for obj in copies:
            bpy.data.objects.remove(obj, do_unlink=True)
    after = sum(tris(obj) for obj in pieces)
    mats = sorted({mat.name for obj in pieces for mat in obj.data.materials if mat})
    imgs = sorted({(node.image.name, tuple(node.image.size)) for mat in bpy.data.materials
                   if mat.use_nodes for node in mat.node_tree.nodes
                   if node.type == 'TEX_IMAGE' and node.image})
    target = (1.5, -1.5, .15) if kind == 'Open' else (1, -2, .15)
    distance = 4.2 if kind == 'Open' else 4.8
    camera = setup_render(target, distance, 15, 72)
    label = 'proposed' if proposed else 'existing'
    shoot(f"sweater-{kind.lower()}-{label}-high.png")
    camera.location = Vector(target) + Vector((1.3, -2.8, 1.25))
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    shoot(f"sweater-{kind.lower()}-{label}-side.png")
    report = {"state": kind, "version": label, "triangles": after,
              "source_triangles": before, "meshes": len(pieces),
              "materials": mats, "textures": imgs}
    (OUT / f"sweater-{kind.lower()}-{label}-stats.json").write_text(
        json.dumps(report, indent=2), encoding='utf-8')
    print("SWEATER_REPORT", json.dumps(report))


parts = build_interior()
if '--interior-only' not in sys.argv:
    render_interior(parts)
    for state in ('Open', 'Folded'):
        sweater(state, False)
        sweater(state, True)
