# ZipTrip Golden Lv1 Gerçek Durum Raporu — 2026-10-06

## A. Beş satırlık özet

1. Aktif dal `wip/gameplay-presentation`; `HEAD` ve `origin/wip/gameplay-presentation` aynı committe: `a85b278` (ART-03).
2. Çalışma ağacı temiz değil: Passport, Sweater ve Travel Pouch için commit edilmemiş ART-04 mesh/prefab/editor-test çalışması mevcut; sahnenin gerçek Editor hali `HEAD`'den farklı.
3. Lv1 ve Lv2 yüklenen, çözülebilir Pack seviyeleri; Domain/Application Fold, Compress, Nest, Extract ve Repack'i destekliyor, fakat shipping Lv1/Lv2 bunları kullanmıyor.
4. Golden Lv1 kompozisyonu, UGUI HUD, Santorini arka planı, suitcase sunumu, canlı kural geri bildirimi ve otomatik Zip It mevcut; SCENE-01 ve UI Kit işleri ayrı, merge edilmemiş dallarda.
5. En büyük doğrulanmış açık kaynak-asset yapım kalitesi ile güncel cihaz performans ölçümü; son görsel inceleme Sweater ve Travel Pouch'u `PARTIAL`, Passport'u kabul edilmiş sayıyor.

### Git durumu

- Yerel dallar: `main`=`5d0b532`; `wip/gameplay-presentation`=`a85b278`; `wip/scene01-review`=`8a9e143`; `wip/visual-align-01`=`4236ef6`; `wip/visual-align-02`=`066a1c0`; `wip/visual-align-03`=`066a1c0` (ayrı worktree). Son dört görsel dal commit'i aktif dalın ancestor'ı değil.
- Staged dosya yok. Değişen prefablar: `PF_Item_Passport`, `PF_Item_SweaterOpen`, `PF_Item_TravelPouch`; ayrıca ilgili materyal/ayar dosyaları Git durumunda modified görünüyor.
- Untracked: `Assets/Art/Models/Items/Art04/`, `Art04HeroAssets.cs`, `Art04HeroReview.cs`, `build_art04_corrective.py`, `build_art04_source.py`, `.utmp/`.

| # | Tarih | Commit | Mesaj | Dokunduğu alan |
|---:|---|---|---|---|
| 1 | 2026-10-06 | `a85b278` | asset family convergence | ART-03 item materyal/texture, builder ve test |
| 2 | 2026-10-05 | `a96a466` | premium presentation baseline | suitcase, 3 item prefab/mesh, presentation ve test |
| 3 | 2026-10-05 | `562f844` | folded sweater import scale | sweater import/prefab |
| 4 | 2026-10-05 | `8e384b9` | target composition and visual acceptance | kamera, HUD, rule/board/table, completion, ADR-0010 |
| 5 | 2026-10-05 | `0888572` | replace Golden Lv1 items | ART-01 altı item, sahne, ADR-0009, test |
| 6 | 2026-10-05 | `268265e` | authored Golden Lv1 travel HUD | HUD ve UI assetleri |
| 7 | 2026-10-05 | `0980be1` | set production camera | kamera framing ve test |
| 8 | 2026-10-05 | `1f79654` | polished packed completion sequence | Zip It/completion sunumu |
| 9 | 2026-10-05 | `45f6fc2` | motion, audio and haptic feel | feel servisleri/sunum |
| 10 | 2026-10-05 | `4ac8d74` | authored motion language | motion token/sunum |
| 11 | 2026-10-05 | `b862a74` | distinct suitcase materials | suitcase materyalleri |
| 12 | 2026-10-04 | `6c9909c` | add Blender backup art sources | kaynak `.blend` dosyaları |
| 13 | 2026-10-04 | `9abe48a` | lock Golden Lv1 reference | referans, seviye verisi, test/doküman |
| 14 | 2026-10-04 | `6defe4f` | add editmode and playmode coverage | testler/fixture'lar |
| 15 | 2026-10-04 | `f5caef9` | build Golden item family | item art/prefab/material |
| 16 | 2026-10-04 | `11e31ac` | add premium travel UI | UGUI sunumu ve UI assetleri |
| 17 | 2026-10-04 | `e93ed04` | add travel-world backdrop | arka plan/dressing |
| 18 | 2026-10-04 | `613cc7a` | document Lv1 content and layout | dokümanlar |
| 19 | 2026-10-04 | `ac30f72` | add safe-area HUD | HUD/safe area |
| 20 | 2026-10-04 | `1787018` | improve cabin readability | suitcase içi/astar sunumu |
| 21 | 2026-10-03 | `506906c` | add modifier interactions | Fold/Compress/Nest gameplay |
| 22 | 2026-10-03 | `113a873` | add Zip It ritual | completion/Zip It |
| 23 | 2026-10-03 | `4f29543` | add live rule feedback | kural UI/değerlendirme |
| 24 | 2026-10-03 | `8c16f4b` | add staging interactions | staging gameplay/sunum |
| 25 | 2026-10-03 | `62751ed` | add production typography | Bricolage Grotesque/font |
| 26 | 2026-10-03 | `2b59537` | add visual polish | gölge/post/sunum polish |
| 27 | 2026-10-03 | `0225b1` | add suitcase lid presentation | valiz kapağı |
| 28 | 2026-10-03 | `7953949` | integrate suitcase presentation | valiz/puzzle board entegrasyonu |
| 29 | 2026-10-03 | `d324977` | add suitcase asset | suitcase mesh/material/prefab |
| 30 | 2026-10-03 | `7948975` | add portrait composition | portrait sahne/kamera/UI |

## B. Ticket durumu

| Ticket | Durum | Kanıt | Kalan iş |
|---|---|---|---|
| ZIP-02 | **Bitti** | `113a873`, `1f79654`; `PuzzleCompletionPresenter`, otomatik completion ve Zip It testleri | Kodda `Kontrol Et`, can veya hamle limiti bulunmadı; ticket kapsamında açık kanıt bulunmadı. |
| ART-01 | **Kısmen** | `0888572`, altı canonical prefab aktif; ART-03=`a85b278`; üç ART-04 mesh sahnede commit edilmemiş | Sweater ve Travel Pouch insan incelemesinde kaynak-geometri barını geçmedi; ART-04 teslim edilmiş değil. |
| CAM-01 | **Bitti** | `0980be1`, ADR-0010, `PuzzleCameraFraming`, kamera kontrat testleri | Ayrı deney kamerası/seçicisi yok; deney sonucu production sabitlerine işlenmiş. |
| SCENE-01 | **Kısmen** | Aktif dalda `e93ed04` Santorini quad+dressing var; tam SCENE-01 commit'i `8a9e143` yalnızca `wip/scene01-review` dalında | `8a9e143` aktif dala merge edilmemiş. |
| UI-02 | **Belirsiz** | Mevcut UGUI HUD işlevsel; `wip/visual-align-01/02` UI kompozisyon/UI Kit commitleri aktif dalda değil; repo içinde kesin UI-02 kabul kaydı bulunamadı | Ticket kimliğiyle tamamlanma kanıtı bulunamadı. |
| POLISH | **Kısmen** | `2b59537`, `4ac8d74`, `45f6fc2`; motion, haptic, gölge ve post mevcut | `AudioCueService` clip slotları bilerek boş; `Assets` altında `.wav/.mp3/.ogg/.aiff` bulunamadı. |

### Gameplay ve test durumu

- `lv1-fit.json`: 5×7×1, altı kilitli roster, 3 preplaced + 3 tray, iki Zone rule. `lv2-rotate.json`: 4×7×1, laptop/sweater/sneaker, rule yok. İkisi de Pack ve otomatik testlerde çözülebilir.
- Domain/Application: Pack, Repack, Extract; Fold, Compress ve Nest command/state akışları var. Shipping Lv1/Lv2 katalog girdileri tek state kullanıyor; Repack/Extract yalnızca `Tests/EditMode/Fixtures/LevelsV2` içinde bulundu.
- Canlı rule preview ve committed rule senkronu `PuzzleRulesPresenter`/`RuleEvaluator` üzerinden var. Tamamlanma state'ten otomatik tetikleniyor; `Kontrol Et` butonu yok.
- Bu salt-okunur denetimde yeni Unity koşusu yapılmadı. En güncel mevcut sonuç: `Builds/art04/EditMode.xml` **282/282 geçti**; `Builds/art04/PlayMode.xml` **104 geçti, 0 kaldı, 23 skip** (2026-10-06).

## C. Görsel kalite envanteri

| Alan | Şu anki gerçek durum | Hedefe göre eksik |
|---|---|---|
| Sweater | Aktif NEW=`M_Item_Sweater_Art04.fbx`; Tripo ART-01 tabanı + Blender/Python geometri düzeltmesi; **7,843 tri**, 1 URP/Lit materyal, 3×1024 harita; normal var, packed metallic/smoothness var (`smoothness=.18`); bounds **2.90×0.50×2.64** | Son insan kararı: tüp/balloon kol ve kaynak inşa kalitesi nedeniyle `PARTIAL`. |
| Passport | Aktif NEW=`M_Item_Passport_Art04.fbx`; Tripo taban + Blender/Python reconstruction; **4,662 tri**, 1 URP/Lit, 3×1024; normal ve packed map var (`.48`); bounds **0.90×0.2426×1.72** | Bu ticket için kabul edildi; commit edilmemiş olması teslim durumu açığı. |
| Towel | Aktif CURRENT=ART-01 Tripo + Blender cleanup; **5,000 tri**, 1 URP/Lit, 3×1024; normal/packed var (`.15`); bounds **0.88×0.50×3.90** | ART-04 kapsamında yeni kaynak yok. |
| Shampoo | Aktif CURRENT=ART-01 Tripo + Blender cleanup; **4,999 tri**, 1 URP/Lit, 3×1024; normal/packed var (`.30`); bounds **0.88×0.4999×2.58** | ART-04 kapsamında yeni kaynak yok. |
| Sunglasses | Aktif CURRENT=ART-01 Tripo + Blender cleanup; **9,482 tri**, 1 URP/Lit, 3×1024; normal/packed var (`.30`); bounds **1.90×0.50×0.86** | Altı item içindeki en yüksek tri sayısı; yeni kaynak yok. |
| Travel Pouch | Aktif NEW=`M_Item_TravelPouch_Art04.fbx`; Blender/Python ile yeniden kurulmuş; **6,988 tri**, 1 URP/Lit, 3×1024; normal/packed var (`.48`); bounds **1.90×0.50×2.58** | Son insan kararı: eski kare organizer silueti ve yetersiz bölme derinliği nedeniyle `PARTIAL`. |
| Valiz/astar | Golden Cabin prefab aktif; ayrı kapak kayışı/toka/welt/stitch geometrisi var. Astar URP/Lit koyu teal (`.045/.10/.115`, smoothness `.19`); procedural quilt filler slab ve gerçek ribbon edge-piping var | SCENE-01 dalındaki ek environment/grounding aktif dalda yok. |
| Gölge | Item altında procedural footprint/blob (`alpha .6`); valiz altında ayrı contact+soft quad, tray shell shadow ve lining occlusion var | Mobile URP soft-shadow desteği kapalıyken runtime Key Light `Soft` istiyor. |
| Işık | Tek realtime Directional Key: RGB `(1,.95,.86)`, intensity `1.65`, Euler `(52,135,0)`, shadow strength `.68`; ambient trilight | Baked light/lightmap bulunmadı; ek fill/rim light bulunmadı. |
| Render/post | Mobile URP: render scale `.8`, MSAA `1`, HDR açık, depth/opaque texture kapalı, 1024 main shadow, 1 cascade, SRP Batcher açık. Runtime Volume: Neutral, exposure `.22`, contrast/saturation `20`, WB `9/2`, Bloom `.25`, Vignette `.12` | DOF yok; Mobile soft shadows kapalı; kamera AA yok. |
| Kamera | Runtime orthographic; pitch 65°/Euler X=25°, distance 12, near/far `.1/40`; ortho size ekran ve board bounds'tan dinamik | Serialized sahne Camera'sı perspektif/FOV 60; runtime bunu eziyor. Ayrı CAM-01 deney kamerası yok. |
| Arka plan | `santorini_vacation_soft` full-screen orthographic quad; blur dosyaya bake edilmiş. Boarding pass, postcard, map, hat ve olive sprig atlas quad dressing mevcut | Gerçek 3D destination set yok; aktif dalda SCENE-01 branch içeriği yok. |
| UI | UGUI/ScreenSpaceOverlay; seviye başlığı, rule card, polaroid, tray, Geri Al/Baştan ve completion var. Bricolage Grotesque Semibold/ExtraBold; `UiSlice011` + `PaperUi`, border varsa 9-slice; renkler kod sabitleri | Renk token ScriptableObject, maskot, coin ve ayar butonu bulunamadı. UI Kit branch'i merge edilmemiş. |

Altı aktif item toplamı **38,974 triangle**; her item tek materyal/renderer ve 1024 BaseColor+Normal+MetallicSmoothness kullanıyor. Android importer limiti 1024; texture-memory için profiler tabanlı resident bellek ölçümü bulunamadı.

## D. Doküman ↔ kod/sahne çelişkileri

1. Kilitli sırada ART-01, CAM-01'den önce; Git geçmişinde CAM-01 `0980be1`, ART-01 `0888572`'den daha eski commit olarak uygulanmış.
2. ADR-0006 Pack+Extract+Repack sözleşmesini kod destekliyor; shipping `lv1-fit` ve `lv2-rotate` yalnızca Pack. Extract/Repack gerçek level JSON'u bulunamadı, test fixture'larında var.
3. `golden-lv1-content-lock.md` sweater'ı approved/reuse kabul ediyor; gerçek worktree sweater'ı ART-04'te yeniden kuruyor ve son insan incelemesi hâlâ `PARTIAL` diyor.
4. `PuzzleGameplay.unity` Camera verisi perspektif/FOV 60; production runtime `PuzzleCameraFraming` bunu orthographic 65° sunuma dönüştürüyor.
5. Runtime ışık `LightShadows.Soft` seçiyor; aktif Mobile URP asset `softShadowsSupported=0`.
6. `AudioCueService` polish kodu 17 semantik clip slotu tanımlıyor; yorum ve asset taraması gerçek onaylı ses cliplerinin bulunmadığını gösteriyor.

## E. Teknik borç / risk

1. Aktif Editor görseli, commit edilmemiş ART-04 dosyaları nedeniyle `HEAD`/remote'dan yeniden üretilemiyor.
2. SCENE-01 ve UI Kit kanıtları ayrı dallarda; aktif production dalındaki durumla karıştırılma riski var.
3. Mobile soft-shadow capability ile runtime light isteği farklı; Editor/PC görüntüsü Android sonucunu temsil etmeyebilir.
4. Güncel P30 Pro frame-time/FPS/SetPass/batch kaydı bulunmadı; performans kabulü görsel capture'a dayanıyor.
5. Fold/Compress/Nest ve Extract/Repack altyapısının shipping level kapsamı yok; davranış test ve fixture ağırlıklı.

## F. Performans bütçesi

- Bulunan tek sayısal render kaydı ART-01 Editor capture'ındaki **97 draw call**; güncel ART-04 ve P30 Pro için FPS, frame time, SetPass ve batch sayısı **bulunamadı**.
- Mevcut mobile profil `.8` render scale, 1x MSAA, tek realtime directional shadow, 1 cascade, depth texture kapalı ve quarter-res Bloom kullanıyor; altı item toplamı 38,974 tri.
- **Tahmin:** draw-call tarafında sınırlı sayıda ek fullscreen pass için orta düzey pay olabilir; GPU shadow/post payı cihaz frame-time verisi olmadan sayısallaştırılamaz. DOF mevcut değil ve aktif depth-texture kapalı olduğu için mevcut bütçeye dahil değil.

## G. Ekran görüntüleri

- Projede `screenshots/` klasörü bulunamadı.
- Güncel gerçek Unity Lv1 açılışı, 1080×2340: `Builds/art04/new/level-start.png`
- Güncel gerçek Unity tüm item yerleştirilmiş, 1080×2340: `Builds/art04/new/all-placed.png`
- ART-04 karşılaştırma/inspection seti: `Builds/art04/current/`, `Builds/art04/new/`, `Builds/art04/comparisons/`
- Mevcut P30 Pro ve gate kanıt klasörleri: `Builds/visual_acceptance_011/`, `Builds/art01_capture/`, `Builds/artcc02/`
