# ZipTrip — Blueprint v1.2 Delta

> **Durum:** Accepted delta
> **Amaç:** Blueprint v1.1'i yeniden yazmak değil, yalnızca sonradan kilitlenen değişiklikleri ve düzeltmeleri tanımlamak.

## Authority Rule

ZipTrip'in ürün ve teknik kararlarında tek source of truth:

1. `ZipTrip_Blueprint_v1.1.md`
2. `ZipTrip_Blueprint_v1.2_Delta.md`

`ZipTrip_Phase_A_Execution_Plan.md` yalnızca execution order'dır.

Ticket'lar yalnız Blueprint + Delta'yı uygular.

Çelişki halinde:

> **Blueprint / Delta > Execution Plan > Ticket > implementation assumption**

## Δ-01 — Phase A Scope Lock

Phase A dışında:

- meta progression
- economy
- monetization
- analytics stack
- live ops
- geniş onboarding
- level map
- account sistemi

Phase A'nın görevi ürün çekirdeğini, solver doğruluğunu, visual readability'yi ve gameplay feel'i kanıtlamaktır.

## Δ-02 — Solver Phase A'nın Zorunlu Dependency'sidir

Solver “sonra yapılacak level tool” değildir.

Phase A içinde:

- PACK solver
- Fold dependency validation
- Repack preserve-start validation
- Extract BFS solver
- runtime/solver divergence testleri

bulunmalıdır.

Runtime ve solver aynı canonical legality kurallarını kullanır.

## Δ-03 — Application Boundary

`ZipTrip.Application` ayrı bir architectural boundary'dir.

Dependency:

```text
ZipTrip.Domain
     ↑
ZipTrip.Application
     ↑
ZipTrip.Unity
```

Hem `ZipTrip.Domain` hem `ZipTrip.Application`:

- UnityEngine'e referans vermez
- `noEngineReferences: true` ile enforce edilir

Unity presentation yalnız Application/Domain sonuçlarını görselleştirir.

## Δ-04 — Golden Asset Readability Week 1'e Çekildi

Art “graybox bittikten sonra” başlamaz.

Hidden-grid ↔ mesh uyumu Phase A'nın erken teknik riskidir.

Erken golden set:

- Sneaker
- Sweater Open
- Sweater Folded
- Laptop

MG-1 readability testi:

- 4 shape × 5 kullanıcı = 20 gözlem
- küçük footprint'lerde tolerans yok
- 3 hücre ve altı: tam isabet
- 4 hücre ve üstü: ±1 hücre tolerans
- Fold state'li item'lar iki state için ayrı test edilir
- başarı hedefi: ≥16/20 ve sistematik hata yok

Maximum test budget sonunda sonuç hâlâ inconclusive ise:

> **B/C belirgin farklılaşma üretmiyor**

olarak yorumlanır; test sonsuza uzatılmaz.

## Δ-05 — Golden Gameplay Integration Gate

Static readability yeterli değildir.

Golden prefab'lar drag + ghost footprint çalıştıktan sonra gerçek gameplay'e bağlanır.

Kontrol:

- mesh sınırı
- hidden footprint
- ghost footprint
- parmak altında okunabilirlik

aynı şeyi anlatmalıdır.

Bu gate, final snap / L1–L3 device pass'ten önce yapılır.

## Δ-06 — Fixed Camera

Gameplay camera fixed'dir.

- player orbit yok
- gameplay pan yok
- gameplay zoom yok
- aspect-aware framing var

Sayısal kamera parametreleri ZA-001 art/readability çalışmasında ölçülerek kilitlenir.

ADR-0002 uygulanır.

## Δ-07 — Input Abstraction

Mouse ve touch aynı semantic pointer akışını kullanır.

`PointerInteractor`:

- down
- move
- up
- cancel

Second touch aktif interaction sırasında yok sayılır.

`GridProjector` screen/world koordinatını integer anchor'a dönüştürür; legality hesaplamaz.

ADR-0004 uygulanır.

## Δ-08 — Unity Bootstrap Contract

Phase A baseline:

- Unity 6.3 LTS
- URP
- Linear color space
- Input System (New)
- portrait-only
- IL2CPP
- ARM64
- Android blank-device build erken doğrulanır
- iOS blank-device build A1 bitmeden stratejiye göre doğrulanır

## Δ-09 — GameState / Undo Ayrımı

Canonical `GameState` yalnız authoritative gameplay state içerir.

Undo/history:

> `GameSession` / Application seviyesinde tutulur.

Snapshot/history canonical state hash'in parçası değildir.

`StateHash` yalnız canonical authoritative state üzerinden hesaplanır.

Canonical serialization deterministic olmalıdır.

Runtime-dependent `GetHashCode` kullanılmaz.

## Δ-10 — Level Data Boundary

Domain JSON kütüphanesini bilmez.

- Domain: `LevelDefinition`
- Unity/adapter: DTO + parse + map

JSON parser/package eklemek package-change olduğu için insan onayı gerekir.

## Δ-11 — LevelCompleted Semantiği

`LevelCompleted`:

> yalnız `incomplete → complete` transition'ında bir kez emit edilir.

Complete state tekrar evaluate edilirse ikinci event oluşmaz.

Undo ile incomplete state'e dönülür, sonra tekrar complete olunursa yeni transition için yeni `LevelCompleted` emit edilir.

## Δ-12 — Feedback Color-only Olamaz

Valid/invalid feedback yalnız renge bağlı olamaz.

En az iki kanal kullanılmalıdır.

Örnek:

- color
- outline/shape
- movement/shake

Color-blind simulation altında ayırt edilebilirlik kontrol edilir.

## Δ-13 — Snap Timing

Final snap timing kontratı:

> **0–120 ms**

Başka dokümanlardaki 80–150 ms aralığı geçersizdir.

## Δ-14 — Fresh Clone DoD

Phase A bootstrap tamamlanmış sayılmaz unless:

1. temiz clone alınır
2. `git lfs pull` çalışır
3. doğru Unity 6.3 LTS ile proje açılır
4. compile temizdir
5. testler çalışır
6. Android development build alınabilir

Başka biri repo'dan projeyi açıp build alabilmelidir.

## Δ-15 — Debug Export

“Copy level state as JSON” ifadesi canonical gameplay state ile LevelDefinition'ı karıştırmamalıdır.

Debug export:

> **current state'ten reproducible LevelDefinition üretir**

ve ZT-006 loader ile yeniden yüklenebilir.

## Δ-16 — Phase A Creative Gate

Phase A yalnız temiz capture build üretip bitmez.

En az üç creative capture üretilir:

- A: Generic Pack
- B: Fold / Transform
- C: Extract

Phase A'da büyük paid-UA kampanyası zorunlu değildir.

Ama internal/organic comprehension testi yapılır:

- kullanıcı hook'u açıklamasız anlıyor mu?
- Fold farklılığı görülüyor mu?
- Extract farklılığı görülüyor mu?
- “neden sığmadı?” problemi var mı?

Paid campaign / budgeted creative test Phase B olabilir.

## Δ-17 — Mobile QA Matrix

Phase A device QA en az şunları içerir:

- pause/resume
- app background/foreground
- safe area
- hızlı drag
- ekran kenarında drag
- pointer cancel
- ikinci touch ignore policy
- orientation lock
- repeated restart/reset
- haptic on/off
- frame pacing / hitch kontrolü

## Δ-18 — Nest Phase A Dışında

Nest v1.1 tasarım alanında bulunabilir.

Ancak Phase A implementation scope'una dahil değildir.

Yeni grammar/mechanic eklenmez.

## Δ-19 — Execution Order

Phase A high-level order:

1. Bootstrap
2. Domain / Grid
3. Solver foundation
4. Pack
5. Fold
6. Golden asset gameplay validation
7. Vacuum
8. Repack
9. Extract + staging
10. Juice
11. Mobile QA
12. Capture + comprehension gate

Art/readability ayrı paralel track olarak Week 1'de başlar.

## Δ-20 — Documentation Discipline

Blueprint v1.1 + v1.2 Delta dışında yeni bir competing roadmap/source-of-truth oluşturulmaz.

Yeni teknik karar:

> ADR

Yeni scope/product değişikliği:

> Delta güncellemesi

Execution detail:

> mevcut Phase A planı / ticket

Dokümantasyon ilerleme hissi yaratmak için çoğaltılmaz; execution'a hizmet eder.

## Δ-21 — ADR-0006 Dynamic Packing Puzzle Contract

`Docs/ADR/ADR-0006-dynamic-packing-puzzle-contract.md` kabul edilmiştir ve dinamik packing gameplay kontratının implementation authority'sidir.

ADR-0006, Blueprint v1.1 ve bu Delta'daki çelişen gameplay hükümlerinin yerine geçer:

- Pack / Extract / Repack semantiği
- `W × H × L` 2.5D board temsili
- layer / access modeli
- staging capacity
- Source Tray ile staging ayrımı
- Fold / Compress / Nest semantiği
- rule / objective / invariant ayrımı
- completion kontratları
- atomic move semantiği
- Extract destination semantiği
- bu mekaniklerin gerektirdiği solver / validator modeli
- 10 level'lık DESIGN VALIDATION slice

Çelişmeyen mimari kararlar geçerliliğini korur. `Domain → Application → Unity` bağımlılık yönü değişmez. ADR-0001, ADR-0002, ADR-0003, ADR-0004 ve çelişmeyen diğer ADR'ler yürürlüktedir.

ZipTrip GDD v0.3 yalnız gelecek içerik keşfidir; implementation authority değildir.
