# Asset provenance

| Asset filename | Source | Date | Purpose | Production status |
|---|---|---|---|---|
| `item_sneaker_pair.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |
| `item_sweater_open.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |
| `item_sweater_folded.png` | Human-approved long-form `SweaterFolded_LongFront.jpeg`; original image-generation provider not recorded | 2026-10-02 canonical replacement | ZA-003 folded-sweater identity, texture and 2×4 shape reference | Reference art; not a production mesh |
| `item_laptop.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |

## ZA-003 laptop 3D pilot — laptop candidate accepted

The laptop candidate was generated with Tripo on 2026-10-01. The user confirmed that the Tripo account was on a paid plan at generation time and accepted the laptop candidate. This closes the laptop asset only, not the full ZA-003 ticket.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_Laptop` |
| Provider | Tripo |
| Plan at generation time | Paid Tripo plan — human confirmed; account receipt/task ID not recorded here |
| Generation date | 2026-10-01 |
| Source reference PNG | `Assets/Art/Items/Golden/item_laptop.png` |
| Raw high-detail file | `Assets/Art/Models/Items/Raw/M_Item_Laptop_TripoRaw.glb` |
| Retopo source | `Assets/Art/Models/Items/Raw/M_Item_Laptop_TripoRetopo_8k.glb` |
| Blender source | `Assets/Art/Models/Items/Source/M_Item_Laptop_Source.blend` |
| Reproducible cleanup script | `Assets/Art/Models/Items/Source/M_Item_Laptop_Cleanup.py` |
| Unity final interchange | `Assets/Art/Models/Items/Final/M_Item_Laptop.fbx` |
| Texture source directory | `Assets/Art/Materials/Items/Laptop/Textures/` |
| Commercial-use basis | Human-confirmed paid plan; paid-user commercial-use rights described in [Tripo Terms §5.2.2](https://www.tripo3d.ai/terms) and [Tripo commercial-use help](https://www.tripo3d.ai/help/privacy-policy/how-to-use-tripo-models-commercially). Private visibility is not a commercial-use blocker. |
| Actual private/public visibility | Not manually verified |
| Final top projection | X = 3.0000, Z = 4.0000 Unity units |
| Final triangle count / material slots | 15111 / 1 |
| Unity import | PASS via `ModelImporter`; imported material's base-color texture is not automatically bound |

### Time record

Exact active-work timings were not measured. The local file timestamps give **estimated elapsed intervals**, which can include waiting, export, and inspection time; they are not measured hands-on time.

| Stage | Time record |
|---|---|
| Tripo generation / comparison | Not measured; no start timestamp or human duration supplied |
| Retopo | ~9 min estimate: raw file 19:35 → retopo file 19:44 |
| Export / FBX preparation | ~17 min estimate: Blender source 19:54 → first FBX file 20:11 |
| Automated Blender normalization | ~10 min estimate: retopo file 19:44 → Blender source 19:54 |
| Unity import verification | ~10 min estimate: first FBX file 20:11 → final screenshot 20:21 |
| Human review | Laptop candidate accepted; duration not measured or supplied |
| Total | ~46 min estimated observable local interval from raw file to final screenshot, excluding unmeasured Tripo generation/comparison and human review; full active-work total unavailable |

## ZA-003 sweater open 3D candidate — asset accepted

The Sweater Open candidate was generated with Meshy on 2026-10-01. The human owner confirmed that the Meshy account was on a paid plan at generation time and that the model generation was private. Acceptance closes this asset only; the full ZA-003 ticket remains open. No other account or license setting was verified for this asset.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_SweaterOpen` |
| Golden reference | `Assets/Art/Items/Golden/item_sweater_open.png` |
| Provider | Meshy |
| Plan at generation time | Paid — human confirmed |
| Model generation visibility | Private — human confirmed |
| Generation date | 2026-10-01 |
| Raw high-detail file | `Assets/Art/Models/Items/Raw/M_Item_SweaterOpen_MeshyRaw.glb` |
| Retopo source | `Assets/Art/Models/Items/Raw/M_Item_SweaterOpen_MeshyRetopo_8k.glb` |
| Blender source | `Assets/Art/Models/Items/Source/M_Item_SweaterOpen_Source.blend` |
| Reproducible cleanup script | `Assets/Art/Models/Items/Source/M_Item_SweaterOpen_Cleanup.py` |
| Unity final interchange | `Assets/Art/Models/Items/Final/M_Item_SweaterOpen.fbx` |
| Texture sources | `Assets/Art/Materials/Items/SweaterOpen/Textures/M_Item_SweaterOpen_MeshyRetopo_8k_basecolor.jpg`, `M_Item_SweaterOpen_MeshyRetopo_8k_normal.jpg`, `M_Item_SweaterOpen_MeshyRetopo_8k_rm.jpg` in the same directory |
| Canonical logical footprint | 3 × 3 cells |
| Unity visual bounds | X = [0, 3], Z = [-2.692080, -0.307920] |
| Visual top projection | 3.0000 × 2.38416 Unity units |
| Packing thickness | 0.375000 Unity units; bottom at Y = 0 |
| Geometry | 8,816 triangles, 7,126 vertices, 1 material slot |
| Unity import | PASS via `ModelImporter` |
| Human visual review | Open-sweater silhouette readable at 75°; +0.15 Y lift does not expose a large hollow underside. Shallow sleeve/hem openings at lower angles accepted; no capping performed. |

### Time record

| Stage | Time record |
|---|---|
| Meshy generation and retopo | Unknown; no measured duration supplied |
| Blender normalization and packing-pose revision | Unknown; no measured active-work duration supplied |
| FBX export and Unity verification | Unknown; no measured active-work duration supplied |
| Human review | Completed; duration unknown |
| Total human time | Unknown; no measured or supported estimate available |

## ZA-003 sweater folded procedural candidate — asset accepted

The human owner accepted this asset on 2026-10-02. The earlier square-ish golden reference was replaced because its visible shape did not agree with the canonical 2×4 hidden footprint. This closes the Sweater Folded asset only; the full ZA-003 ticket remains open.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_SweaterFolded` |
| Geometry authorship | Procedurally authored in Blender; no Meshy or Tripo generated mesh is used in production geometry |
| Reproducible Blender script | `Assets/Art/Models/Items/Source/M_Item_SweaterFolded_Procedural.py` |
| Blender source | `Assets/Art/Models/Items/Source/M_Item_SweaterFolded_Source.blend` |
| Approved long-form reference | `Assets/Art/Items/Golden/item_sweater_folded.png` |
| Top-surface texture source | `Assets/Art/Materials/Items/SweaterFolded/Textures/SweaterFolded_LongFront.jpeg`; the approved reference supplies the visual identity and mustard collar knit |
| Rejected R&D | Meshy and Tripo generated meshes were tried and rejected; their raw/retopo/Smart-Mesh outputs are not production dependencies |
| Unity final interchange | `Assets/Art/Models/Items/Final/M_Item_SweaterFolded.fbx` |
| Canonical logical footprint | 2 × 4 cells |
| Unity visual bounds | X = [0.0500, 1.9500], Z = [-3.9500, -0.0500]; bottom at Y = 0 |
| Packing thickness | 0.371316 Unity units |
| Final geometry | 9,600 triangles: body 8,064; collar 1,536 |
| Unity import | PASS via `ModelImporter` |
| Human visual review | PASS: folded-sweater identity and Sweater Open family, visual/hidden footprint agreement, revised collar, no giant cavity, texture smear or visible holes, and +0.15 Y drag lift |
| Generation and cleanup time | Unknown; active human time was not measured or supplied |

## ZA-003 sneaker pair 3D candidate — asset accepted

The human owner accepted this Sneaker Pair candidate on 2026-10-02. This closes only the Sneaker Pair asset; the full ZA-003 ticket remains open.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_SneakerPair` |
| Golden reference | `Assets/Art/Items/Golden/item_sneaker_pair.png` |
| Provider | Tripo |
| Account plan at generation | Paid Tripo plan — human confirmed |
| Generation date | 2026-10-02 |
| Actual private/public visibility | Not explicitly verified for this generation |
| Raw high-detail archive | `Assets/Art/Models/Items/Raw/M_Item_SneakerPair_TripoRaw.glb` |
| Production retopo source | `Assets/Art/Models/Items/Raw/M_Item_SneakerPair_TripoRetopo_10k.glb` |
| Blender source | `Assets/Art/Models/Items/Source/M_Item_SneakerPair_Source.blend` |
| Reproducible cleanup script | `Assets/Art/Models/Items/Source/M_Item_SneakerPair_Cleanup.py` |
| Unity final interchange | `Assets/Art/Models/Items/Final/M_Item_SneakerPair.fbx` |
| Texture source directory | `Assets/Art/Materials/Items/SneakerPair/Textures/` |
| Canonical footprint | `X. / X. / XX`; logical X [0, 2], Z [-3, 0] |
| Unity visual bounds | X [0.060000, 1.960000], Z [-2.995761, -0.060000]; height 1.039440, bottom Y = 0 |
| Forbidden-cell projection | (1,0) = 0%; (1,1) = 0% |
| Final geometry | 17,961 triangles |
| Deterministic cleanup | 28 specifically reviewed stray mesh islands removed (340 triangles); laces, pull tabs, sole pads, and decorative components preserved |
| Unity import | PASS via `ModelImporter` |
| Human visual review | PASS: L-footprint readability and remaining very small seam/edge traces accepted at gameplay scale |
| Generation and cleanup time | Unknown; no measured duration supplied |
