"""ZA-003 laptop pilot: blender -b --python M_Item_Laptop_Cleanup.py"""

import json
import sys
from math import radians
from pathlib import Path

import bpy
from mathutils import Matrix


items_dir = Path(__file__).resolve().parents[1]
raw_path = items_dir / "Raw" / "M_Item_Laptop_TripoRetopo_8k.glb"
blend_path = items_dir / "Source" / "M_Item_Laptop_Source.blend"


def export_unity_fbx():
    """Export the accepted Blender pilot without rewriting its source or GLB."""
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    meshes = [entry for entry in bpy.context.scene.objects if entry.type == "MESH"]
    if len(meshes) != 1 or any(entry.type != "MESH" for entry in bpy.context.scene.objects):
        raise ValueError("FBX export requires exactly one mesh and no other scene objects")
    obj = meshes[0]
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    bounds = [(min(point[axis] for point in points),
               max(point[axis] for point in points)) for axis in range(3)]
    if any(abs(actual - expected) > 1e-5 for actual, expected in zip(
            (bounds[0][0], bounds[0][1], bounds[1][0], bounds[1][1], bounds[2][0]),
            (0.0, 3.0, -4.0, 0.0, 0.0))):
        raise ValueError(f"Unexpected Blender footprint bounds: {bounds}")
    if any(abs(value) > 1e-6 for value in (*obj.location, *obj.rotation_euler)) or \
            any(abs(value - 1.0) > 1e-6 for value in obj.scale):
        raise ValueError("Laptop transforms must be applied before FBX export")
    if not obj.data.uv_layers or not obj.material_slots:
        raise ValueError("Laptop UVs or material slots are missing")

    textures_dir = items_dir.parents[1] / "Materials" / "Items" / "Laptop" / "Textures"
    textures_dir.mkdir(parents=True, exist_ok=True)
    texture_report = []
    for suffix in ("basecolor.jpg", "normal.jpg", "rm.png"):
        matches = [image for image in bpy.data.images
                   if image.name.endswith(suffix) and image.packed_file]
        if len(matches) != 1:
            raise ValueError(f"Expected exactly one packed source texture for {suffix}")
        image = matches[0]
        target = textures_dir / image.name
        target.write_bytes(image.packed_file.data)
        image.unpack(method="REMOVE")
        image.filepath = str(target)
        texture_report.append({"path": str(target), "resolution": list(image.size)})

    # The Unity FBX importer maps exported local (-X, Y, Z) to Unity (X, Y, Z).
    # Build an export-only copy so the saved Blender source remains untouched.
    export_obj = obj.copy()
    export_obj.data = obj.data.copy()
    bpy.context.collection.objects.link(export_obj)
    export_obj.data.transform(Matrix(((-1, 0, 0, 0),
                                      (0, 0, 1, 0),
                                      (0, 1, 0, 0),
                                      (0, 0, 0, 1))))
    bpy.ops.object.select_all(action="DESELECT")
    export_obj.select_set(True)
    bpy.context.view_layer.objects.active = export_obj
    fbx_path = items_dir / "Final" / "M_Item_Laptop.fbx"
    bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                             object_types={"MESH"}, bake_anim=False,
                             axis_forward="Y", axis_up="Z", global_scale=1.0,
                             apply_scale_options="FBX_SCALE_ALL",
                             use_mesh_modifiers=True, use_tspace=True,
                             path_mode="RELATIVE", embed_textures=False)
    obj.data.calc_loop_triangles()
    print("ZIPTRIP_FBX_REPORT_BEGIN")
    print(json.dumps({"fbx": str(fbx_path), "blender_bounds": bounds,
                      "triangles": len(obj.data.loop_triangles),
                      "materials": len(obj.material_slots),
                      "textures": texture_report}, indent=2))
    print("ZIPTRIP_FBX_REPORT_END")


if "--export-fbx" in sys.argv:
    export_unity_fbx()
    raise SystemExit(0)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(raw_path))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(meshes) != 1:
    raise ValueError(f"Expected one laptop mesh, found {len(meshes)}")

obj = meshes[0]
raw_world = obj.matrix_world.copy()
rotation = Matrix.Rotation(radians(90), 4, "Z")
rotated = [rotation @ raw_world @ vertex.co for vertex in obj.data.vertices]
minimum = [min(point[axis] for point in rotated) for axis in range(3)]
maximum = [max(point[axis] for point in rotated) for axis in range(3)]
width = maximum[0] - minimum[0]
depth = maximum[1] - minimum[1]
if width <= 0 or depth <= 0:
    raise ValueError("Laptop top projection has zero width or depth")

scale_x = 3.0 / width
scale_y = 4.0 / depth
scale = Matrix.Diagonal((scale_x, scale_y, 1.0, 1.0))
translation = Matrix.Translation((-minimum[0] * scale_x,
                                  -maximum[1] * scale_y, -minimum[2]))
obj.parent = None
obj.matrix_world = translation @ scale @ rotation @ raw_world
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
obj.name = "M_Item_Laptop"
obj.data.name = "M_Item_Laptop"

# The import root is an empty transform only; geometry, UVs, and materials stay intact.
for empty in [entry for entry in bpy.context.scene.objects if entry.type == "EMPTY"]:
    if not empty.children:
        bpy.data.objects.remove(empty, do_unlink=True)

# Transform application preserves the imported normals. Recalculate only if degenerate.
if any(face.normal.length < 0.5 for face in obj.data.polygons):
    import bmesh
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    mesh.normal_update()
    mesh.to_mesh(obj.data)
    mesh.free()

points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
bounds = [[min(point[axis] for point in points),
           max(point[axis] for point in points)] for axis in range(3)]
if abs(bounds[0][0]) > 1e-5 or abs(bounds[0][1] - 3.0) > 1e-5 or \
        abs(bounds[1][0] + 4.0) > 1e-5 or abs(bounds[1][1]) > 1e-5 or \
        abs(bounds[2][0]) > 1e-5:
    raise ValueError(f"Footprint bounds do not match the contract: {bounds}")

bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
obj.data.calc_loop_triangles()
print("ZIPTRIP_CLEANUP_REPORT_BEGIN")
print(json.dumps({"rotation_degrees_z": 90, "scale_x": scale_x,
                  "scale_y": scale_y, "bounds": bounds,
                  "triangles": len(obj.data.loop_triangles),
                  "materials": [slot.material.name for slot in obj.material_slots
                                if slot.material]}, indent=2))
print("ZIPTRIP_CLEANUP_REPORT_END")
