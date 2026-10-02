# ZA-003 — Laptop image-to-3D pilot (human execution)

Status: **laptop candidate human accepted; full ZA-003 ticket completed after all four assets were accepted**. This pilot stopped before sneaker, sweater, prefabs, or gameplay integration.

## Contract and files

- Input: `Assets/Art/Items/Golden/item_laptop.png` only. Keep the approved transparent reference unchanged.
- Canonical footprint: `XXX / XXX / XXX / XXX`, width 3 cells, depth 4 cells, area 12.
- Scale: 1 logical cell = 1 Unity world unit. The final top projection target is 3 × 4 Unity units; the mesh adapts to the gameplay footprint.
- Raw archive: `Assets/Art/Models/Items/Raw/M_Item_Laptop_TripoRaw.glb`; retopo source: `Assets/Art/Models/Items/Raw/M_Item_Laptop_TripoRetopo_8k.glb`.
- Blender working source: `Assets/Art/Models/Items/Source/M_Item_Laptop_Source.blend`.
- Unity-facing accepted export: `Assets/Art/Models/Items/Final/M_Item_Laptop.fbx`. The experimental final GLB was removed; the two raw GLBs and `.blend` remain the reproducible sources. Unity recognizes GLB as `DefaultAsset` because the project has no GLB importer; adding a package is unnecessary for Phase A.
- Source texture maps extracted unchanged from the packed Blender source live in `Assets/Art/Materials/Items/Laptop/Textures/`. Their Unity material channel assignment remains subject to visual review. Do not create a prefab here.
- Reproduce the Unity-facing export with `blender -b --python Assets/Art/Models/Items/Source/M_Item_Laptop_Cleanup.py -- --export-fbx`. This reads the existing `.blend` without saving it. Unity imports one material slot but does not automatically bind its base-color texture; the import pilot uses a temporary test material. No production URP channel conversion has been selected.

## 1. Original Meshy preparation (superseded for this accepted Tripo laptop)

1. Before uploading, verify the Meshy account is on an **active paid plan at generation time**. If it is free or cannot be verified, stop. Keep the task/model **Private** and do not publish to Meshy Community. Use only the approved laptop PNG; do not upload third-party references.
2. Open Meshy Workspace → Model → Image to 3D and upload `item_laptop.png` as the single reference. The PNG already has a transparent background; do not alter the source. Do not add Multi-View images that do not exist.
3. Pilot settings: **Smart Topology**, **Meshy 7** if available, **Private** license; Multi-View off, Pose off, Auto Split off, Image Enhancement off for this clear single-subject image. Record the actual version and any setting the account/UI cannot provide. These are pilot choices, not a claim that Meshy can reproduce the footprint exactly.
4. Generate and inspect the result from above and all sides. Check the closed-laptop silhouette, teal/coral/mustard/cream identity, stray geometry, visible material divisions, and whether the object can be oriented into a 3 × 4 rectangle. If a candidate fails, record the retry and reason; keep every production candidate private. Do not silently substitute Tripo.
5. Meshy may initially return an uncolored mesh. Use its Texture step if needed to carry the approved color/material identity. Inspect the textured result before download. Avoid unnecessary remesh that erases the silhouette; Blender cleanup follows.
6. On the selected finished result, use Download → **GLB**. Archive the unmodified download at the Raw path above. Do not overwrite it during cleanup. Record Meshy task/model ID, generation date, plan, actual settings, privacy state, and generation time in `asset-provenance.md`.

Meshy documents the [Image to 3D controls](https://help.meshy.ai/en/articles/9996860-how-to-use-meshy-image-to-3d), [GLB download with embedded textures](https://help.meshy.ai/en/articles/15724161-how-to-export-meshy-models-with-colors-and-textures), and [paid/private ownership conditions](https://help.meshy.ai/en/articles/10137554-what-is-the-ownership-of-the-generated-models). Its [Terms of Service](https://www.meshy.ai/terms-of-use) govern the actual generation; verify the terms again on that date. This preparation does not verify the human account or grant rights by itself.

## 2. Clean in Blender

1. Import the archived GLB into Blender via File → Import → glTF 2.0. Work on a separate `.blend`; keep import scale at 1 initially and inspect orientation, UVs, and textures. Save the working file at the Source path above.
2. Orient the **closed laptop** for a top-down gameplay view. The gameplay plane is Unity X/Z, with +X to the right and -Z down the rows. In Blender's Z-up workspace, use X/Y for the top projection: +X to the right, -Y down the rows. Do not use the angled PNG projection as the mesh's final floor orientation.
3. Remove stray pieces, obvious generation artifacts, and hidden/internal faces only when removal is safe. Check face normals, non-manifold or doubled geometry where relevant, UV damage, and silhouette after any topology reduction. Keep the approved color blocks and material separation; consolidate redundant materials without flattening distinct visual regions.
4. Set a consistent placement root/pivot at the **top-left footprint corner on the floor**. In the Blender working coordinates that is `(0, 0, 0)`, with intended bounds X `[0, 3]`, Y `[-4, 0]`, and Z at or above the floor. Apply rotation and scale after numerical sizing; final object scale should be `(1, 1, 1)` and rotation zero. Preserve the pivot through the later export/import comparison.
5. Scale and position by measured geometry, not visual guess. Save the `.blend` source. The accepted laptop export uses FBX after Unity import and footprint verification. Meshy's [Blender cleanup guide](https://help.meshy.ai/en/articles/16100257-meshy-to-blender-cleanup-workflow) was consulted for the original pipeline preparation.

## 3. Measure the top projection

In Blender, select **all and only** the laptop's final visible mesh objects, with modifiers evaluated. The following read-only Python Console snippet prints their combined world-space bounds. It does not change geometry:

```python
import bpy

depsgraph = bpy.context.evaluated_depsgraph_get()
points = []
for original in bpy.context.selected_objects:
    if original.type != 'MESH':
        continue
    obj = original.evaluated_get(depsgraph)
    mesh = obj.to_mesh()
    try:
        points.extend(obj.matrix_world @ vertex.co for vertex in mesh.vertices)
    finally:
        obj.to_mesh_clear()
if not points:
    raise ValueError('Select the laptop mesh objects before measuring')
bounds = [(min(point[i] for point in points), max(point[i] for point in points)) for i in range(3)]
print('X min/max:', bounds[0], 'width:', bounds[0][1] - bounds[0][0])
print('Y min/max:', bounds[1], 'depth:', bounds[1][1] - bounds[1][0])
print('Z min/max:', bounds[2])
print('edge deviations Xmin/Xmax/Ymin/Ymax:',
      bounds[0][0], bounds[0][1] - 3, bounds[1][0] + 4, bounds[1][1])
```

Target in Blender: X min/max `0 / 3`, Y min/max `-4 / 0`; target width/depth `3 / 4`. Record all four **signed edge deviations**, the actual width/depth, and Z min/max. Inspect a top orthographic view with a 3 × 4 grid overlay: the bounding box alone cannot prove that protrusions or large missing regions read as the canonical rectangle. Record any intentional visual overflow explicitly for human review; do not change the Domain footprint or silently accept a numerical deviation. After a final-format candidate exists, repeat the X/Z bounds and pivot check on the imported Unity model before accepting the interchange format.

## 4. Review record and stop

Record measured bounds, deviations, visible overflow, material/UV result, generation and cleanup times, and actual license/plan evidence in `asset-provenance.md`. Return the raw GLB, `.blend`, screenshots of top and angled views with the footprint overlay, and the measurement output for human review. Stop after the laptop pilot; do not process the other references or create ZA-004 prefabs.
