# ADR-0004 — Input Abstraction and Multi-Touch Policy

## Status

Accepted

## Context

ZipTrip mobile-first bir oyun olsa da geliştirme ve test sırasında mouse ile de çalıştırılacaktır.

Mouse ve touch için ayrı gameplay yolları oluşturmak:

- davranış farklarına,
- duplicate input logic'e,
- test karmaşıklığına,
- platform divergence'a

neden olabilir.

Ayrıca Phase A gameplay'i single-pointer interaction olarak tasarlanmıştır.

## Decision

Unity Input System kullanılacaktır.

Mouse ve touch aynı abstraction katmanını besler.

Ana input abstraction:

`PointerInteractor`

Aşağıdaki semantic pointer event'lerini üretir:

- PointerDown
- PointerMove
- PointerUp
- PointerCancel

Gameplay katmanı input kaynağının:

- mouse,
- touch,
- editor simulation

olduğunu bilmez.

## Pointer Ownership

Aynı anda yalnızca bir active gameplay pointer kabul edilir.

Bir drag/interaction aktifken ikinci touch:

- gameplay interaction başlatmaz,
- active pointer'ı devralmaz,
- current drag state'ini değiştirmez,
- yok sayılır.

Active pointer release veya cancel olduğunda ownership temizlenir.

## Grid Projection

Screen-space input doğrudan gameplay legality üretmez.

`GridProjector`:

screen position → world position → canonical integer grid anchor

dönüşümünden sorumludur.

`GridProjector`:

- integer anchor üretir,
- placement legality hesaplamaz,
- overlap kontrol etmez,
- container validity kararı vermez.

ZT-011 için açık insan kararı: continuous grid koordinatı en yakın integer anchor'a çevrilir. Tam yarım/eşit uzaklıkta X için küçük sütun, Y için küçük satır seçilir; dört yönlü eşitlikte sol üst anchor seçilir. Bu kural `Mathf.RoundToInt` veya platforma bağlı midpoint rounding'e bırakılmaz.

Board dışında da en yakın **ham** integer anchor döner; canonical 8×10 sınıra veya container mask'e clamp edilmez. Örneğin `(-1, -1)` ve `(8, 10)` geçerli projector çıktıları olabilir. Sınır/maske/placement legality kararları Domain katmanında kalır.

Bu kararlar Domain tarafındaki canonical validator'lardan gelir.

## Drag Policy

ZT-012 için açık insan kararları: preview yalnız tray item'ları içindir; board'daki item bu ticket'ta taşınmaz. Pointer-down hit testi collider/raycast yerine interaction plane kesişimindeki X/Z noktasını tray `ItemView` occupied cell dikdörtgenleriyle karşılaştırır; boş bounding-box hücresi hit değildir. Birden çok görsel çakışırsa mevcut presentation sırasındaki en öndeki seçilir. Kimlik `ItemView.ItemId` üzerinden alınır.

İlk grab delta aynen korunur: `itemVisualAnchorWorld - projectedPointerWorld`; ek ergonomik offset `0`'dır. Aktif item yalnız görsel olarak `+0.15` world unit yükselir; tilt, bounce, pulse veya easing yoktur. Ghost board düzleminden `+0.02` world unit yukarıdadır.

ZT-012 feedback kanalları: valid footprint art-bible teal `#4E9FA2` ve yaklaşık `%32` alpha; her occupied ghost cell çevresinde warm cream `#E9E5DD`, yaklaşık `%90` opacity ve `0.06` cell kalınlığında sabit outline vardır. Valid durumda X işareti yoktur. Invalid footprint terracotta `#C36F58` ve yaklaşık `%32` alpha, Domain `offendingCells` içindeki her hücrede iki diyagonal bardan oluşan X vardır. Bar kalınlığı `0.08` cell, opacity yaklaşık `%85`'tir. X, renkten bağımsız okunmalıdır. Shake kullanılmaz. Pointer Up/Cancel yalnız preview'i bitirir; gameplay command çalıştırmaz. Valid outline, ZT-012 insan görsel incelemesi sonrası açık karardır.

ZT-012 performans kararı: preview, aynı `PlacementValidator` kurallarını kullanan caller-owned scratch ile allocation-free validation yapabilir. Mevcut `Validate` davranışı korunur; non-alloc yol parity-test edilir.

Pointer interaction presentation seviyesinde:

- lift,
- drag,
- release,
- cancel

davranışlarını tetikleyebilir.

Ancak authoritative gameplay state yalnız Application/Domain tarafından kabul edilen command sonucunda değişir.

Visual drag position authoritative item position değildir.

## Cancel Policy

Pointer cancel olduğunda:

- geçici visual drag state temizlenir,
- authoritative state korunur,
- kabul edilmemiş geçici placement commit edilmez.

## Protected Rules

- Mouse ve touch için ayrı gameplay rule set'i yazılmaz.
- Presentation layer placement legality hesaplamaz.
- Multi-touch aynı anda iki item'ı manipüle etmez.
- İkinci pointer mevcut interaction state'ini değiştirmez.
- Grid projection sonucu integer anchor'dır.
- World-space float değerler Domain'e gameplay state olarak aktarılmaz.

## Tests

En az şu davranışlar test edilir:

- mouse down/move/up aynı pointer flow'u kullanır
- touch down/move/up aynı pointer flow'u kullanır
- ikinci touch active drag sırasında yok sayılır
- pointer cancel visual state'i temizler
- grid projector sınır değerleri
- projector legality hesaplamaz
- input kaynağı değişse de aynı semantic event sequence oluşur

## Consequences

Artıları:

- Editor testleri mouse ile hızlı yapılabilir.
- Mobile davranışıyla desktop test davranışı aynı kalır.
- Input sistemi gameplay kurallarından ayrılır.
- Multi-touch kaynaklı edge-case sayısı azalır.
- Otomatik PlayMode testleri kolaylaşır.

Bedeli:

- Phase A'da gerçek multi-touch interaction desteklenmez.
- Input-to-grid adapter katmanı ayrıca test edilmek zorundadır.
