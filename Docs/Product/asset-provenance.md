# Asset provenance

ZA-003 per-item totals below are retrospective human-approved active-work estimates; not instrumented timings.

| Asset filename | Source | Date | Purpose | Production status |
|---|---|---|---|---|
| `item_sneaker_pair.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |
| `item_sweater_open.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |
| `item_sweater_folded.png` | Human-approved long-form `SweaterFolded_LongFront.jpeg`, generated with ChatGPT / OpenAI image generation during the human-guided ZA-003 workflow | Generated 2026-10-01; canonical replacement 2026-10-02 | ZA-003 procedural folded-sweater visual reference and top-surface texture identity for the 2×4 shape | Reference art; not production mesh geometry |
| `item_laptop.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |

## ZA-003 laptop 3D pilot — laptop candidate accepted

The laptop candidate was generated with Tripo on 2026-10-01. The user confirmed that the Tripo account was on a paid plan at generation time and accepted the laptop candidate. That acceptance closed the laptop asset only; the full ZA-003 ticket was closed after all four assets were accepted.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_Laptop` |
| Provider | Tripo |
| Provider selection | Meshy and Tripo were compared. Meshy low-poly geometry was usable, but its textured candidate had visible seam/color-bleed artifacts; Tripo better retained the intended form and material identity. |
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
| Total active work | ~1.5 h retrospective human-approved active-work estimate; not an instrumented timing. The partial file-timestamp intervals above remain separate evidence. |

## ZA-003 sweater open 3D candidate — asset accepted

The Sweater Open candidate was generated with Meshy on 2026-10-01. The human owner confirmed that the Meshy account was on a paid plan at generation time and that the model generation was private. That acceptance closed this asset only; the full ZA-003 ticket was closed after all four assets were accepted. No other account or license setting was verified for this asset.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_SweaterOpen` |
| Golden reference | `Assets/Art/Items/Golden/item_sweater_open.png` |
| Provider | Meshy |
| Provider selection | Meshy produced an acceptable candidate and was retained. |
| Plan at generation time | Paid — human confirmed |
| Model generation visibility | Private — human confirmed |
| Commercial-use record | Human-confirmed paid/private generation under Blueprint D-007; Meshy terms references are recorded in `Docs/Product/za-003-laptop-pilot.md`. No other license/account setting was verified. |
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
| Total human time | ~1.0 h retrospective human-approved active-work estimate; not an instrumented timing. Stage durations above remain unmeasured. |

## ZA-003 sweater folded procedural candidate — asset accepted

The human owner accepted this asset on 2026-10-02. The earlier square-ish golden reference was replaced because its visible shape did not agree with the canonical 2×4 hidden footprint. That acceptance closed the Sweater Folded asset only; the full ZA-003 ticket was closed after all four assets were accepted.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_SweaterFolded` |
| Geometry authorship | Procedurally authored in Blender; no Meshy or Tripo generated mesh is used in production geometry |
| Production selection | Meshy and Tripo AI meshes repeatedly failed topology/readability quality; AI outputs were visual/reference exploration only. The final geometry was procedurally authored in Blender. |
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
| Generation and cleanup time | ~4.5 h retrospective human-approved active-work estimate; not an instrumented timing. Stage durations were not measured. |

## ZA-003 sneaker pair 3D candidate — asset accepted

The human owner accepted this Sneaker Pair candidate on 2026-10-02. That acceptance closed the Sneaker Pair asset; the full ZA-003 ticket was closed after all four assets were accepted.

| Field | Recorded value |
|---|---|
| Asset | `M_Item_SneakerPair` |
| Golden reference | `Assets/Art/Items/Golden/item_sneaker_pair.png` |
| Provider | Tripo |
| Provider selection | Tripo was selected directly under the later per-asset provider decision; no failed Meshy attempt is claimed for this asset. |
| Account plan at generation | Paid Tripo plan — human confirmed |
| Commercial-use record | Human-confirmed paid plan; Tripo commercial-use terms references are recorded in the laptop entry above. This generation's private/public visibility was not verified. |
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
| Generation and cleanup time | ~1.5 h retrospective human-approved active-work estimate; not an instrumented timing. Stage durations were not measured. |

## ZT-040D.1 HUD typography — Bricolage Grotesque

| Field | Recorded value |
|---|---|
| Font | Bricolage Grotesque, SemiBold (600) and ExtraBold (800) static instances |
| Source | Google Fonts static WOFF (fonts.gstatic.com, v9), converted losslessly to TTF (stdlib WOFF 1.0 unpack, no glyph changes) |
| Files | `Assets/Art/Fonts/BricolageGrotesque/BricolageGrotesque-SemiBold.ttf`, `BricolageGrotesque-ExtraBold.ttf` |
| Licence | SIL Open Font License 1.1 — `Assets/Art/Fonts/BricolageGrotesque/OFL.txt` (from google/fonts `ofl/bricolagegrotesque`) |
| Coverage | Latin incl. Turkish (ı İ ş Ş ğ Ğ ç Ç ö Ö ü Ü) verified in the cmap |
| Approval | Human-approved typography and download, 2026-10-03 |
