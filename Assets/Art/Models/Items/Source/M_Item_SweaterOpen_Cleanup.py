"""ZA-003 sweater-open pilot: Blender 5.2, headless, no destructive topology edits."""

import json
import sys
from math import radians
from pathlib import Path

import bpy
from mathutils import Matrix


items_dir = Path(__file__).resolve().parents[1]
raw_path = items_dir / "Raw" / "M_Item_SweaterOpen_MeshyRetopo_8k.glb"
blend_path = items_dir / "Source" / "M_Item_SweaterOpen_Source.blend"
fbx_path = items_dir / "Final" / "M_Item_SweaterOpen.fbx"
textures_dir = items_dir.parents[1] / "Materials" / "Items" / "SweaterOpen" / "Textures"
texture_names = {
    "Image_0": "M_Item_SweaterOpen_MeshyRetopo_8k_basecolor.jpg",
    "Image_1": "M_Item_SweaterOpen_MeshyRetopo_8k_rm.jpg",
    "Image_2": "M_Item_SweaterOpen_MeshyRetopo_8k_normal.jpg",
}
packing_thickness = 0.375


def mesh_bounds(obj):
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return [[min(point[axis] for point in points),
             max(point[axis] for point in points)] for axis in range(3)]


def export_fbx():
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    meshes = [entry for entry in bpy.context.scene.objects if entry.type == "MESH"]
    if len(meshes) != 1 or len(bpy.context.scene.objects) != 1:
        raise ValueError("Expected exactly one sweater mesh and no other scene objects")
    obj = meshes[0]
    bounds = mesh_bounds(obj)
    if bounds[0][0] < -1e-5 or bounds[0][1] > 3.00001 or \
            bounds[1][0] < -3.00001 or bounds[1][1] > 1e-5 or \
            abs(bounds[2][0]) > 1e-5 or \
            abs(bounds[2][1] - packing_thickness) > 1e-5:
        raise ValueError(f"Sweater visual exceeds its logical 3x3 footprint: {bounds}")
    if any(abs(value) > 1e-6 for value in (*obj.location, *obj.rotation_euler)) or \
            any(abs(value - 1.0) > 1e-6 for value in obj.scale):
        raise ValueError("Sweater transforms must be applied before FBX export")
    if len(obj.material_slots) != 1 or not obj.data.uv_layers:
        raise ValueError("Sweater material slot or UVs are missing")

    textures_dir.mkdir(parents=True, exist_ok=True)
    texture_report = []
    for source_name, filename in texture_names.items():
        image = bpy.data.images.get(filename)
        if image is None or image.packed_file is None:
            raise ValueError(f"Packed source texture missing: {filename}")
        target = textures_dir / filename
        target.write_bytes(image.packed_file.data)
        texture_report.append({"path": str(target), "resolution": list(image.size)})
        image.unpack(method="REMOVE")
        image.filepath = str(target)

    # Keep the Blender source unchanged; this export-only copy maps Blender
    # (X, Y, Z-up) to Unity (X, Z-up, Y-depth) through the verified FBX importer.
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
    bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                             object_types={"MESH"}, bake_anim=False,
                             axis_forward="Y", axis_up="Z", global_scale=1.0,
                             apply_scale_options="FBX_SCALE_ALL",
                             use_mesh_modifiers=True, use_tspace=True,
                             path_mode="RELATIVE", embed_textures=False)
    obj.data.calc_loop_triangles()
    print("ZIPTRIP_SWEATER_FBX_REPORT_BEGIN")
    print(json.dumps({"fbx": str(fbx_path), "blender_bounds": bounds,
                      "triangles": len(obj.data.loop_triangles),
                      "material_slots": len(obj.material_slots),
                      "textures": texture_report}, indent=2))
    print("ZIPTRIP_SWEATER_FBX_REPORT_END")


if "--export-fbx" in sys.argv:
    export_fbx()
    raise SystemExit(0)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(raw_path))
meshes = [entry for entry in bpy.context.scene.objects if entry.type == "MESH"]
if len(meshes) != 1 or len(bpy.context.scene.objects) != 1:
    raise ValueError("Expected exactly one raw retopo mesh")
obj = meshes[0]
rotation = Matrix.Rotation(radians(-90), 4, "X")
rotated = [rotation @ obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
minimum = [min(point[axis] for point in rotated) for axis in range(3)]
maximum = [max(point[axis] for point in rotated) for axis in range(3)]
width = maximum[0] - minimum[0]
depth = maximum[1] - minimum[1]
if width <= 0 or depth <= 0:
    raise ValueError("Sweater projection has zero width or depth")
uniform_scale = min(3.0 / width, 3.0 / depth)
scaled_width = width * uniform_scale
scaled_depth = depth * uniform_scale
left_margin = (3.0 - scaled_width) / 2.0
top_margin = (3.0 - scaled_depth) / 2.0
translation = Matrix.Translation((left_margin - minimum[0] * uniform_scale,
                                  -top_margin - maximum[1] * uniform_scale,
                                  -minimum[2] * uniform_scale))
obj.matrix_world = translation @ Matrix.Scale(uniform_scale, 4) @ rotation @ obj.matrix_world
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
obj.name = "M_Item_SweaterOpen"
obj.data.name = "M_Item_SweaterOpen"
pre_pose_bounds = mesh_bounds(obj)
thickness_before = pre_pose_bounds[2][1] - pre_pose_bounds[2][0]
if thickness_before <= 0:
    raise ValueError("Sweater has zero vertical thickness")
vertical_scale = packing_thickness / thickness_before
# Compress only the board-normal axis, preserving X/Y projection, UVs and faces.
# Keep the current mesh bottom on the board plane; the underside remains open.
obj.data.transform(Matrix.Translation((0, 0, -pre_pose_bounds[2][0] * vertical_scale)) @
                   Matrix.Diagonal((1.0, 1.0, vertical_scale, 1.0)))
obj.data.update()
bounds = mesh_bounds(obj)
if bounds[0][0] < -1e-5 or bounds[0][1] > 3.00001 or \
        bounds[1][0] < -3.00001 or bounds[1][1] > 1e-5 or \
        abs(bounds[2][0]) > 1e-5 or \
        abs(bounds[2][1] - packing_thickness) > 1e-5 or \
        any(abs(bounds[axis][edge] - pre_pose_bounds[axis][edge]) > 1e-6
            for axis in (0, 1) for edge in (0, 1)):
    raise ValueError(f"Sweater visual exceeds its logical 3x3 footprint: {bounds}")
if len(obj.material_slots) != 1 or not obj.data.uv_layers:
    raise ValueError("Sweater material slot or UVs are missing")
for source_name, filename in texture_names.items():
    image = bpy.data.images.get(source_name)
    if image is None or image.packed_file is None:
        raise ValueError(f"Packed source texture missing: {source_name}")
    image.name = filename
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
obj.data.calc_loop_triangles()
print("ZIPTRIP_SWEATER_CLEANUP_REPORT_BEGIN")
print(json.dumps({"rotation_x_degrees": -90, "pre_scale_width": width,
                  "pre_scale_depth": depth, "uniform_scale": uniform_scale,
                  "thickness_before": thickness_before,
                  "packing_thickness_target": packing_thickness,
                  "vertical_scale": vertical_scale,
                  "visual_width": scaled_width, "visual_depth": scaled_depth,
                  "left_right_margin": left_margin, "top_bottom_margin": top_margin,
                  "blender_bounds": bounds,
                  "triangles": len(obj.data.loop_triangles)}, indent=2))
print("ZIPTRIP_SWEATER_CLEANUP_REPORT_END")
