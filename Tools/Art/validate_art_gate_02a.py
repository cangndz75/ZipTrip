"""Re-import ART-GATE-02A review FBXs and verify their basic art contracts."""

import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
CASES = (
    ("interior", ROOT / "ArtSource/CabinSuitcase/Interior/CabinInterior_Review.fbx", 2208, 3),
    ("open", ROOT / "ArtSource/GoldenItems/SweaterOpen/SweaterOpen_Review.fbx", 8816, 1),
    ("folded", ROOT / "ArtSource/GoldenItems/SweaterFolded/SweaterFolded_Review.fbx", 7900, 2),
)
results = {}
for name, path, expected_tris, expected_meshes in CASES:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    total = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in objects)
    points = [obj.matrix_world @ vert.co for obj in objects for vert in obj.data.vertices]
    bounds = [[round(min(p[axis] for p in points), 5),
               round(max(p[axis] for p in points), 5)] for axis in range(3)]
    assert total == expected_tris, (name, total)
    assert len(objects) == expected_meshes, (name, len(objects))
    assert all(obj.data.uv_layers for obj in objects), name
    assert all(obj.data.materials for obj in objects), name
    if name == 'interior':
        # Imported Blender coordinates must remain inside the locked golden
        # InteriorMin / InteriorMax X/Y and below the existing rim height.
        assert bounds[0][0] >= -.355 and bounds[0][1] <= .355, bounds
        assert bounds[1][0] >= -.440 and bounds[1][1] <= .445, bounds
        assert bounds[2][0] >= .188 and bounds[2][1] <= .27832, bounds
    results[name] = {"triangles": total, "meshes": len(objects), "bounds_blender_xyz": bounds,
                     "material_slots": [len(obj.data.materials) for obj in objects]}
print("ART_GATE_02A_REIMPORT_PASS", json.dumps(results, indent=2))
