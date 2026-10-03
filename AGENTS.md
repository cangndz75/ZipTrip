# ZipTrip — Agent Instructions

## Authority

ZipTrip'in ürün ve teknik kararlarında tek source of truth:

1. Blueprint v1.1
2. Blueprint v1.2 Delta

Delta'nın yetkilendirdiği kabul edilmiş ADR'ler bu kaynakların parçasıdır. Dinamik packing gameplay kontratı için implementation authority `Docs/ADR/ADR-0006-dynamic-packing-puzzle-contract.md`'dir (Δ-21).

`ZipTrip_Phase_A_Execution_Plan.md` yalnızca execution order'dır.
Phase A ticket'ları yalnızca Blueprint + Delta kararlarını uygular.

Çelişki halinde:
Blueprint / Delta > Execution Plan > Ticket > implementation assumption.

Agent eksik bir kararı kendi başına icat etmez.

---

## Phase A Scope Lock

Phase A dışında:

- meta progression
- economy
- monetization
- analytics stack
- live ops
- geniş onboarding
- level map
- account sistemi

Ticket açıkça istemiyorsa bunlara dokunma.

---

## Required Task Protocol

Her task'ta şu sıra zorunludur:

1. AGENTS.md'yi oku.
2. İlgili ADR ve canonical dokümanları oku.
3. Mevcut kodu incele.
4. Kısa implementation planı çıkar.
5. Değişecek dosyaları belirt.
6. En küçük doğru değişikliği uygula.
7. İlgili testleri yaz/çalıştır.
8. Unity compile ve Console durumunu kontrol et.
9. Ne değiştiğini kanıtlarıyla özetle.
10. İnsan kabulünden önce commit atma.

---

## Protected Architecture Rules

- `ZipTrip.Domain` UnityEngine'e referans vermez.
- `ZipTrip.Application` UnityEngine'e referans vermez.
- Domain deterministic olmalıdır.
- Gameplay legality yalnız Domain/Application kurallarından gelir.
- Presentation katmanı authoritative gameplay state'i doğrudan değiştirmez.
- Solver runtime ile aynı gameplay kurallarını kullanır.
- Float tabanlı görsel hesaplar Domain grid kurallarına sızmaz.
- Rejected command state'i değiştirmez.
- Her gameplay kuralı için en az bir pozitif ve bir negatif test bulunur.
- "Exception oluşmadı" tek başına yeterli test değildir.

---

## Change Boundaries

Ticket'ta açıkça belirtilmeyen:

- scene değişikliği
- prefab değişikliği
- package değişikliği
- mimari refactor
- yeni gameplay mechanic

yapılmaz.

Package ekleme veya package güncelleme Level D değişikliktir ve önce insan onayı gerekir.

Permission ladder'ın kalan seviyeleri canonical Blueprint v1.1 kaynağı repo'ya eklendiğinde buraya birebir aktarılacaktır. O zamana kadar belirsiz permission seviyelerinde insan onayı iste.

---

## Engineering Style

- Smallest correct change.
- Deterministic behavior.
- Explicit dependencies.
- Test before presentation polish.
- Existing architecture over speculative abstraction.
- No silent scope expansion.
- No duplicated gameplay rules in Unity presentation code.
- No hidden fallback behavior that changes game rules.

---

## Git

- İnsan kabulünden önce commit atma.
- Generated Unity folders commit edilmez.
- Binary/art asset kuralları `.gitattributes` ve Git LFS üzerinden uygulanır.
- Ticket dışında toplu formatting veya unrelated cleanup yapılmaz.