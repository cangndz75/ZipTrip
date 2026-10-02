"""Build the ZA-003 folded sweater from procedural cloth geometry in Blender 5.2."""

import hashlib
import json
import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix


items_dir = Path(__file__).resolve().parents[1]
source_path = items_dir / "Source" / "M_Item_SweaterFolded_Source.blend"
fbx_path = items_dir / "Final" / "M_Item_SweaterFolded.fbx"
reference_path = (items_dir.parents[1] / "Materials" / "Items" /
                  "SweaterFolded" / "Textures" / "SweaterFolded_LongFront.jpeg")
reference_hash = "c61773ca82c5a33a81518acec4fd9ff7aa030e3a94323e3d687f60a496f76980"
if hashlib.sha256(reference_path.read_bytes()).hexdigest() != reference_hash:
    raise ValueError("Approved long-form front reference has changed")


def solid_material(name, color, roughness=0.88):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0
    return material


def top_material(image):
    material = solid_material("SweaterFolded_LongFront", (1.0, 1.0, 1.0))
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    shader = nodes.get("Principled BSDF")
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    uv = nodes.new("ShaderNodeUVMap")
    uv.uv_map = "TopReferenceUV"
    links.new(uv.outputs["UV"], texture.inputs["Vector"])
    noise = nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 180.0
    noise.inputs["Detail"].default_value = 2.0
    links.new(uv.outputs["UV"], noise.inputs["Vector"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.08
    bump.inputs["Distance"].default_value = 0.006
    links.new(noise.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    return material


def contour():
    points = []
    xmin, xmax, ymin, ymax, radius = 0.05, 1.95, -3.95, -0.05, 0.20
    corners = ((xmax - radius, ymax - radius, 0.0),
               (xmin + radius, ymax - radius, 90.0),
               (xmin + radius, ymin + radius, 180.0),
               (xmax - radius, ymin + radius, 270.0))
    for center_x, center_y, start in corners:
        for step in range(48):
            angle = math.radians(start + 90.0 * step / 48)
            points.append((center_x + radius * math.cos(angle),
                           center_y + radius * math.sin(angle)))
    return points


def top_height(x, y):
    radial = max(abs((x - 1.0) / 0.95), abs((y + 2.0) / 1.95))
    soft_edge = 0.35 - 0.010 * radial * radial - 0.025 * radial ** 8
    fold_ridge = 0.012 * math.exp(-((y + 3.62) / 0.09) ** 2)
    return soft_edge + fold_ridge


def create_mesh(name, vertices, faces, materials, face_materials, uv_override=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for material in materials:
        mesh.materials.append(material)
    uv_layer = mesh.uv_layers.new(name="TopReferenceUV")
    for polygon, material_index in zip(mesh.polygons, face_materials):
        polygon.material_index = material_index
        polygon.use_smooth = True
        for loop_index in polygon.loop_indices:
            vertex = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            if uv_override is not None:
                uv_layer.data[loop_index].uv = uv_override[mesh.loops[loop_index].vertex_index]
            else:
                x_fraction = (vertex.x - 0.05) / 1.90
                y_fraction = (-vertex.y - 0.05) / 3.90
                uv_layer.data[loop_index].uv = (0.28 + 0.44 * x_fraction,
                                                0.66 - 0.52 * y_fraction)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    open_edges = sum(1 for edge in bm.edges if not edge.is_manifold)
    bm.free()
    if open_edges:
        raise ValueError(f"{name} has {open_edges} non-manifold/boundary edges")
    return obj


def body_mesh(materials):
    outline = contour()
    count = len(outline)
    vertices, faces, assignments = [], [], []

    def ring(scale, height=None):
        start = len(vertices)
        for x, y in outline:
            vx = 1.0 + (x - 1.0) * scale
            vy = -2.0 + (y + 2.0) * scale
            vz = top_height(vx, vy) if height is None else height
            vertices.append((vx, vy, vz))
        return start

    side_rings = [ring(0.88, 0.0), ring(0.96, 0.04), ring(1.0, 0.12),
                  ring(1.0, 0.24), ring(0.96, 0.31), ring(0.90)]
    for lower, upper in zip(side_rings, side_rings[1:]):
        for index in range(count):
            following = (index + 1) % count
            faces.append((lower + index, lower + following,
                          upper + following, upper + index))
            assignments.append(0)

    top_rings = [side_rings[-1]]
    for step in range(1, 16):
        top_rings.append(ring(0.90 * (1.0 - step / 16.0)))
    for outer, inner in zip(top_rings, top_rings[1:]):
        for index in range(count):
            following = (index + 1) % count
            faces.append((outer + index, outer + following,
                          inner + following, inner + index))
            assignments.append(0)
    top_center = len(vertices)
    vertices.append((1.0, -2.0, top_height(1.0, -2.0)))
    bottom_center = len(vertices)
    vertices.append((1.0, -2.0, 0.0))
    for index in range(count):
        following = (index + 1) % count
        faces.append((top_rings[-1] + index, top_rings[-1] + following, top_center))
        assignments.append(0)
        faces.append((bottom_center, side_rings[0] + following, side_rings[0] + index))
        assignments.append(1)
    return create_mesh("M_Item_SweaterFolded_Body", vertices, faces,
                       materials, assignments)


def collar_mesh(front_material):
    # A closed, shallow knitted oval. Its lower surface sinks into the body;
    # the neckline reveals the body's cream top instead of a hollow cavity.
    vertices, faces, assignments, uvs = [], [], [], []
    path_steps = 128
    section = ((0.47, 0.17, -0.012), (0.48, 0.18, 0.004),
               (0.53, 0.23, 0.012), (0.62, 0.30, 0.010),
               (0.66, 0.34, -0.010), (0.56, 0.26, -0.015))
    for step in range(path_steps):
        angle = 2 * math.pi * step / path_steps
        front_weight = (1.0 - math.sin(angle)) * 0.5
        for index, (radius_x, radius_y, height) in enumerate(section):
            x = 1.0 + radius_x * math.cos(angle)
            y = -0.45 + radius_y * math.sin(angle)
            raised_knit = 0.015 * front_weight if index in (2, 3) else 0.0
            vertices.append((x, y, top_height(x, y) + height + raised_knit))
            # Sample only the mustard knit band of the approved front image.
            # This region has no cream tips, so the closed oval has no caps.
            uvs.append((0.42 + 0.16 * step / path_steps,
                        0.72 - 0.025 * index / len(section)))
    for step in range(path_steps):
        following = (step + 1) % path_steps
        for index in range(len(section)):
            next_index = (index + 1) % len(section)
            faces.append((step * len(section) + index,
                          step * len(section) + next_index,
                          following * len(section) + next_index,
                          following * len(section) + index))
            assignments.append(0)
    return create_mesh("M_Item_SweaterFolded_Collar", vertices, faces,
                       (front_material,), assignments, uvs)


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
image = bpy.data.images.load(str(reference_path), check_existing=False)
image.name = "SweaterFolded_LongFront"
front_material = top_material(image)
teal = solid_material("SweaterFolded_DeepTeal", (0.025, 0.18, 0.23))
body = body_mesh((front_material, teal))
collar = collar_mesh(front_material)
bpy.ops.file.pack_all()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(source_path))

objects = (body, collar)
triangles = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons)
                for obj in objects)
points = [obj.matrix_world @ vertex.co for obj in objects for vertex in obj.data.vertices]
bounds = [[min(point[axis] for point in points), max(point[axis] for point in points)]
          for axis in range(3)]
if not 8000 <= triangles <= 15000:
    raise ValueError(f"Triangle budget missed: {triangles}")
if bounds[0][0] < 0 or bounds[0][1] > 2 or bounds[1][0] < -4 or \
        bounds[1][1] > 0 or abs(bounds[2][0]) > 1e-6 or bounds[2][1] > 0.45:
    raise ValueError(f"Bounds outside packing contract: {bounds}")

# Unity imports each FBX mesh child with a 270-degree X rotation. Compensate
# on export copies only; the saved source remains flat in Blender XY.
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
bpy.ops.export_scene.fbx(filepath=str(fbx_path), use_selection=True,
                         object_types={"MESH"}, bake_anim=False,
                         axis_forward="Y", axis_up="Z", global_scale=1.0,
                         apply_scale_options="FBX_SCALE_ALL",
                         use_mesh_modifiers=True, use_tspace=True,
                         path_mode="RELATIVE", embed_textures=False)
print("ZIPTRIP_FOLDED_PROCEDURAL_REPORT_BEGIN")
print(json.dumps({"source": str(source_path), "fbx": str(fbx_path),
                  "reference": str(reference_path), "triangles": triangles,
                  "body_triangles": sum(len(face.vertices) - 2 for face in body.data.polygons),
                  "collar_triangles": sum(len(face.vertices) - 2 for face in collar.data.polygons),
                  "bounds": bounds, "material_slots": [len(obj.material_slots) for obj in objects],
                  "uv_layers": [len(obj.data.uv_layers) for obj in objects]}, indent=2))
print("ZIPTRIP_FOLDED_PROCEDURAL_REPORT_END")
