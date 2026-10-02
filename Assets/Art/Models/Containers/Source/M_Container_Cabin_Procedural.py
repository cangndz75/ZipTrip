"""Build the ZA-004 static Cabin suitcase in Blender 5.2.2.

Blender XY is the board plane; Unity imports it as XZ. The origin is the
top-left corner of the 6x8 Cabin bounding area at packing-floor height.
"""

import json
import math
import random
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "M_Container_Cabin_Source.blend"
FINAL = ROOT / "Final" / "M_Container_Cabin.fbx"
LINING_TEXTURE = ROOT.parents[1] / "Materials" / "Containers" / "Cabin_LiningGrain.png"

WIDTH = 6.0
DEPTH = 8.0
FLOOR_THICKNESS = 0.12
RIM_CLEARANCE = 0.12
RIM_THICKNESS = 0.30
RIM_HEIGHT = 0.25
RIM_CORNER_RADIUS = 0.52
RIM_BEVEL = 0.035
SHELL_OVERHANG = 0.18
SHELL_BOTTOM = -0.38
BLOCKER_RADIUS = 1.03
BLOCKER_HEIGHT = 0.19
BLOCKER_BEVEL = 0.04
CAMERA_PITCH_DEGREES = 75.0


def linear(channel):
    value = channel / 255.0
    return value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def material(name, color):
    rgb = tuple(linear(int(color[index:index + 2], 16)) for index in (0, 2, 4))
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1.0)
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*rgb, 1.0)
    shader.inputs["Roughness"].default_value = 0.82
    return result


def lining_texture(mat):
    """Deterministic, low-contrast fabric grain; no geometry on valid cells."""
    LINING_TEXTURE.parent.mkdir(parents=True, exist_ok=True)
    image = bpy.data.images.new("Cabin_LiningGrain", width=256, height=256)
    rng = random.Random(4004)
    pixels = []
    for _ in range(256 * 256):
        grain = rng.uniform(-2.0, 2.0)
        pixels.extend((linear(233 + grain), linear(229 + grain),
                       linear(221 + grain), 1.0))
    image.pixels[:] = pixels
    image.filepath_raw = str(LINING_TEXTURE)
    image.file_format = "PNG"
    image.save()
    nodes = mat.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    mat.node_tree.links.new(texture.outputs["Color"],
                            nodes.get("Principled BSDF").inputs["Base Color"])


def freeze(obj):
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    return obj


def cube(name, center, size, mat, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
        modifier.affect = "EDGES"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        weighted = obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
        bpy.ops.object.modifier_apply(modifier=weighted.name)
    return freeze(obj)


def rounded_outline(xmin, xmax, ymin, ymax, radius, steps=12):
    corners = ((xmax - radius, ymax - radius, 0),
               (xmin + radius, ymax - radius, 90),
               (xmin + radius, ymin + radius, 180),
               (xmax - radius, ymin + radius, 270))
    result = []
    for cx, cy, start in corners:
        for step in range(steps + 1):
            angle = math.radians(start + step * 90.0 / steps)
            result.append((cx + radius * math.cos(angle),
                           cy + radius * math.sin(angle)))
    return result


def ring(name, inner, outer, bottom, top, mat, bevel=0.0):
    count = len(inner)
    vertices = []
    for outline, elevation in ((inner, bottom), (outer, bottom),
                               (inner, top), (outer, top)):
        vertices.extend((x, y, elevation) for x, y in outline)
    faces = []
    for index in range(count):
        next_index = (index + 1) % count
        faces.extend(((index, next_index, count + next_index, count + index),
                      (2 * count + index, 3 * count + index,
                       3 * count + next_index, 2 * count + next_index),
                      (index, 2 * count + index,
                       2 * count + next_index, next_index),
                      (count + index, count + next_index,
                       3 * count + next_index, 3 * count + index)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    if bevel:
        modifier = obj.modifiers.new("Soft rim", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def corner_intrusion(name, center, start_angle, mat):
    """A curved structural quarter-disc joined to the shell at each corner."""
    cx, cy = center
    arc = [(cx + BLOCKER_RADIUS * math.cos(math.radians(start_angle + step * 90 / 24)),
            cy + BLOCKER_RADIUS * math.sin(math.radians(start_angle + step * 90 / 24)))
           for step in range(25)]
    vertices = [(cx, cy, -FLOOR_THICKNESS),
                *((x, y, -FLOOR_THICKNESS) for x, y in arc),
                (cx, cy, BLOCKER_HEIGHT),
                *((x, y, BLOCKER_HEIGHT) for x, y in arc)]
    half = len(arc) + 1
    faces = []
    for index in range(1, half - 1):
        faces.extend(((0, index + 1, index),
                      (half, half + index, half + index + 1)))
    for index in range(half):
        next_index = (index + 1) % half
        faces.append((index, next_index, half + next_index, half + index))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    modifier = obj.modifiers.new("Soft structural edge", "BEVEL")
    modifier.width = BLOCKER_BEVEL
    modifier.segments = 3
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def bounds(objects):
    points = [obj.matrix_world @ vertex.co
              for obj in objects for vertex in obj.data.vertices]
    return [[min(point[axis] for point in points),
             max(point[axis] for point in points)] for axis in range(3)]


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
shell = material("Cabin_DeepBlueGreen", "31515D")
lining = material("Cabin_WarmCream", "E9E5DD")
accent = material("Cabin_Terracotta", "C36F58")
lining_texture(lining)

floor = cube("Cabin_FlatLining", (WIDTH / 2, -DEPTH / 2,
                                  -FLOOR_THICKNESS / 2),
             (WIDTH, DEPTH, FLOOR_THICKNESS), lining)

inner = rounded_outline(-RIM_CLEARANCE, WIDTH + RIM_CLEARANCE,
                        -DEPTH - RIM_CLEARANCE, RIM_CLEARANCE,
                        RIM_CORNER_RADIUS - RIM_THICKNESS)
outer = rounded_outline(-RIM_CLEARANCE - RIM_THICKNESS,
                        WIDTH + RIM_CLEARANCE + RIM_THICKNESS,
                        -DEPTH - RIM_CLEARANCE - RIM_THICKNESS,
                        RIM_CLEARANCE + RIM_THICKNESS,
                        RIM_CORNER_RADIUS)
rim = ring("Cabin_RoundedOuterRim", inner, outer,
           -FLOOR_THICKNESS, RIM_HEIGHT, shell, RIM_BEVEL)

# The skirt adds luggage volume below and outside the fixed packing floor.
skirt_outer = rounded_outline(-RIM_CLEARANCE - RIM_THICKNESS - SHELL_OVERHANG,
                              WIDTH + RIM_CLEARANCE + RIM_THICKNESS + SHELL_OVERHANG,
                              -DEPTH - RIM_CLEARANCE - RIM_THICKNESS - SHELL_OVERHANG,
                              RIM_CLEARANCE + RIM_THICKNESS + SHELL_OVERHANG,
                              RIM_CORNER_RADIUS + SHELL_OVERHANG)
skirt = ring("Cabin_RoundedShellSkirt", outer, skirt_outer,
             SHELL_BOTTOM, RIM_HEIGHT - 0.08, shell, 0.055)

# This narrow stripe sits on the rim, entirely outside the 6x8 board.
stripe_outer = rounded_outline(-RIM_CLEARANCE - 0.14,
                               WIDTH + RIM_CLEARANCE + 0.14,
                               -DEPTH - RIM_CLEARANCE - 0.14,
                               RIM_CLEARANCE + 0.14,
                               RIM_CORNER_RADIUS - RIM_THICKNESS + 0.14)
stripe_inner = rounded_outline(-RIM_CLEARANCE - 0.11,
                               WIDTH + RIM_CLEARANCE + 0.11,
                               -DEPTH - RIM_CLEARANCE - 0.11,
                               RIM_CLEARANCE + 0.11,
                               RIM_CORNER_RADIUS - RIM_THICKNESS + 0.11)
zipper = ring("Cabin_SubtleZipperLine", stripe_inner, stripe_outer,
              RIM_HEIGHT + 0.001, RIM_HEIGHT + 0.004, accent)

blockers = []
for column, row, center, angle in (
        (0, 0, (-RIM_CLEARANCE, RIM_CLEARANCE), -90),
        (5, 0, (WIDTH + RIM_CLEARANCE, RIM_CLEARANCE), 180),
        (0, 7, (-RIM_CLEARANCE, -DEPTH - RIM_CLEARANCE), 0),
        (5, 7, (WIDTH + RIM_CLEARANCE, -DEPTH - RIM_CLEARANCE), 90)):
    blockers.append(corner_intrusion(
        f"Cabin_BlockedCorner_{column}_{row}", center, angle, shell))

# Short-edge grip stays completely outside the packing rectangle.
handle = [cube("Cabin_Handle_LeftMount", (2.36, 0.59, 0.055),
               (0.26, 0.36, 0.20), shell, 0.085),
          cube("Cabin_Handle_RightMount", (3.64, 0.59, 0.055),
               (0.26, 0.36, 0.20), shell, 0.085),
          cube("Cabin_Handle_Grip", (3.0, 0.81, 0.055),
               (1.55, 0.24, 0.20), shell, 0.105)]

objects = [floor, rim, skirt, zipper, *blockers, *handle]
triangles = sum(len(face.vertices) - 2
                for obj in objects for face in obj.data.polygons)
report = {"floor_bounds_blender": bounds([floor]),
          "rim_bounds_blender": bounds([rim]),
          "shell_bounds_blender": bounds([skirt]),
          "blocker_bounds_blender": [bounds([blocker]) for blocker in blockers],
          "exterior_bounds_blender": bounds(objects),
          "triangle_count": triangles,
          "mesh_object_count": len(objects),
          "rim_projected_clearance": RIM_CLEARANCE - RIM_HEIGHT *
                                     math.tan(math.radians(90 - CAMERA_PITCH_DEGREES)),
          "blocker_projected_clearance": 1.0 + RIM_CLEARANCE - BLOCKER_RADIUS - BLOCKER_HEIGHT *
                                         math.tan(math.radians(90 - CAMERA_PITCH_DEGREES)),
          "material_names": sorted({slot.material.name for obj in objects
                                    for slot in obj.material_slots})}
if report["rim_projected_clearance"] < 0 or \
        report["blocker_projected_clearance"] < 0:
    raise ValueError("75-degree rim or blocker projection covers valid cells")
floor_actual = bounds([floor])
if any(abs(actual - expected) > 1e-6
       for pair, target in zip(floor_actual,
                               ((0.0, 6.0), (-8.0, 0.0), (-0.12, 0.0)))
       for actual, expected in zip(pair, target)):
    raise ValueError("Floor no longer matches the exact Cabin bounds")
for blocker, (column, row) in zip(blockers, ((0, 0), (5, 0), (0, 7), (5, 7))):
    bb = bounds([blocker])
    if ((column == 0 and bb[0][1] > 1.0) or
            (column == 5 and bb[0][0] < 5.0) or
            (row == 0 and bb[1][0] < -1.0) or
            (row == 7 and bb[1][1] > -7.0)):
        raise ValueError(f"Corner intrusion entered a valid cell: {blocker.name}")

SOURCE.parent.mkdir(parents=True, exist_ok=True)
FINAL.parent.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))

# Apply the same Unity-facing axis conversion as accepted ZA-003 item FBXs.
bpy.ops.object.select_all(action="DESELECT")
for obj in objects:
    exported = obj.copy()
    exported.data = obj.data.copy()
    bpy.context.collection.objects.link(exported)
    exported.data.transform(Matrix(((-1, 0, 0, 0),
                                    (0, 0, 1, 0),
                                    (0, 1, 0, 0),
                                    (0, 0, 0, 1))) @
                            Matrix.Rotation(math.radians(-90), 4, "X"))
    exported.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(FINAL), use_selection=True,
                         object_types={"MESH"}, bake_anim=False,
                         axis_forward="Y", axis_up="Z", global_scale=1.0,
                         apply_scale_options="FBX_SCALE_ALL",
                         use_mesh_modifiers=True, use_tspace=True,
                         path_mode="RELATIVE", embed_textures=False)
print("ZIPTRIP_CABIN_REPORT_BEGIN")
print(json.dumps(report, indent=2))
print("ZIPTRIP_CABIN_REPORT_END")
