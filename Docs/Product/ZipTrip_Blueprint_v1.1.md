# ZipTrip — Blueprint v1.1

> **Durum:** Canonical product + technical baseline
> **Otorite:** v1.2 Delta tarafından açıkça override edilen maddeler hariç bu belge geçerlidir.

## 1. Ürün Tezi

**ZipTrip**, global pazara yönelik, yetişkin casual oyuncuyu hedefleyen, mobile-first **3D hybrid-casual travel packing puzzle** oyunudur.

Çekirdek ürün cümlesi:

> **Pack → Repack → Extract**

Oyuncu aynı temel bavul/container ve item sistemini farklı spatial puzzle grammars ile kullanır:

- **PACK:** Eşyaları bavula geçerli biçimde yerleştir.
- **REPACK:** Mevcut düzeni bozup yeni parçalar için yeniden optimize et.
- **EXTRACT:** Dolu bavuldan hedef eşyanın çıkış yolunu aç.

Pazarlama dili:

> **Pack, repack and unpack tricky luggage.**

Amaç “daha güzel Pack Master” olmak değildir. Travel packing fantasy'sini shape transformation, repacking, extraction, tactile 3D execution ve zipper payoff ile daha derin bir casual puzzle sistemine dönüştürmektir.

## 2. Hedef Kitle ve Konumlandırma

- adult-first
- global-first
- mobile-first
- casual / hybrid-casual
- çocuk oyunu değil
- 3D, toy-like, tactile, polished
- preschool görünüm yok
- gameplay alanı UI ve karakterlerden daha baskın

Yeni özellik filtresi:

> Yeni mekanik mevcut container board'u, mevcut item sistemi ve mevcut input dili kullanılarak üretilebiliyor mu?

Cevap hayırsa büyük ihtimalle ZipTrip'e ait değildir.

Şimdilik yapılmayacaklar:

- match-3
- traffic mini-game
- airport simulator
- city builder
- farklı kontrol şemasına sahip rastgele mini-game'ler
- multiplayer/social
- gerçek travel utility
- serbest physics packing

## 3. Canonical Board Modeli

Oyun 3D görünür fakat gameplay deterministic hidden-grid üzerinde çalışır.

Başlangıç canonical board:

> **8 × 10**

Container'lar farklı fizik sistemleri değildir. Aynı canonical grid üzerinde farklı **valid-cell mask** kullanırlar.

Grid:

- integer tabanlıdır
- solver ile runtime tarafından aynı şekilde yorumlanır
- kullanıcıya görünmek zorunda değildir
- visual mesh'ten bağımsız canonical gameplay footprint üretir

Origin ve iterasyon sırası:

- origin: sol üst
- row-major
- deterministik

## 4. PACK Grammar

PACK'te oyuncu tray'deki item'ları container'a yerleştirir.

Temel hareketler:

- drag
- discrete rotate
- placement
- item'ı tray'e geri alma
- uygun item'larda Fold state değiştirme

Tamamlanma koşulu:

- tüm zorunlu item'lar valid biçimde yerleşmiş olmalı
- PACK inventory tray boş olmalı

Successful completion sonrası signature zipper payoff oynar.

## 5. REPACK Grammar

REPACK başlangıçta preplaced item'lar içerir.

Oyuncu:

- mevcut item'ları hareket ettirir
- gerekiyorsa tray'e alır
- yeni item'lar için alan açar

Preplaced item'lar kilitli değildir.

Bir REPACK level'ın gerçek Repack olması için:

> Tüm preplaced item'ların anchor + rotation + shape state'i sabit tutulduğunda solver çözüm bulamamalıdır.

Aksi halde level aslında Repack gerektirmiyordur.

Return Trip güçlü tema örneğidir.

## 6. EXTRACT Grammar

EXTRACT, PACK'in ters çevrilmiş hali değildir. Ayrı bir movement grammar'dır.

Kurallar:

- cardinal slide only
- free drag-and-drop yok
- diagonal hareket yok
- item engelin içinden geçemez
- rotate yok
- Fold yok
- hedef item belirlenmiş zipper/exit edge'den çıkar
- her slide, mesafeden bağımsız olarak 1 move sayılır

Board hedefi:

- yaklaşık %80–90 occupancy
- hareket için bilinçli boş hücreler
- çıkış yolu ve blocking ilişkisi okunabilir olmalı

### Staging Tray

EXTRACT'ta maksimum **3 slotluk staging tray** vardır.

PACK tray ile aynı mekanik değildir.

- PACK tray = henüz yerleştirilmemiş kaynak
- EXTRACT staging = geçici puzzle storage

Staging 3'ü aşamaz.

Re-entry davranışı ayrı karar/ADR ile kilitlenir.

## 7. Item Shape ve Rotation

Rotation free-angle değildir.

Desteklenen canonical rotasyonlar:

- 0°
- 90°
- 180°
- 270°

Rotation grid hücreleri üzerinde uygulanır.

Canonical rotation contract ayrıntısı ADR-0003 tarafından kilitlenir.

Visual mesh pivot gameplay anchor değildir.

## 8. Fold

**Fold core ve ücretsiz gameplay action'ıdır. Booster değildir.**

Fold'un amacı item'ı her zaman dramatik biçimde küçültmek değildir.

Asıl amaç:

> **shape transformation**

Örnek:

- scarf: 1×6 ↔ 2×3
- sweater: yaklaşık 3×3 ↔ 2×4
- pants: uzun/dar ↔ kısa/geniş

Fold state'leri authored canonical footprint'lerdir.

Launch/slice kuralı:

- Fold yalnız item tray'deyken yapılır
- bag içindeki item önce tray'e dönmelidir
- Fold reversible olabilir
- Fold state area kuralı: alan korunur veya en fazla 1 hücre azalır

Fold-designed level, Fold kullanılmadan çözülebiliyorsa export edilmemelidir.

## 9. Vacuum / Compress

Vacuum gerçek footprint küçültme davranışıdır ve Fold'dan ayrıdır.

- Fold = ücretsiz shape decision
- Vacuum = güçlü, sınırlı rescue/booster

Vacuum kullanım yeri ve interaction sınırı ayrı karar ile kapatılır.

Slice'ta L5:

- Vacuum olmadan çözülemez
- tek Vacuum ile çözülebilir

## 10. Nest

v1.1 tasarım alanında küçük item'ları bir packing cube/toiletry bag içine gruplayan **Nest** fikri bulunur.

Ancak bu mekanik Phase A implementation scope'una dahil değildir. Phase A için v1.2 Delta scope lock geçerlidir.

## 11. Container Fantasy

Uzun vadeli container ailesi:

- Backpack
- Cabin Suitcase
- Large Suitcase
- Duffel

Phase A için:

- Backpack
- Cabin Suitcase

yeterlidir.

Ürün “her şeyi kutuya koyma” oyununa dönüşmemelidir. Ana fantasy travel packing olarak kalır.

## 12. Theme / Scenario Örnekleri

- Weekend Trip
- Beach Trip
- Business Trip
- Winter Trip
- Camping
- Carry-On / Low-Cost Flight
- Return Trip
- Airport Security

Tema yalnız art değişikliği değildir. Item geometry ve grammar baskısını da etkiler.

## 13. Timer

İlk yaklaşık 10 level'da timer yoktur.

Timer daha sonraki campaign / event tasarımlarında değerlendirilebilir.

Phase A core gameplay'i timer'a bağlı değildir.

## 14. Difficulty Prensibi

Zorluk yalnız item sayısıyla artmaz.

Ana kaynaklar:

- container geometry
- item shape
- allowed rotation
- Fold dependency
- occupancy
- solution density
- forced placement ratio
- Repack preserve-start dependency
- Extract minimum moves
- branching
- staging tray peak
- dead-end ratio

## 15. Phase A / Slice Ürün Kapsamı

Hedef:

- yaklaşık 10 level
- yaklaşık 10 reusable item
- 2 container
- 3 Pack
- 2 Fold Pack
- 2 Repack
- 3 Extract
- final-quality'ye yakın golden item art
- güçlü snap
- Fold animation
- zipper payoff
- basic haptics
- minimal UI

Bu slice final onboarding sırası değildir.

Amaç üç grammar ve ana creative angle'ların gerçek gameplay ile test edilmesidir.

## 16. Art Direction

Hedef:

- 3D
- toy-like
- tactile
- rounded
- colorful
- polished
- clean lighting
- strong material separation
- readable silhouettes

Kaçınılacaklar:

- preschool
- gameplay alanını kaplayan karakterler
- büyük gameplay logosu
- child-like UI

Hidden-grid ↔ mesh ilişkisi oyuncuya sezgisel görünmelidir.

Oyuncu “neden buraya sığmadı?” dememelidir.

## 17. Signature Zipper Payoff

Başarılı level sonunda hedef sekans:

1. son item snap
2. kısa visual confirmation
3. lid approach
4. gerekiyorsa hafif squash/compression
5. zipper movement
6. `zzzip`
7. final click
8. haptic
9. kısa success transition

Juice gameplay legality'yi değiştirmez.

## 18. Core Architecture

Engine:

> **Unity**

Core logic rendering'den bağımsız tutulur.

Temel katmanlar:

```text
ZipTrip.Domain
     ↑
ZipTrip.Application
     ↑
ZipTrip.Unity
```

`ZipTrip.Domain` ve `ZipTrip.Application` UnityEngine'e referans vermez.

Domain:

- grid
- item shape
- container mask
- placement legality
- canonical state
- solver-compatible rules

Application:

- PlaceItem
- MoveItem
- ReturnToTray
- RotateItem
- FoldItem
- Undo
- ResetLevel
- daha sonra Extract/Vacuum use-case'leri

Unity:

- input
- camera
- presentation
- prefab
- animation
- feedback

## 19. Command / State Prensibi

Gameplay state deterministic ve canonical olmalıdır.

Command sonucu:

- `Accepted(newState, events[])`
- `Rejected(reason)`

Rejected command state'i değiştirmez.

Semantic event örnekleri:

- ItemPlaced
- ItemRotated
- FoldChanged
- ItemReturned
- LevelCompleted

Canonical state hash runtime-dependent `GetHashCode` kullanmaz.

## 20. Solver

### PACK Solver v1

Canonical heuristic:

1. MRV
2. tie-break: büyük area önce
3. row-major candidate order
4. allowed rotations dahil
5. runtime ile aynı `PlacementValidator`

Ölçümler:

- solvable
- solution bucket / upper-bounded count
- first-solution depth
- backtrack depth/count
- forced placement ratio

`HeuristicVersion = 1`

Solver sonucu runtime use-case'leri ile replay edildiğinde aynı completion sonucunu vermelidir.

### Fold-aware Validation

Fold-designed level:

- Fold'suz çözülüyorsa geçersiz
- Fold gerekli olmalı

### REPACK Validation

Preserve-start solver check zorunludur.

### EXTRACT Solver

BFS tabanlı canonical validation.

Metrikler:

- min moves
- branching
- tray peak
- dead-end ratio

## 21. İlk 10 Level Validation Hedefleri

- **L1:** Pack, 100+ çözüm
- **L2:** Pack, 11–100 çözüm
- **L3:** Pack, 2–10 çözüm, yaklaşık %90 occupancy
- **L4:** Fold Pack, Fold olmadan çözümsüz
- **L5:** Vacuum, Vacuum olmadan çözümsüz; tek Vacuum ile çözüm
- **L6–L7:** Repack, preserve-start ile çözümsüz
- **L8:** Extract, min 4 move, tray peak 1
- **L9:** Extract, min 6–8 move, tray peak 2
- **L10:** Extract, min 8–12 move, tray peak 3

## 22. Creative Angles

### A — Generic Packing

Hook:

> **Can you make it fit?**

### B — Fold / Transform

Shape transformation + rearrangement.

### C — Extract

Hook:

> **PASSPORT IS TRAPPED!**

Creative'ler yalnız marketing değildir. Hangi grammar'ın kullanıcıya en güçlü biçimde geçtiğini ölçer.

## 23. Kırmızı Çizgiler

Proje şu durumlarda yeniden değerlendirilir:

1. Fold / Extract generic Pack'ten anlamlı biçimde ayrışmıyorsa
2. oyuncu hidden-grid nedeniyle “neden sığmadı?” diyorsa
3. Pack + Repack + Extract + Fold kombinasyonu 30–40 level içinde monotonlaşıyorsa

200 level üreterek temel problemi gizlemeye çalışılmayacaktır.

## 24. Task Protocol

Her agent task'ında:

1. `AGENTS.md` ve ilgili ADR okunur
2. mevcut kod incelenir
3. kısa plan ve değişecek dosyalar belirtilir
4. en küçük doğru uygulama yapılır
5. testler çalıştırılır
6. Unity compile / Console kontrol edilir
7. kanıtlı özet verilir
8. insan kabulünden sonra commit atılır

Package ekleme/güncelleme insan onayı gerektirir.

## 25. Açık / Gated Kararlar

Aşağıdaki kararlar ilgili ticket'a gelmeden kapatılmalıdır:

- **D-001:** Rotation/anchor contract → ADR-0003 ile kapatıldı
- **D-002:** Extract re-entry davranışı
- **D-003:** Vacuum tray-only mı, in-bag mi
- **D-004:** Creative test threshold / budget
- **D-005:** Store vs landing-page test yolu
- **D-006:** Test ülkeleri / geo
- **D-007:** Golden asset / image-to-3D pipeline — Phase A için insan kararıyla kapatıldı (2026-10-01): üretim yalnız ücretli Meshy planında ve private; Community'ye yayımlanmaz. Raw `.glb` → Blender `.blend` cleanup → insan kabulünden sonra Unity-ready export. Tripo yalnız Meshy sonucu makul denemelerden sonra yetersiz kalırsa, ayrı ücretli/ticari lisans kaydıyla karşılaştırma fallback'idir. Final `.glb`/`.fbx` formatı laptop pilot ölçümünden sonra seçilir.
- **D-008:** Gate devices + frame pacing hedefi
- **D-009:** iOS device-build stratejisi
- **D-010:** Extract slide distance kontratı

## 26. Nihai Ürün Tezi

> **ZipTrip, oyuncunun farklı seyahat senaryolarında eşyaları bavula yerleştirdiği, mevcut düzeni yeni parçalar için yeniden optimize ettiği ve gerektiğinde dolu bavuldan hedef eşyaları çıkardığı, hidden-grid üzerine kurulmuş tactile 3D hybrid-casual puzzle oyunudur.**

En önemli kural:

> **Yeni fikir eklemek yerine önce bu çekirdeğin gerçekten iyi olduğunu kanıtla.**

## Ek A — Phase A canonical item ve L1–L4 içerikleri

Bu ek, ZT-009 için insan tarafından onaylanan authored içeriği kaydeder. Tüm footprint'ler canonical integer hücreleridir; rectangle ölçüleri genişlik × yüksekliktir. Fold state sırası `0 = open`, `1 = folded` ve Domain string ID'leri `open` / `folded`dır.

| Item | Open footprint | Folded footprint | İzinli rotasyonlar |
|---|---|---|---|
| sweater | 3×3 | 2×4 | 0°, 90° |
| pants | 2×5 | 3×3 | 0°, 90° |
| scarf | 1×6 | 2×3 | 0°, 90° |
| towel | 1×4 | 2×2 | 0°, 90° |
| laptop | 3×4 | — | 0°, 90° |
| book | 2×3 | — | 0°, 90° |
| camera | 2×2 | — | 0° |
| bottle | 1×3 | — | 0°, 90° |
| passport | 1×2 | — | 0°, 90° |
| sneaker | `(0,0) (0,1) (0,2) (1,2)` | — | 0°, 90°, 180°, 270° |

L1–L4 `grammar=pack`, `preplaced=[]`, `targets=[]` ve `boosters.vacuumCount=0` kullanır. Inventory sırası aşağıdaki sıradır.

| Level | Container | Inventory | Fold | Canonical solver hedefi |
|---|---|---|---|---|
| L1 | Backpack | book, camera, bottle, sneaker | Kapalı | `100_PLUS` |
| L2 | Backpack | book, bottle, camera, laptop, sneaker | Kapalı | 12 çözüm, `11_99`; yalnız 0° rotasyonla çözümsüz |
| L3 | Cabin | camera, laptop, pants, sneaker, sweater | Kapalı; pants ve sweater açık | 4 çözüm, `2_10`; 39/44 = %88,6363… doluluk |
| L4 | Cabin | book, bottle, camera, laptop, scarf, sneaker, sweater | Açık | Fold'suz 0; sweater açık kilitliyken 0; yalnız sweater Fold ile çözülebilir |

L3'ün 39/44 doluluğu, yaklaşık %90 ürün hedefinin onaylanmış authored karşılığıdır. L3 sneaker'ın dışarıda kaldığı son-item Creative A anı için, L4 ise sweater'a özgü Fold dönüşümü ve Creative B için kaynak level'dır. L4'te açık footprint toplamı 44/44, ilk canonical Fold çözümünde 43/44'tür.
