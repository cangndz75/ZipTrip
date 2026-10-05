"""Inspect and prepare the six ART-01 GLBs with the established Blender-to-FBX path.

Run with Blender 5.2: blender -b -P Tools/Art/prepare_art01.py -- [--export]
The source GLBs are read only. Without --export this prints source measurements.
Runtime-only corrections (never written back to the GLBs): simple items are decimated to the
ADR-0009 ~5K class, and the Passport cover emblem (ADR-0009: no emblems) is removed from the
runtime textures and its embossed geometry is replaced by a plain cover fill.
"""

import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Art/Items/Golden"
MODELS = ROOT / "Assets/Art/Models/Items/Art01"
TEXTURES = ROOT / "Assets/Art/Materials/Items/Art01"
TEXTURE_SIZE = 1024
# One normalization rule for every item: uniform scale until the visual bounds touch the
# footprint inset by FOOTPRINT_MARGIN cells per side (mirrored by Art01AssetBuilder.Margin).
FOOTPRINT_MARGIN = 0.05
# Documented exception: in the Source Tray the Shampoo sits beside the Boarding Pass backdrop prop, and the
# BACKDROP-01 screen-clearance contract needs it narrower than the shared margin allows.
MARGIN_EXCEPTIONS = {"Shampoo": 0.14}
HEIGHT_LIMIT = 0.50
SIMPLE_TRIANGLES = 5000
SIMPLE_ITEMS = {"Passport", "Shampoo", "Towel"}
# Source proportions are reported for visual review, but are not an export gate. ART-01 intentionally
# uses the existing canonical GLBs and fits them to gameplay with uniform scaling only.
# Passport cover globe in source units (x across, z along the cover); both covers carry it.
EMBLEM_CENTRE = (0.03, 0.57)
EMBLEM_RADIUS = 0.24
ITEMS = (
    ("SweaterOpen", "sweater", 3, 3),
    ("Passport", "passport", 1, 2),
    ("Towel", "towel", 1, 4),
    ("Shampoo", "shampoo", 1, 3),
    ("Sunglasses", "sunglasses", 2, 1),
    ("TravelPouch", "organizer", 2, 3),
)


def image_for(material, suffix):
    images = [node.image for node in material.node_tree.nodes
              if node.type == "TEX_IMAGE" and node.image and node.image.name.endswith(suffix)]
    if len(images) != 1:
        raise RuntimeError(f"{material.name}: expected one {suffix} image, got {len(images)}")
    return images[0]


def pixels_of(image):
    image.scale(TEXTURE_SIZE, TEXTURE_SIZE)
    pixels = np.empty(TEXTURE_SIZE * TEXTURE_SIZE * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return pixels.reshape(TEXTURE_SIZE, TEXTURE_SIZE, 4)


def save_image(image, pixels, destination, file_format):
    image.pixels.foreach_set(pixels.ravel())
    image.file_format = file_format
    image.filepath_raw = str(destination)
    image.save()


def box_blur(values, radius):
    for axis in (0, 1):
        padded = np.concatenate([np.repeat(values.take([0], axis), radius + 1, axis), values,
                                 np.repeat(values.take([-1], axis), radius, axis)], axis)
        total = np.cumsum(padded, axis)
        values = (total.take(range(2 * radius + 1, total.shape[axis]), axis)
                  - total.take(range(0, total.shape[axis] - 2 * radius - 1), axis)) / (2 * radius + 1)
    return values


def is_gold(base):
    rgb = base[..., :3]
    high, low = rgb.max(-1), rgb.min(-1)
    return (rgb[..., 0] > rgb[..., 2]) & (high > 0.3) & ((high - low) > 0.5 * high)


def is_navy(base):
    rgb = base[..., :3]
    return (rgb[..., 2] > rgb[..., 0] + 0.05) & (rgb.max(-1) < 0.45)


def cover_region(mesh, inner, outer, covers_only=True):
    """Texels of cover faces whose centre lies in the inner..outer ring around the emblem."""
    region = np.zeros((TEXTURE_SIZE, TEXTURE_SIZE), bool)
    uv = mesh.data.uv_layers.active.data
    world = mesh.matrix_world
    mesh.data.calc_loop_triangles()
    for triangle in mesh.data.loop_triangles:
        centre = world @ triangle.center
        normal = (world.to_3x3() @ triangle.normal).normalized()
        distance = ((centre.x - EMBLEM_CENTRE[0]) ** 2 + (centre.z - EMBLEM_CENTRE[1]) ** 2) ** 0.5
        if (covers_only and abs(normal.y) < 0.6) or not inner <= distance < outer:
            continue
        points = np.array([uv[loop].uv[:] for loop in triangle.loops]) * TEXTURE_SIZE
        low = np.clip(np.floor(points.min(0)).astype(int), 0, TEXTURE_SIZE - 1)
        high = np.clip(np.ceil(points.max(0)).astype(int), 0, TEXTURE_SIZE - 1)
        xs, ys = np.meshgrid(np.arange(low[0], high[0] + 1) + 0.5, np.arange(low[1], high[1] + 1) + 0.5)
        a, b, c = points
        area = (b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])
        if abs(area) < 1e-9:
            continue
        w1 = ((xs - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (ys - a[1])) / area
        w2 = ((b[0] - a[0]) * (ys - a[1]) - (xs - a[0]) * (b[1] - a[1])) / area
        inside = (w1 >= -0.02) & (w2 >= -0.02) & (w1 + w2 <= 1.02)
        region[ys[inside].astype(int), xs[inside].astype(int)] = True
    return region


def rebuild_emblem(mesh, navy_uv):
    """Replace the embossed emblem geometry on both covers with a clean fill mapped to plain navy cover."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    uv = bm.loops.layers.uv.active
    # Tripo splits vertices along UV seams; weld them (UVs live on loops) so the only boundary is the cut hole.
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    half = 0.5 * max(abs(vertex.co.y) for vertex in bm.verts)
    doomed = [face for face in bm.faces
              if abs(face.calc_center_median().y) > half
              and ((face.calc_center_median().x - EMBLEM_CENTRE[0]) ** 2
                   + (face.calc_center_median().z - EMBLEM_CENTRE[1]) ** 2) < (0.9 * EMBLEM_RADIUS) ** 2]
    bmesh.ops.delete(bm, geom=doomed, context="FACES_ONLY")
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    near = lambda edge: all((vertex.co.x - EMBLEM_CENTRE[0]) ** 2 + (vertex.co.z - EMBLEM_CENTRE[1]) ** 2
                            < (1.2 * EMBLEM_RADIUS) ** 2 and abs(vertex.co.y) > half for vertex in edge.verts)
    boundary = [edge for edge in bm.edges if edge.is_boundary and near(edge)]
    filled = bmesh.ops.holes_fill(bm, edges=boundary, sides=0)["faces"]
    leftover = [edge for edge in boundary if edge.is_valid and edge.is_boundary]
    if leftover:  # holes_fill skips loops that touch themselves; triangle_fill takes any edge set.
        filled += bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=leftover)["geom"]
        filled = [face for face in filled if isinstance(face, bmesh.types.BMFace)]
    bmesh.ops.recalc_face_normals(bm, faces=filled)
    filled = bmesh.ops.triangulate(bm, faces=filled, quad_method="BEAUTY", ngon_method="BEAUTY")["faces"]
    open_edges = [edge for edge in bm.edges if edge.is_boundary and near(edge)]
    sides = {np.sign(face.calc_center_median().y) for face in filled}
    if open_edges or sides != {1.0, -1.0}:
        raise RuntimeError(f"Passport emblem fill incomplete: open edges={len(open_edges)} sides={sides}")
    for face in filled:
        face.smooth = True
        for loop in face.loops:
            loop[uv].uv = navy_uv
    bm.to_mesh(mesh.data)
    bm.free()
    # The source custom normals carry the emblem's shading and do not cover the new faces.
    with bpy.context.temp_override(object=mesh, active_object=mesh):
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    for polygon in mesh.data.polygons:
        polygon.use_smooth = True
    print(f"[ART-01] Passport: emblem geometry replaced: removed {len(doomed)} faces, "
          f"boundary edges {len(boundary)}, fill faces {len(filled)}")
    if len(boundary) == 0 or len(filled) == 0:
        raise RuntimeError("Passport emblem geometry was not replaced")


def remove_emblem(mesh, layers):
    """Repaint every emblem-disc texel (+ UV gutter) from plain navy cover texels around it."""
    disc = cover_region(mesh, 0.0, EMBLEM_RADIUS).astype(np.float32)
    gutter = (box_blur(disc, 10) > 0) & ~cover_region(mesh, 0.0, np.inf, covers_only=False)
    emblem = (box_blur(disc, 3) > 0) | gutter
    known = (cover_region(mesh, EMBLEM_RADIUS, 1.5 * EMBLEM_RADIUS) & is_navy(layers[0]) & ~emblem)
    known = known.astype(np.float32)[..., None]
    for index, layer in enumerate(layers):
        filled = layer.copy()
        missing = emblem.copy()
        for radius in (16, 32, 64, 128):
            weight = box_blur(known, radius)
            estimate = box_blur(layer * known, radius) / np.maximum(weight, 1e-6)
            take = missing & (weight[..., 0] > 1e-3)
            filled[take] = estimate[take]
            missing &= ~take
        filled[missing] = np.median(layer[known[..., 0] > 0], 0)
        layers[index] = filled
    remaining = (emblem & is_gold(layers[0])).sum()
    print(f"[ART-01] Passport: emblem texels repainted={emblem.sum()} navy samples={int(known.sum())} "
          f"gold remaining={remaining}")
    if remaining:
        raise RuntimeError("Passport emblem still present after cleanup")
    # A navy texel well inside the plain cover samples, used as the flat UV for the rebuilt emblem area.
    interior = box_blur(known, 4)[..., 0] > 0.999
    rows, columns = np.nonzero(interior)
    pick = np.argmin((rows - rows.mean()) ** 2 + (columns - columns.mean()) ** 2)
    return ((columns[pick] + 0.5) / TEXTURE_SIZE, (rows[pick] + 0.5) / TEXTURE_SIZE)


def export_textures(label, material, mesh):
    TEXTURES.mkdir(parents=True, exist_ok=True)
    base = image_for(material, "_basecolor.jpg")
    normal = image_for(material, "_normal.png")
    packed = image_for(material, "_rm.jpg")
    layers = [pixels_of(base), pixels_of(normal), pixels_of(packed)]
    navy_uv = remove_emblem(mesh, layers) if label == "Passport" else None
    save_image(base, layers[0], TEXTURES / f"T_Item_{label}_BaseColor.jpg", "JPEG")
    save_image(normal, layers[1], TEXTURES / f"T_Item_{label}_Normal.png", "PNG")
    pixels = layers[2].reshape(-1, 4)
    # glTF G=roughness, B=metallic; URP Lit R=metallic, A=smoothness.
    converted = np.zeros_like(pixels)
    converted[:, 0] = pixels[:, 2]
    converted[:, 3] = 1.0 - pixels[:, 1]
    metallic = bpy.data.images.new(f"T_Item_{label}_MetallicSmoothness", TEXTURE_SIZE,
                                   TEXTURE_SIZE, alpha=True)
    metallic.pixels.foreach_set(converted.ravel())
    metallic.file_format = "PNG"
    metallic.filepath_raw = str(TEXTURES / f"T_Item_{label}_MetallicSmoothness.png")
    metallic.save()
    return navy_uv


def inspect(label, source, width, depth, sideways, export):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"{label}: no mesh in {source}")
    bounds = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[i] for point in bounds) for i in range(3)))
    maximum = Vector(tuple(max(point[i] for point in bounds) for i in range(3)))
    triangles = sum(sum(len(face.vertices) - 2 for face in obj.data.polygons) for obj in meshes)
    print(f"[ART-01] {label}: source={source.name} meshes={len(meshes)} tris={triangles} "
          f"bounds={tuple(round(v, 5) for v in minimum)}..{tuple(round(v, 5) for v in maximum)} "
          f"images={[(image.name, tuple(image.size), image.file_format) for image in bpy.data.images]}")
    for material in bpy.data.materials:
        if material.users:
            print(f"[ART-01] {label}: material={material.name} "
                  f"metallic={material.diffuse_color[:3]} nodes="
                  f"{[(node.type, node.name) for node in material.node_tree.nodes] if material.node_tree else []}")
    source_width = maximum.z - minimum.z if sideways else maximum.x - minimum.x
    source_depth = maximum.x - minimum.x if sideways else maximum.z - minimum.z
    margin = MARGIN_EXCEPTIONS.get(label, FOOTPRINT_MARGIN)
    scale = min((width - 2 * margin) / source_width, (depth - 2 * margin) / source_depth)
    fill = (source_width * scale / (width - 2 * margin), source_depth * scale / (depth - 2 * margin))
    print(f"[ART-01] {label}: source aspect fill of inset footprint X={fill[0]:.0%} Z={fill[1]:.0%}")
    if not export:
        return
    if len(meshes) != 1 or len([material for material in bpy.data.materials if material.users]) != 1:
        raise RuntimeError(f"{label}: expected one mesh and one material")
    thickness = maximum.y - minimum.y
    thickness_factor = min(1.0, HEIGHT_LIMIT / (thickness * scale))
    if thickness_factor < 1.0:
        middle = (minimum.y + maximum.y) * 0.5
        for vertex in meshes[0].data.vertices:
            vertex.co.y = middle + (vertex.co.y - middle) * thickness_factor
        meshes[0].data.update()
    print(f"[ART-01] {label}: projected uniform scale={scale:.5f} "
          f"visual thickness cleanup={thickness_factor:.5f} "
          f"result height={thickness * thickness_factor * scale:.5f}")
    material = next(material for material in bpy.data.materials if material.users)
    navy_uv = export_textures(label, material, meshes[0])
    if label == "Passport":
        if not np.allclose(np.array(meshes[0].matrix_world), np.eye(4)):
            raise RuntimeError("Passport: emblem cleanup expects an identity object transform")
        rebuild_emblem(meshes[0], navy_uv)
    if label in SIMPLE_ITEMS:
        decimate = meshes[0].modifiers.new("ADR-0009 simple budget", "DECIMATE")
        decimate.decimate_type = "COLLAPSE"
        current = sum(len(face.vertices) - 2 for face in meshes[0].data.polygons)
        decimate.ratio = min(1.0, SIMPLE_TRIANGLES / current)
        decimate.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    runtime = sum(len(face.vertices) - 2 for face in meshes[0].data.polygons)
    print(f"[ART-01] {label}: runtime triangles {triangles} -> {runtime}")
    MODELS.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    meshes[0].select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    output = MODELS / f"M_Item_{label}_Art01.fbx"
    bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False,
                             add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE",
                             embed_textures=False, use_custom_props=False)
    print(f"[ART-01] {label}: wrote {output} and 3 textures at {TEXTURE_SIZE}px")


export = "--export" in sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else False
for name, filename, width, depth in ITEMS:
    source = SOURCE / f"zt_lv1_{filename}.glb"
    if source.exists():
        inspect(name, source, width, depth, name == "Towel", export)
    else:
        print(f"[ART-01] {name}: MISSING {source}")
