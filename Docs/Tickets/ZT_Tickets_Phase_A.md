# ZipTrip — Phase A Tickets

> **Revision:** execution-ready bootstrap revision. ZT-000B assembly sınırı, golden gameplay readability gate, canonical state/history ayrımı, completion event semantiği, debug export formatı ve Phase A creative comprehension gate netleştirilmiştir.

> **Authority rule.** Blueprint v1.1 + v1.2 Delta, ZipTrip'in ürün ve teknik kararlarında **tek source of truth**'tür. `ZipTrip_Phase_A_Execution_Plan.md` yalnızca execution order'dır. Bu ticket'lar yalnızca bu iki belgeyi uygular. Çelişki halinde ticket veya execution plan değil, **Blueprint/Delta geçerlidir.**
>
> **Scope lock.** Phase A dışında: meta progression, economy, monetization, analytics stack, live ops, geniş onboarding, level map, account sistemi.

## Tüm ticket'lar için ortak kurallar

- Agent, Blueprint v1.1 §7.3'teki task protokolünü uygular: AGENTS.md ve ilgili ADR'yi oku → mevcut kodu incele → kısa plan ve değişecek dosya listesi → en küçük uygulama → testler → console kontrolü → kanıtlı özet → insan kabulünden sonra commit.
- `ZipTrip.Domain` ve `ZipTrip.Application` UnityEngine'e referans vermez (asmdef `noEngineReferences: true`).
- Her kural için en az bir pozitif ve bir negatif test yazılır. "Exception fırlatmadı" tek başına bir assert değildir.
- Reddedilen bir command state'i değiştirmez. Bu, ilgili her ticket'ta test edilir.
- Package ekleme ya da güncelleme = Level D: önce insan onayı alınır.
- Ticket'ta açıkça yazmayan scene, prefab ya da package değişikliği yapılmaz.
- SP: 1 SP = 1 saat.

---

## A0 — Bootstrap

### ZT-000A — Repository, LFS ve agent context
- **SP:** 2 · **Sahip:** insan + agent · **Dependency:** yok
- **Amaç:** Tekrar üretilebilir bir repo ve agent'ın okuyacağı tek bağlam.
- **Scope:** Unity `.gitignore`; `.gitattributes` (Delta Ek D1'deki LFS uzantıları); `README.md` (aç/test et/build al); `AGENTS.md` (permission ladder, task protokolü, protected decisions, authority rule); `Docs/{ADR,Product,QA,Plan,Tickets}`; ADR-0001 (Domain sınırı), ADR-0002 (fixed camera), ADR-0003 (D-001 rotation), ADR-0004 (input abstraction ve multi-touch politikası).
- **Acceptance:**
  - `git lfs ls-files` test binary'sini listeliyor ve temiz bir clone'da dosya gerçek içerikle geliyor.
  - `AGENTS.md` authority rule'u birebir içeriyor ve yalnız Blueprint v1.1 + Delta'ya referans veriyor.
  - Repo'da canonical `ZipTrip_Phase_A_Execution_Plan.md` dışında legacy, alternatif veya duplicate plan belgesi yok.
- **Test:** Manuel: clone → `git lfs pull` → dosya boyutu kontrolü.
- **Out-of-scope:** Unity projesi, kod.

### ZT-000B — Unity projesi, ayarlar, assembly'ler ve smoke testler
- **SP:** 3 · **Sahip:** insan (proje oluşturma) + agent · **Dependency:** ZT-000A
- **Amaç:** Delta Δ-08'e uygun, boş ama sağlıklı bir proje.
- **Scope:** Unity 6.3 LTS, URP template; Linear color space; Input System (New); portrait-only; IL2CPP + ARM64. `Boot` ve `GameplaySandbox` scene'leri. Asmdef'ler: `ZipTrip.Domain` (`noEngineReferences: true`), `ZipTrip.Application` (`noEngineReferences: true`), `ZipTrip.Unity`, `ZipTrip.Editor`, `ZipTrip.Tests.EditMode`, `ZipTrip.Tests.PlayMode`. Referans yönü: `Domain <- Application <- Unity`; test assembly'leri yalnız ihtiyaç duydukları katmanlara referans verir.
- **Acceptance:**
  - Sıfır compile hatası ve sıfır console warning.
  - Domain'e `using UnityEngine;` eklendiğinde derleme **hata veriyor** (bu doğrulanır, sonra geri alınır).
  - `ProjectVersion.txt` commit'lenmiş.
- **Test:** 1 EditMode smoke test (Domain'deki trivial bir tipi çağırır) ve 1 PlayMode test (`Boot` scene yüklenir, hata yok).
- **Out-of-scope:** Gameplay tipleri, prefab, art.

### ZT-000C — Android blank device build
- **SP:** 2 · **Sahip:** insan · **Dependency:** ZT-000B
- **Amaç:** Cihaz build yolunu gameplay kodundan önce kanıtlamak.
- **Acceptance:** Development build fiziksel Android telefonda açılıyor ve portrait'te kalıyor. Fresh clone DoD (Δ-14) bir kez geçildi. `pre-core-v0` tag'i atıldı.
- **Out-of-scope:** Release signing, store ayarları.

### ZT-000D — iOS blank device build (D-009)
- **SP:** 3 · **Sahip:** insan · **Dependency:** ZT-000C, D-009 kararı
- **Amaç:** G6'nın iPhone dependency'sini erkenden kanıtlamak.
- **Acceptance:** Seçilen yoldan (local Mac / remote Mac / cloud CI) blank proje iPhone'a development build olarak kurulup açılıyor. Adımlar `Docs/ProjectInfo.md`'de yazılı.
- **Deadline:** A1 bitmeden.
- **Out-of-scope:** TestFlight, App Store Connect ayarları.

---

## A-Art — Golden readability (paralel track)

### ZA-001 — Mini art bible ve kamera parametreleri
- **SP:** 3 · **Sahip:** insan · **Dependency:** ZT-000A
- **Scope:** Kamera projeksiyonu ve açısı (ADR-0002'ye yazılır); ışık yönü; material dili; palet; detay yoğunluğu; hücre başına hedef ekran boyutu.
- **Acceptance:** `Docs/Product/art-bible.md` tek sayfa ve kamera parametreleri sayısal.
- **Out-of-scope:** Logo, UI, arka plan.

### ZA-002 — 3 golden 2D referans
- **SP:** 3 · **Sahip:** insan · **Dependency:** ZA-001
- **Scope:** Sneaker, sweater (açık + katlı), laptop için dört transparent PNG; item kimliği, renk/malzeme dili, açık/katlı state kimliği, hedef siluet ve gameplay footprint referansları.
- **Acceptance:** Mevcut dört PNG insan tarafından final ZA-002 stil/kimlik/şekil referansı olarak kabul edilmiştir; hedef footprint'leri `Docs/Product/golden-item-footprints.md`'de kayıtlıdır. Pixel-identical veya ortak kamera projeksiyonu ZA-002 koşulu değildir.
- **İnsan kararı (2026-10-01):** Ortak 75° gameplay kamera, orthographic projeksiyon, world scale, pivot ve mesh ↔ footprint uyumu gerçek 3D mesh'lerle ZA-003'te kurulup ölçülür.

### ZA-003 — Image-to-3D ve Blender cleanup
- **SP:** 5 · **Sahip:** insan · **Dependency:** ZA-002, D-007 ara cevabı
- **Scope:** Meshy (gerekirse Tripo ile karşılaştırma) → Blender: pivot, scale (1 hücre = sabit birim), topology ve material cleanup.
- **Acceptance:** Mesh'in üstten izdüşümü hedef footprint'e oturuyor. Kaynak, lisans ve provenance `Docs/Product/asset-provenance.md`'de. Tek item başına harcanan süre kaydedildi; bu kayıt Phase B tahmininin girdisidir.

### ZA-004 — Unity prefab'ları ve ReadabilityTest scene
- **SP:** 2 · **Sahip:** agent (Level C) + insan review · **Dependency:** ZA-003, ZT-000B
- **Scope:** `PF_Item_Sneaker`, `PF_Item_SweaterOpen`, `PF_Item_SweaterFolded`, `PF_Item_Laptop`; placeholder cabin container; `ReadabilityTest` scene. Scene statiktir, ADR-0002 kamerasını kullanır ve grid overlay aç/kapa ile ghost kapalı başlar.
- **Acceptance:** Scene gameplay koduna **bağımlı değil**. Prefab'larda gameplay script'i yok. Telefonda portrait çalışıyor.
- **Out-of-scope:** Drag, Domain entegrasyonu.

### ZA-005 — MG-1 okunabilirlik testi
- **SP:** 3 · **Sahip:** insan · **Dependency:** ZA-004
- **Scope:** 4 shape × 5 kullanıcı = 20 gözlem, Delta Δ-04 protokolüyle.
- **Acceptance:** ≥16/20 doğru ya da tolerans içinde ve sistematik hata yok. Sonuç `Docs/QA/readability-mg1.md`'de. Başarısızsa revize edilecek asset listesi yazılı.

---

## A1 — Domain

### ZT-001 — Canonical grid ve container mask
- **SP:** 3 · **Dependency:** ZT-000B
- **Amaç:** Unity'den bağımsız 8×10 board primitive'leri ve container mask doğrulaması.
- **Scope:** `Cell` (integer value type, eşitlik ve hash), `GridSize` (sabit 8×10), `ContainerMask` (valid/invalid hücre lookup, tam iterasyon), `ContainerDefinition` (id, mask, zipperEdge). Origin sol üst, row-major.
- **Acceptance:**
  - UnityEngine referansı yok; float yok.
  - `IsValid(cell)` sınır dışı hücre için exception fırlatmaz, `false` döner. Sınır dışı erişim davranışı açıkça tanımlı ve testli.
  - Cabin ve Backpack mask'leri **test fixture** olarak mevcut.
  - Tam mask iterasyonu deterministik sırada (row-major) ve tekrarlanabilir.
  - Stable, serialize edilebilir değer temsili var (ZT-004'teki hash'in girdisi).
- **Test:** Dört köşe; blocked köşe hücreleri (Cabin fixture); mask dışı iç hücre; sınır dışı (−1, 8, 10); tam iterasyonda hücre sayısı ve sırası; aynı input'un tekrarlı çalıştırmada aynı sonucu vermesi.
- **Out-of-scope:** Item, placement, rendering, drag, prefab, MonoBehaviour.

### ZT-002 — Item shape, rotation ve Fold state'leri
- **SP:** 3 · **Dependency:** ZT-001, **D-001 (ADR-0003) kapalı**
- **Amaç:** Item footprint'lerini ve state dönüşümlerini tanımlamak.
- **Scope:** `ItemShape` (occupied cell listesi, normalize edilmiş); `Rotation` enum (0/90/180/270); `ItemDefinition` (id, shapeStates, allowedRotations, tags, vacuumShape?); Fold state'leri ayrı authored shape'lerdir.
- **Önerilen D-001 cevabı** (ADR'de onaylanmalı): rotation hücreler üzerinde uygulanır, ardından min x/y = 0 olacak şekilde normalize edilir; anchor = normalize edilmiş bounding box'ın sol üstü. Görsel pivot presentation'ın işidir.
- **Acceptance:**
  - `allowedRotations`'ta olmayan rotasyon `Rejected(RotationNotAllowed)` döner.
  - Rotate ×4 orijinal shape'i verir.
  - Fold area kuralı (alan korunur ya da en fazla 1 hücre azalır) `ItemDefinition` oluşturulurken doğrulanır; ihlal eden tanım reddedilir.
- **Test:** Irregular sneaker shape'inin 4 rotasyonu; simetrik item; izinsiz rotasyon; sweater açık ↔ katlı; alan kuralı ihlali.
- **Out-of-scope:** Placement, Vacuum aksiyonu, animasyon.

### ZT-003 — Placement validator
- **SP:** 3 · **Dependency:** ZT-002
- **Amaç:** Bir item'ın anchor + rotation + shapeState ile container'a sığıp sığmadığına karar vermek.
- **Scope:** `PlacementValidator.Validate(board, item, anchor, rotation, shapeState)` → `Valid` ya da `Invalid(reason, offendingCells[])`. Reason tipleri: `OutOfBounds`, `OutsideMask`, `Overlap`.
- **Acceptance:**
  - Tüm occupied hücreler mask içinde olmak zorunda.
  - Overlap reddedilir ve çakışan hücreler raporlanır (Δ-12'deki invalid hücre işaretlemesinin kaynağı).
  - Float yok.
- **Test:** Irregular sneaker'ın blocked Cabin köşesine yerleştirilmesi; kısmi sınır taşması; tek hücrelik overlap; tam sığan son parça.
- **Out-of-scope:** Command, state değişimi.

### ZT-004 — GameState, command/event, undo ve state hash
- **SP:** 4 · **Dependency:** ZT-003
- **Amaç:** Deterministik ve hashable bir state machine iskeleti.
- **Scope:** Immutable canonical `GameState` (yalnız authoritative gameplay state: container, occupancy, tray, targets); `ICommand`; `CommandResult` (`Accepted(newState, events[])` / `Rejected(reason)`); semantic event'ler (`ItemPlaced`, `ItemRotated`, `FoldChanged`, `ItemReturned`, `LevelCompleted`); undo/history `GameState` dışında Application katmanındaki `GameSession`/snapshot stack'te tutulur; `StateHash` yalnız canonical `GameState` üzerinden hesaplanır.
- **Acceptance:**
  - State hash, canonical serialization üzerinden FNV-1a 64 ile hesaplanır. `GetHashCode` ya da runtime'a bağlı bir hash kullanılmaz.
  - Undo, canonical serialized gameplay state'i eşdeğer duruma geri getirir.
  - Rejected command'dan sonra state hash değişmez.
- **Test:** Property testi: rastgele (seed'li) 200 command dizisinde iki bağımsız çalıştırma aynı hash'i üretir; accepted move'dan sonra overlap yoktur; N accepted + N undo sonucu başlangıç hash'ine eşittir.
- **Out-of-scope:** Belirli grammar kuralları (ZT-005 ve sonrası).

### ZT-005 — Pack use-case'leri ve tray
- **SP:** 3 · **Dependency:** ZT-004
- **Scope:** `ZipTrip.Application`: `PlaceItem`, `MoveItem`, `ReturnToTray`, `RotateItem`, `FoldItem` (**yalnız tray'de**, v1.1 §4.3), `Undo`, `ResetLevel`. PACK tamamlanma koşulu: tüm zorunlu item'lar valid yerleşik ve tray boş.
- **Acceptance:**
  - Bag içindeki item'a `FoldItem` `Rejected(FoldOnlyInTray)` döner.
  - `LevelCompleted` yalnız `incomplete -> complete` transition'ında bir kez emit edilir. Complete state'in tekrar evaluate edilmesi ikinci event üretmez; undo ile incomplete'e dönülüp yeniden tamamlanırsa yeni transition için yeniden emit edilir.
  - Unity katmanının çağıracağı tek API budur.
- **Test:** Mini bir Pack senaryosu baştan sona oynanır; tamamlanma, undo ile geri alınan tamamlanma ve tekrar tamamlanma; tray'de fold → bag'e yerleştirme.
- **Out-of-scope:** Vacuum, Repack, Extract.

---

## A2 — Solver

### ZT-006 — LevelDefinition şeması ve JSON yükleme
- **SP:** 4 · **Dependency:** ZT-005, **JSON parser package onayı (Δ-10)**
- **Scope:** Domain `LevelDefinition` (id, grammar, containerId, preplaced[], inventory[], targets[], boosters, `schemaVersion`, `metricVersion`); Unity katmanında DTO + parse + map; Domain'de content validation.
- **Acceptance:** Eksik item/container id, desteklenmeyen schemaVersion, mask dışında preplaced item ve duplicate id reddedilir ve açık bir hata mesajı verir. Domain hiçbir JSON kütüphanesini bilmez.
- **Test:** Geçerli bir fixture; her bir hata tipi için ayrı fixture.
- **Out-of-scope:** Solver, level içerikleri.

### ZT-007 — Pack solver v1
- **SP:** 5 · **Dependency:** ZT-006
- **Scope:** `ZipTrip.Domain.Solver`: Blueprint v1.1 §10.2'deki canonical heuristic v1 ile backtracking — MRV, eşitlikte büyük alan önce, row-major aday sırası, rotasyonlar dahil. **Aynı `PlacementValidator`'ı** kullanır. Metrikler: solvable, çözüm sayısı (üst sınırlı sayım), first-solution/backtrack depth, forced placement ratio. `HeuristicVersion = 1`.
- **Acceptance:**
  - Solver'ın bulduğu her çözüm, runtime use-case'leriyle adım adım oynatıldığında `LevelCompleted` üretir (divergence testi).
  - Aynı level'da metrikler her çalıştırmada aynıdır.
- **Test:** Çözülebilir fixture; çözülemez fixture; tek çözümlü fixture; divergence testi.
- **Out-of-scope:** Fold, Vacuum, Extract.

### ZT-008 — Fold-aware solver ve fold dependency
- **SP:** 4 · **Dependency:** ZT-007
- **Scope:** Shape state'leri solver'ın arama uzayına eklenir; `fold dependency` metriği yazılır; "Fold olmadan çöz" modu eklenir.
- **Acceptance:** Fold-designed olarak işaretli bir level'da Fold'suz çözüm bulunursa export bloke edilir (v1.1 §10.3).
- **Test:** Fold'suz çözülemeyen fixture; Fold'suz da çözülebilen (reddedilmesi gereken) fixture.

### ZT-009 — Level validation runner ve L1–L4 içerikleri
- **SP:** 3 · **Dependency:** ZT-008
- **Scope:** Editor menüsü *ZipTrip → Validate All Levels*; tüm level'ları solver'dan geçiren EditMode regression testi ve metric snapshot'ları; L1–L4 JSON'ları (v1.1 Ek A).
- **Acceptance:** L1–L4 matristeki solver hedeflerini tutuyor (L1 100+ çözüm, L2 11–100, L3 2–10 ve ~%90 occupancy, L4 Fold'suz çözümsüz). Snapshot farkı varsa test kırılır.
- **Out-of-scope:** Görsel level editor (LATER).

---

## A3 — Pack presentation

### ZT-010 — BoardPresenter, ItemView ve fixed camera
- **SP:** 4 · **Dependency:** ZT-009, ADR-0002
- **Scope:** Domain state'ini render eden `BoardPresenter`; `ItemView` (visual root + shape referansı); ADR-0002 kamerası; aspect'e göre framing; placeholder küp item'lar.
- **Acceptance:** Presentation hiçbir Domain state'ini doğrudan değiştirmez; yalnız event dinler. 16:9, 19.5:9 ve 20:9'da board tamamen görünür.
- **Test:** PlayMode: L1 yüklenir ve item sayısı ile konumları Domain state'iyle eşleşir.

### ZT-011 — Input abstraction ve grid projector
- **SP:** 4 · **Dependency:** ZT-010, ADR-0004
- **Scope:** Input System üzerinde `PointerInteractor` (down/move/up/cancel), mouse ve touch aynı akışta; `GridProjector` (ekran → world → en yakın anchor); ADR-0004'e göre ikinci parmağın yok sayılması.
- **Acceptance:** Gameplay kodu input kaynağını bilmez. Projector integer anchor döner ve legality hesaplamaz.
- **Test:** EditMode: projector'ın sınır değerleri. PlayMode: simüle edilmiş pointer akışı.

### ZT-012 — Drag ve ghost footprint preview
- **SP:** 4 · **Dependency:** ZT-011
- **Scope:** Lift; drag; ghost footprint (Domain `PlacementValidator` sonucuyla); invalid hücre işaretleme; parmak offset'i.
- **Acceptance:**
  - Ghost'un valid/invalid kararı **yalnız** Domain'den gelir.
  - Feedback en az iki kanal kullanır (Δ-12) ve renk körlüğü simülasyonunda ayırt edilebilir.
  - Drag loop'unda per-frame managed allocation yok (Profiler ekran görüntüsü PR'da).

### ZT-016 — MG-2: Golden prefab entegrasyonu ve gameplay readability gate
- **SP:** 2 · **Dependency:** ZT-012, **MG-1 geçmiş**
- **Scope:** ZA-004 prefab'ları `ItemDefinition.visualPrefabId` üzerinden bağlanır; sweater'ın açık ve katlı prefab'ı state'e göre değişir. Golden item'lar gerçek drag + ghost footprint akışında cihazda doğrulanır.
- **Acceptance:** Prefab'larda gameplay script'i yok. Golden item'larda görünür mesh sınırı ile hidden-grid/ghost footprint oyuncuya çelişkili sinyal vermiyor. Sorun varsa ZT-013'e geçilmeden asset/pivot/scale/shape revize edilir.

### ZT-013 — Snap, invalid return, completion ve reset; L1–L3 cihazda
- **SP:** 3 · **Dependency:** ZT-016
- **Scope:** Release'te `PlaceItem`; accepted → snap (≤120 ms, Δ-13); rejected → tray'e ya da son konuma dönüş; basit "Packed" sonucu; reset; debug level select.
- **Acceptance:** L1–L3 telefonda baştan sona oynanıyor. Tween sırasında authoritative state hiçbir zaman belirsiz değil.
- **Test:** PlayMode: L1 simüle input ile tamamlanır ve `LevelCompleted` bir kez gelir.

### ZT-014 — Debug overlay ve command log
- **SP:** 3 · **Dependency:** ZT-013
- **Scope:** Dev build'de aç/kapa: grid mask, occupied hücreler, footprint, anchor, rotation, state hash; accepted/rejected command log; tek tık "export current setup as reproducible LevelDefinition JSON".
- **Acceptance:** Overlay Capture profilinde derlenmez ya da tamamen kapalıdır. Export edilen JSON, ZT-006 `LevelDefinition` loader'ıyla tekrar yüklenebilir ve aynı başlangıç state'ini üretir.

---

## A4 — Fold + Vacuum

### ZT-015 — Fold interaction ve L4
- **SP:** 3 · **Dependency:** ZT-013
- **Scope:** Tray'de Fold input'u; önce state değişir, animasyon sonra temsil eder; L4 cihazda oynanabilir.
- **Acceptance:** Animasyon sırasında state belirsiz değil. L4 Fold'suz çözülemiyor (ZT-008 ile tutarlı).

### ZT-017 — Vacuum ve L5
- **SP:** 4 · **Dependency:** ZT-015, ZT-016, **D-003 kapalı**
- **Scope:** `VacuumItem` use-case'i; vacuumShape; kullanım limiti; solver desteği; L5 JSON ve cihaz.
- **Acceptance:** L5 Vacuum'suz çözülemiyor ve tek Vacuum ile çözülüyor. Limit aşımı `Rejected(VacuumLimit)` döner. D-003 hangi cevabı verdiyse (tray-only ya da in-bag) yalnız o izinli.

---

## A5 — Repack

### ZT-018 — Preplaced start, preserve-start validator, L6/L7
- **SP:** 4 · **Dependency:** ZT-017
- **Scope:** Preplaced item'larla level başlangıcı (kilitli değiller); Blueprint v1.1 §4.4'teki formal kural: tüm preplaced item'ların anchor + rotation + shapeState'i sabitken solver çözüm bulmamalı. L6 ve L7 JSON'ları.
- **Acceptance:**
  - Preplaced'i sabitken çözüm bulunan bir "Repack" level'ı export'ta bloke edilir.
  - L7'de preplaced katlı sweater'ın state'ini değiştirmek için tray'e alınması gerekir (Fold yalnız tray'de).
  - L6/L7 için grammar'a özel bir kod yolu yok; aynı PACK use-case'leri kullanılıyor.
- **Test:** Preserve-start çözümü olan (reddedilmesi gereken) fixture; L6 ve L7 regression.

---

## A6 — Extract

### ZT-019 — Extract state modeli ve slide kuralları
- **SP:** 5 · **Dependency:** ZT-018, **D-002 ve D-010 kapalı**
- **Scope:** EXTRACT grammar'ı: yalnız cardinal slide; D-010'a göre mesafe; zipper edge'den çıkış; `SlideItem` use-case'i.
- **Acceptance:**
  - Diagonal hareket ya da bir engelin içinden geçiş reddedilir.
  - Move count slide mesafesinden bağımsızdır (her slide 1 hamle).
  - Tamamlanma koşulu: tüm target item'lar çıkmış.
- **Test:** Engel önünde durma; sınırdan geçerli çıkış; zipper edge olmayan kenardan çıkma denemesi.

### ZT-020 — Staging tray, exit, re-entry ve undo
- **SP:** 4 · **Dependency:** ZT-019
- **Scope:** `ExtractItem` ve `ReenterItem`; 3 slotluk staging; D-002'ye göre re-entry; undo.
- **Acceptance:** Staging hiçbir zaman 3'ü aşmaz. Staging doluyken non-target çıkamaz. Target completion tam olarak bir kez fire eder. Undo staging dahil tüm state'i geri getirir.
- **Test:** v1.1 §11.2'deki Extract invariant'larının hepsi.

### ZT-021 — Extract BFS solver, metrikler ve L8–L10
- **SP:** 5 · **Dependency:** ZT-020
- **Scope:** Item konumları + staging state'i üzerinde BFS; aynı use-case kurallarını kullanır. Metrikler: min moves, branching, tray peak, dead-end ratio. L8–L10 JSON'ları ve regression.
- **Acceptance:** L8 min 4 hamle / tray peak 1; L9 min 6–8 / peak 2; L10 min 8–12 / peak 3. Solver çözümü runtime'da oynatılınca tamamlanıyor (divergence testi). Target 1 hamlede çıkabiliyorsa, tasarım gereği tuzaklı olması gereken level'da export bloke.
- **Out-of-scope:** Extract solver'ında heuristic optimizasyonu (ölçülmüş bir performans sorunu yoksa).

### ZT-022 — Extract presentation
- **SP:** 4 · **Dependency:** ZT-021
- **Scope:** Slide drag input'u; slide-stop tween; staging tray UI; re-entry etkileşimi; L8–L10 cihazda.
- **Acceptance:** Presentation Extract legality'si hesaplamaz. L8–L10 telefonda sözlü açıklama olmadan denenebilir durumda.

---

## A7 — Juice + mobil QA

### ZT-023 — Juice v1
- **SP:** 4 · **Dependency:** ZT-022, D-008
- **Scope:** Lift/tilt, snap (≤120 ms), invalid shake, slide-stop bounce, completion; v1.1 §9.7 zipper sequence'i placeholder görsel ve sesle ama **final timing'le**; SFX stub'ları.
- **Acceptance:** Tüm efektler event'lerle tetiklenir; hiçbiri legality'yi etkilemez. D-008 gate cihazlarında hedef frame pacing'de, tekrarlayan hitch yok.

### ZT-024 — Haptics (Android + iOS)
- **SP:** 3 · **Dependency:** ZT-023, ZT-000D
- **Scope:** Snap, invalid ve completion için üç ayrı pattern. Native plugin ya da package gerekiyorsa Level D onayı alınır.
- **Acceptance:** Android ve iPhone'da üç pattern ayırt edilebilir. Ayarlardan kapatılabilir.

### ZT-025 — Mobil QA pass
- **SP:** 4 · **Dependency:** ZT-024
- **Scope:** Delta Ek D2 matrisinin tamamı gate cihazlarında çalıştırılır; bulunan sorunlar düzeltilir ya da kayda geçirilir.
- **Acceptance:** `Docs/QA/device-pass-A7.md` tüm satırları doldurulmuş. Açık kalan her madde için karar yazılı (fix / Phase B / kabul).

---

## A8 — Capture build

### ZT-026 — Capture build profili
- **SP:** 2 · **Dependency:** ZT-025
- **Scope:** Capture profili: debug overlay yok; debug level select gizli bir girişte; deterministic level reset; capture-safe framing. Pack, Fold ve Extract hook'larını ayrı ayrı gösteren 3 kısa capture creative hazırlanır ve küçük internal/organik comprehension testinde "ne yapılıyor?" sorusu ile anlaşılabilirlik gözlenir.
- **Acceptance:** 10 level'ın her biri aynı başlangıç state hash'iyle açılıyor. Temiz UI'da ekran kaydı alınabiliyor. 3 hook creative'i mevcut ve comprehension sonucu `Docs/QA/gate-A8.md` içinde kayıtlı. Phase A gate raporu yazıldı.
- **Out-of-scope:** Paid campaign spend, ölçekli A/B medya satın alımı ve UA optimizasyonu (Phase B).
