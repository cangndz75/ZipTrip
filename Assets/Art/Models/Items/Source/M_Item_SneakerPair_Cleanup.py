"""Reproduce the ZA-003 sneaker-pair pilot from the Tripo retopo GLB."""

import bmesh
import bpy
import hashlib
import json
import math
from pathlib import Path

from mathutils import Matrix


items_dir = Path(__file__).resolve().parents[1]
raw_path = items_dir / "Raw" / "M_Item_SneakerPair_TripoRetopo_10k.glb"
blend_path = items_dir / "Source" / "M_Item_SneakerPair_Source.blend"
fbx_path = items_dir / "Final" / "M_Item_SneakerPair.fbx"
textures_dir = items_dir.parents[1] / "Materials" / "Items" / "SneakerPair" / "Textures"
expected_raw_hash = "bd105c1984cc6ec3b743925ca15d23737324764e745ec12196528133139b3509"
texture_names = {
    "colorful+sneakers+3d+model_basecolor.jpg": "M_Item_SneakerPair_TripoRetopo_10k_basecolor.jpg",
    "colorful+sneakers+3d+model_normal.jpg": "M_Item_SneakerPair_TripoRetopo_10k_normal.jpg",
    "colorful+sneakers+3d+model_rm.png": "M_Item_SneakerPair_TripoRetopo_10k_rm.png",
}
uniform_scale = 2.55
# Source connected-island roots identified by color-overlay review at 45/75 degrees.
# Detached edge/junction slivers only; heel panels, laces, tabs and sole pads
# remain. The face counts guard this reviewed selection against source changes.
artifact_island_faces = {5612: 15, 6560: 7, 7748: 12, 9124: 3,
                         15618: 1, 3732: 26, 13631: 10, 4839: 12,
                         3941: 17, 287: 6, 3858: 14, 15839: 4,
                         7831: 11, 752: 20, 1237: 14, 2947: 30,
                         6415: 8, 7663: 22, 4210: 21,
                         9551: 5, 7452: 4, 15455: 1,
                         2054: 47, 3326: 10, 11454: 8,
                         12149: 4, 13469: 4, 8074: 4}


def bounds(obj, transform=None):
    transform = transform or obj.matrix_world
    points = [transform @ vertex.co for vertex in obj.data.vertices]
    return [[min(point[axis] for point in points),
             max(point[axis] for point in points)] for axis in range(3)]


def split_shoes(source):
    mesh = source.data
    parent = list(range(len(mesh.vertices)))

    def find(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    for edge in mesh.edges:
        first, second = edge.vertices
        first_root, second_root = find(first), find(second)
        if first_root != second_root:
            parent[second_root] = first_root

    sums = {}
    for vertex in mesh.vertices:
        component = sums.setdefault(find(vertex.index), [0, 0.0, 0.0])
        component[0] += 1
        component[1] += vertex.co.x
        component[2] += vertex.co.z
    if len(sums) != 1530:
        raise ValueError(f"Retopo component count changed: {len(sums)}")
    face_counts = {root: 0 for root in artifact_island_faces}
    for face in mesh.polygons:
        root = find(face.vertices[0])
        if root in face_counts:
            face_counts[root] += 1
    if face_counts != artifact_island_faces:
        raise ValueError(f"Reviewed artifact islands changed: {face_counts}")
    labels = {root: ("Left" if total_x / count < -0.19 or total_z / count > 0.41
                     else "Right") for root, (count, total_x, total_z) in sums.items()}
    vertex_labels = [labels[find(vertex.index)] for vertex in mesh.vertices]
    output = {}
    for name in ("Left", "Right"):
        shoe = source.copy()
        shoe.data = mesh.copy()
        bpy.context.collection.objects.link(shoe)
        shoe.name = f"M_Item_SneakerPair_{name}"
        shoe.data.name = shoe.name
        split = bmesh.new()
        split.from_mesh(shoe.data)
        split.verts.ensure_lookup_table()
        split.verts.index_update()
        unwanted = [face for face in split.faces
                    if vertex_labels[face.verts[0].index] != name or
                    find(face.verts[0].index) in artifact_island_faces]
        bmesh.ops.delete(split, geom=unwanted, context="FACES")
        bmesh.ops.delete(split, geom=[vertex for vertex in split.verts
                                      if not vertex.link_faces], context="VERTS")
        split.to_mesh(shoe.data)
        split.free()
        shoe.data.update()
        output[name] = shoe
    if (len(output["Left"].data.polygons), len(output["Right"].data.polygons)) != (9611, 8350):
        raise ValueError("Shoe-island classification changed")
    bpy.data.objects.remove(source, do_unlink=True)
    return output


def place(shoe, rotation, x_min=None, x_max=None, y_max=None):
    before = bounds(shoe, rotation)
    x_translation = (x_min - before[0][0] * uniform_scale if x_min is not None
                     else x_max - before[0][1] * uniform_scale)
    translation = (x_translation, y_max - before[1][1] * uniform_scale,
                   -before[2][0] * uniform_scale)
    shoe.data.transform(Matrix.Translation(translation) @
                        Matrix.Scale(uniform_scale, 4) @ rotation)
    shoe.data.update()
    return {"rotation_x_degrees": -55 if shoe.name.endswith("Left") else 0,
            "translation": translation, "bounds": bounds(shoe)}


if hashlib.sha256(raw_path.read_bytes()).hexdigest() != expected_raw_hash:
    raise ValueError("Tripo retopo input has changed")
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(raw_path))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(meshes) != 1 or len(meshes[0].data.polygons) != 18301:
    raise ValueError("Unexpected retopo mesh")
source = meshes[0]
for obj in list(bpy.context.scene.objects):
    if obj != source:
        bpy.data.objects.remove(obj, do_unlink=True)
shoes = split_shoes(source)
poses = {
    "Left": place(shoes["Left"], Matrix.Rotation(math.radians(-55), 4, "X"),
                  x_min=0.06, y_max=-0.06),
    "Right": place(shoes["Right"], Matrix.Identity(4), x_max=1.96, y_max=-2.01),
}
left, right = poses["Left"]["bounds"], poses["Right"]["bounds"]
for shoe_bounds in (left, right):
    if (shoe_bounds[0][0] < -1e-5 or shoe_bounds[0][1] > 2.00001 or
            shoe_bounds[1][0] < -3.00001 or shoe_bounds[1][1] > 1e-5 or
            abs(shoe_bounds[2][0]) > 1e-5):
        raise ValueError(f"Shoe exceeds the board or floats: {shoe_bounds}")
if left[0][1] >= 1.0 or right[1][1] >= -2.0 or left[1][0] >= -2.0 or \
        right[0][0] >= 1.0 or right[0][1] <= 1.0:
    raise ValueError("Sneaker L-footprint or empty cells are invalid")
if any(len(shoe.material_slots) != 1 or not shoe.data.uv_layers
       for shoe in shoes.values()):
    raise ValueError("Source material slot or UVs were lost")

for source_name, filename in texture_names.items():
    image = bpy.data.images.get(source_name)
    if image is None or image.packed_file is None:
        raise ValueError(f"Packed source texture missing: {source_name}")
    image.name = filename
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

textures_dir.mkdir(parents=True, exist_ok=True)
textures = []
for filename in texture_names.values():
    image = bpy.data.images[filename]
    target = textures_dir / filename
    target.write_bytes(image.packed_file.data)
    textures.append({"path": str(target), "resolution": list(image.size)})
    image.unpack(method="REMOVE")
    image.filepath = str(target)

# Export copies use the established Blender-to-Unity axis mapping. The saved
# source remains in Blender XY with applied transforms and an origin at (0,0,0).
bpy.ops.object.select_all(action="DESELECT")
for shoe in shoes.values():
    export = shoe.copy()
    export.data = shoe.data.copy()
    bpy.context.collection.objects.link(export)
    export.data.transform(Matrix(((-1, 0, 0, 0),
                                  (0, 0, 1, 0),
                                  (0, 1, 0, 0),
                                  (0, 0, 0, 1))) @
                          Matrix.Rotation(math.radians(-90), 4, "X"))
    export.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                         object_types={"MESH"}, bake_anim=False,
                         axis_forward="Y", axis_up="Z", global_scale=1.0,
                         apply_scale_options="FBX_SCALE_ALL",
                         use_mesh_modifiers=True, use_tspace=True,
                         path_mode="RELATIVE", embed_textures=False)

print("ZIPTRIP_SNEAKER_REPORT_BEGIN")
print(json.dumps({"source": str(blend_path), "fbx": str(fbx_path),
                  "uniform_scale": uniform_scale, "poses": poses,
                  "removed_source_island_faces": artifact_island_faces,
                  "triangles": {name: sum(len(face.vertices) - 2
                                          for face in shoe.data.polygons)
                                for name, shoe in shoes.items()},
                  "textures": textures}, indent=2))
print("ZIPTRIP_SNEAKER_REPORT_END")
