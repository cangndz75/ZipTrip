# ADR-0001 — Domain Boundary

## Status

Accepted

## Context

ZipTrip'in gameplay kuralları Unity presentation katmanından bağımsız, deterministik ve test edilebilir olmalıdır.

Phase A boyunca grid, item shape, placement, command/state, solver ve gameplay legality aynı canonical kurallara dayanacaktır.

## Decision

`ZipTrip.Domain` saf C# assembly'sidir.

`ZipTrip.Domain`:

- UnityEngine'e referans vermez.
- MonoBehaviour içermez.
- GameObject, Transform, Vector3 veya Unity serialization tiplerini kullanmaz.
- Gameplay legality'nin canonical kaynağıdır.
- Integer/grid tabanlı deterministic veri ve kuralları barındırır.
- Solver tarafından doğrudan yeniden kullanılır.

`ZipTrip.Application`:

- UnityEngine'e referans vermez.
- Domain use-case'lerini orkestre eder.
- PlaceItem, MoveItem, ReturnToTray, RotateItem, FoldItem ve benzeri application operasyonlarını barındırır.
- Presentation katmanının gameplay state'ine eriştiği kontrollü sınırdır.

`ZipTrip.Unity`:

- Unity Editor/runtime integration ve presentation katmanıdır.
- Input, camera, rendering, prefab, animation ve visual feedback burada yaşar.
- Domain state'ini doğrudan mutate etmez.
- Gameplay legality hesaplamaz.
- Domain/Application sonucunu görselleştirir.

Dependency yönü:

```text
ZipTrip.Domain
     ↑
ZipTrip.Application
     ↑
ZipTrip.Unity
```

Domain hiçbir üst katmanı bilmez.

## Protected Rules

- Gameplay kuralı Unity presentation kodunda duplicate edilmez.
- Solver runtime'dan farklı bir legality implementasyonu kullanmaz.
- Float/world-space hesapları Domain grid mantığına sızmaz.
- Rejected command authoritative state'i değiştirmez.
- Domain davranışı Unity Editor çalışmadan test edilebilir olmalıdır.

## Enforcement

ZT-000B sırasında:

- `ZipTrip.Domain` assembly definition `noEngineReferences: true` olacaktır.
- `ZipTrip.Application` assembly definition `noEngineReferences: true` olacaktır.
- Domain içine geçici olarak `using UnityEngine;` eklenerek compile failure doğrulanacak ve değişiklik geri alınacaktır.

## Consequences

Artıları:

- Solver ve runtime aynı kuralları paylaşır.
- Gameplay testleri hızlı ve deterministic çalışır.
- Unity presentation değişiklikleri core logic'i etkilemez.
- Headless level validation mümkün olur.

Bedeli:

- Unity tipleri ile Domain tipleri arasında açık adapter/map katmanı gerekir.
- Presentation tarafında bazı veriler dönüştürülmek zorundadır.
