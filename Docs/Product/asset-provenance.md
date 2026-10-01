# Asset provenance

| Asset filename | Source | Date | Purpose | Production status |
|---|---|---|---|---|
| `item_sneaker_pair.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |
| `item_sweater_open.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |
| `item_sweater_folded.png` | Generated with ChatGPT image generation | 2026-10-01 | ZA-002 reference only | Not yet a final production mesh |
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
