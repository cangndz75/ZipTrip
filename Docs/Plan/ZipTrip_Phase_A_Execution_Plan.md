# ZipTrip — Phase A Execution Plan

> **Rol:** Yalnız execution order.
> **Otorite değildir:** Blueprint v1.1 + v1.2 Delta ile çelişirse Blueprint/Delta geçerlidir.

## A0 — Bootstrap

Amaç:

- repo
- Git LFS
- canonical docs
- AGENTS
- ADR-0001..0004
- Unity 6.3 project settings
- assembly boundaries
- smoke tests
- Android blank-device build
- iOS build strategy / early validation

Exit:

- fresh clone reproducible
- compile/test clean
- blank Android device build çalışıyor

## A-Art — Golden Readability (A0 ile Paralel)

Sıra:

1. mini art bible
2. fixed camera sayısal parametreleri
3. Sneaker / Sweater Open / Sweater Folded / Laptop 2D refs
4. image-to-3D + cleanup
5. ReadabilityTest scene
6. MG-1

Bu track gameplay graybox'ı beklemez.

## A1 — Domain

Sıra:

1. canonical 8×10 grid + container mask
2. item shape + rotation + Fold states
3. placement validator
4. canonical GameState + command/event + StateHash
5. Application PACK use-case'leri

Exit:

- UnityEngine-free core
- deterministic tests
- rejected commands state mutate etmiyor

## A2 — Solver

Sıra:

1. LevelDefinition + adapter
2. PACK solver v1
3. Fold-aware solver
4. level validation runner
5. L1–L4 regression

Exit:

- solver/runtime divergence test geçiyor
- Fold dependency export gate çalışıyor

## A3 — PACK Presentation

Sıra:

1. BoardPresenter + ItemView + fixed camera
2. PointerInteractor + GridProjector
3. drag + ghost footprint
4. **golden prefab gameplay integration gate**
5. snap + invalid return + completion/reset
6. debug overlay + reproducible LevelDefinition export

Exit:

- L1–L3 telefonda oynanabilir
- mesh ↔ footprint ↔ ghost çelişmiyor

## A4 — Fold + Vacuum

Sıra:

1. Fold interaction + L4
2. Vacuum decision D-003 kapanır
3. Vacuum use-case + solver + L5

Exit:

- L4 Fold'suz çözümsüz
- L5 Vacuum'suz çözümsüz, tek Vacuum ile çözülür

## A5 — Repack

Sıra:

1. preplaced start
2. preserve-start validator
3. L6 / L7

Exit:

- başlangıcı koruyarak çözülen “Repack” export edilmez
- grammar-specific duplicate gameplay path yok

## A6 — Extract

D-002 ve D-010 kapanmadan implementation başlamaz.

Sıra:

1. slide state model
2. zipper-edge exit
3. 3-slot staging
4. re-entry
5. undo
6. BFS solver + metrics
7. L8–L10
8. Extract presentation

Exit:

- L8–L10 solver/runtime aynı sonucu verir
- sözlü açıklama olmadan cihazda denenebilir

## A7 — Juice + Mobile QA

Sıra:

1. lift/tilt
2. snap ≤120 ms
3. invalid feedback
4. slide-stop bounce
5. zipper payoff
6. SFX
7. haptics
8. mobile QA matrix

Exit:

- feedback legality'yi etkilemiyor
- gate devices'ta tekrar eden hitch yok
- QA matrix doldurulmuş

## A8 — Capture + Comprehension Gate

Sıra:

1. capture-safe build profile
2. deterministic level reset
3. debug UI gizleme
4. Creative A capture
5. Creative B capture
6. Creative C capture
7. internal/organic comprehension test
8. Phase A gate report

Exit:

- 10 level deterministic başlangıç state'i
- temiz capture alınabiliyor
- Pack/Fold/Extract hook'larının anlaşılabilirliği kayıtlı

## Execution Rule

Her çalışma:

1. tek ticket seç
2. dependencies kapalı mı kontrol et
3. ilgili ADR/Blueprint oku
4. en küçük implementation
5. tests
6. Unity Console
7. device gate gerekiyorsa cihazda doğrula
8. insan kabulü
9. commit

Aynı anda scope dışı “hazır başlamışken” işleri eklenmez.
