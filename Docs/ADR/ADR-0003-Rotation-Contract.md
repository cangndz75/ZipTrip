# ADR-0003 — Rotation Contract

## Status

Accepted

## Context

ZipTrip item'ları canonical integer grid üzerinde tanımlanır.

Rotation davranışı açıkça tanımlanmazsa:

- aynı item farklı sistemlerde farklı hücrelere oturabilir,
- solver ile runtime divergence oluşabilir,
- visual pivot gameplay anchor'ına sızabilir,
- determinism bozulabilir.

Bu nedenle rotation davranışı Domain seviyesinde tek ve açık bir kontrata sahip olmalıdır.

## Decision

Item rotation yalnızca canonical grid hücreleri üzerinde uygulanır.

Desteklenen rotation değerleri:

- 0°
- 90°
- 180°
- 270°

Rotation sonrası shape normalize edilir.

Normalization kuralı:

1. Occupied cell koordinatları rotation transform'una tabi tutulur.
2. Ortaya çıkan shape'in minimum X ve minimum Y değerleri bulunur.
3. Tüm hücreler bu minimum değerler çıkarılarak yeniden origin'e taşınır.
4. Normalize edilmiş shape'in minimum koordinatı `(0, 0)` olur.

Gameplay anchor:

- normalize edilmiş bounding box'ın sol üst hücresidir,
- integer grid koordinatıdır,
- visual mesh pivot'undan bağımsızdır.

Visual pivot ve mesh offset presentation katmanının sorumluluğudur.

## Allowed Rotations

Her `ItemDefinition` kendi `allowedRotations` set'ini tanımlar.

Bir item için izin verilmeyen rotation talebi:

`Rejected(RotationNotAllowed)`

sonucunu üretir.

Rejected rotation authoritative state'i değiştirmez.

## Fold Interaction

Fold state'leri rotation'dan ayrı authored shape state'leridir.

Örneğin:

- `SweaterOpen`
- `SweaterFolded`

iki ayrı canonical footprint'tir.

Rotation, aktif shape state üzerinde uygulanır.

Fold işlemi rotation matematiği tarafından otomatik türetilmez.

## Protected Rules

- Rotation float veya world-space koordinatlarıyla hesaplanmaz.
- Rotation sonucu her zaman normalize edilir.
- Visual mesh pivot gameplay anchor'ını belirlemez.
- Solver ve runtime aynı rotation implementasyonunu kullanır.
- Aynı shape dört kez 90° döndürüldüğünde canonical olarak başlangıç shape'ine dönmelidir.

## Tests

En az şu testler bulunmalıdır:

- irregular shape 90° rotation
- irregular shape 180° rotation
- irregular shape 270° rotation
- dört rotation sonrası başlangıç shape'i
- symmetric item
- izin verilmeyen rotation
- rotation sonrası normalization
- folded shape rotation

## Consequences

Artıları:

- Solver ve runtime aynı footprint'i üretir.
- Anchor davranışı deterministic olur.
- Mesh pivot hataları gameplay mantığını etkilemez.
- Serialization ve state hash daha kararlı olur.

Bedeli:

- Presentation katmanı mesh pivot ile grid anchor arasında explicit offset yönetmek zorundadır.
- Artist-authored pivot doğrudan gameplay verisi olarak kullanılamaz.
