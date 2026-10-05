# ADR-0009: Visual Asset Direction — Premium Stylized Product

- **Status:** Accepted
- **Date:** 2026-10-05
- **Deciders:** Can
- **Supersedes:** the "3D toy-like (Color Block Jam style)" item look recorded in the launch-scope decision
- **Related:** ADR-0006 (Dynamic Packing Puzzle Contract), ZIP-02 (completion moment), DEPTH-01 (planned)

> Verify the number against `docs/adr/` before committing. ADR-0008 is taken by the deferred mini-games decision.

## Context

Golden Lv1 looks too basic on device. Items come from several unrelated styles (patterned knit sweater, flat-icon passport, plain cylinder towel, labelled bottle, plastic block bag), the scene has no depth, and the old toy-like target reads as a children's game. ZipTrip is an adult-first premium casual game.

Several directions were generated for the same reference item (toiletry bag) and judged at real in-game size (~50 pt), not at 1024 px:

| Direction | Small-size read | Adult/premium | 3D-friendly | Result |
|---|---|---|---|---|
| v1 chunky toy | good | no (kids) | yes | rejected |
| v2 premium toy | best silhouette | partly | yes | rejected, kept as silhouette benchmark |
| v3 stylized product | good | yes | yes | form accepted, material too plastic |
| Photoreal / cinematic / luxury | weak (generic box) | yes | partly | rejected as form, kept as material reference |
| Cel / isometric / low-poly / painted / vintage | varies | varies | no (needs custom shaders or loses look in 3D) | rejected |
| **v3 form + realistic material (hybrid)** | good | yes | yes | **accepted** |

## Decision

ZipTrip moves from a premium-toy visual language to a **premium stylized product** language, keeping the silhouette clarity and small-screen readability proven by the toy direction.

**Golden references** (the quality bar for every item):
- `ZipTrip_golden_bag`: caramel leather toiletry bag, dark handle and bottom band, piping, enlarged brass zipper pull
- `ZipTrip_golden_camera`: mustard leather-wrapped instant camera, dark bottom band, layered lens, leather wrist strap on a brass ring

Only these two are references. Other approved items are compatible, not references.

### Item rules

1. **Target:** 70% refined product design, 30% game simplification. Mostly realistic proportions; exaggerate only interaction parts (pulls, handles, caps, straps).
2. **Construction:** 2–5 functional details built from geometry: piping, panel breaks, inset caps, sole separation, edge binding. No decorative cuteness.
3. **Structure color:** light main body, darker structural band or panel, restrained metal only where real.
4. **Material:** premium and believable, with subtle tonal variation and soft natural sheen, while staying clean, simplified and game-readable. No distressed leather, heavy grain, scratches, prints or noisy surface detail. The golden bag marks the upper realism limit.
5. **Forbidden:** text, logos, brands, flags, emblems, transparent glass, lens reflections, thin or floating parts.
6. **Readability check:** each item must read at ~50 pt **and from the top-down gameplay camera**.
7. **One style only:** v2 and v3 assets must never be mixed in the same build.

### Production pipeline

1. **Image:** ChatGPT, in one locked chat, with both golden references attached to every request. One object per image, pure white background, 3/4 view from slightly above.
2. **Review:** a contact sheet at game size next to the golden pair. Reject before spending 3D credits.
3. **3D:** Tripo Studio, image-to-3D, standard profile:
   - AI model H3.1, Ultra Mesh off, AI Complete off
   - Texture on, 2K, Remove Lighting on, PBR on
   - Triangle topology
   - Polycount **10K** for detailed items, **5K** for simple items
   - ~30 credits per model
4. **Engine:** import GLB, normalize to grid footprint with pivot at base centre (ZipTrip Asset Studio importer or Blender cleanup), review in the Lv1 scene under game lighting.

### Validation evidence

- Both golden assets converted to 3D with the standard profile. Camera strap loop stayed open, lens rings stayed separate, brass ring attached, hidden back sides plausible. Bag leather sheen preserved, zipper teeth carried in texture, pull and handle attached.
- Known softness: Tripo's preview looks duller than the source because Remove Lighting strips baked light. Final sheen must come from PBR under scene lighting and is verified in Unity, not in the Tripo viewer.

## Consequences

**Positive**
- One coherent, adult, premium look across hundreds of items.
- Cheap and repeatable: about 30 credits and a few minutes per item, without manual modelling.
- Material richness lives in textures, so geometry stays light enough for mobile.

**Negative / risks**
- Existing Lv1 items, the suitcase shell and the liner no longer match and must be regenerated.
- Plastic-dominant items (bottle, toothbrush, hair dryer) have not yet been proven against the golden pair; the camera was rendered as leather-wrapped instead of plastic.
- Folded fabric (t-shirt) still drifts toward product photography and needs one polish pass.
- Image models drift over long chats; the golden references must be attached every time.

## Follow-ups

1. Regenerate the suitcase shell and liner in this language (it is the largest object on screen); wire it through the SuitcaseRig contract from ZIP-02.
2. Regenerate Lv1 items: sweater, passport, towel, bottle, sunglasses, small bag.
3. Validate one plastic-dominant item against the golden pair.
4. DEPTH-01: contact shadows, AO in compartments, camera tilt trial.
5. Update the ZipTrip Asset Studio defaults: image prompt template, 3D profile.
