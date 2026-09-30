# ADR-0002 — Fixed Gameplay Camera

## Status

Accepted

## Context

ZipTrip mobile-first bir spatial puzzle oyunudur.

Oyuncunun temel görevi item şekillerini, container sınırlarını, boş alanları ve diğer item'larla ilişkileri hızlı ve doğru okuyabilmektir.

Serbest kamera kontrolü puzzle okunabilirliğini azaltabilir, ek input karmaşıklığı yaratabilir ve hidden-grid ile görsel temsil arasındaki ilişkiyi bozabilir.

## Decision

Ana gameplay sırasında kamera fixed olacaktır.

Oyuncu:

- kamerayı serbestçe döndüremez,
- orbit yapamaz,
- kamerayı manuel pan edemez,
- gameplay amacıyla manuel zoom kullanmaz.

Camera framing cihaz aspect ratio'suna göre sistem tarafından ayarlanabilir ancak oyuncunun puzzle state'ini algıladığı temel bakış açısı değişmez.

Kamera:

- container'ın tamamını okunabilir biçimde göstermelidir,
- item footprint'lerinin görsel olarak karşılaştırılmasını kolaylaştırmalıdır,
- portrait mobile kullanımına göre tasarlanmalıdır,
- gameplay legality veya grid hesaplamasına etki etmemelidir.

## Numerical Parameters

Kesin:

- projection tipi,
- camera angle,
- camera distance,
- FOV / orthographic size,
- container framing,
- hücre başına hedef ekran boyutu

ZA-001 Mini Art Bible ve golden readability çalışması sırasında ölçülerek belirlenecektir.

Bu değerler doğrulanmadan tahmin edilerek production kararı haline getirilmez.

## Aspect Ratio Policy

Gameplay board şu hedef ekran oranlarında tamamen görünür kalmalıdır:

- 16:9
- 19.5:9
- 20:9

Aspect değişikliği camera framing'i değiştirebilir ancak canonical gameplay grid veya item state'ini değiştirmez.

## Protected Rules

- Camera transform gameplay state değildir.
- Camera hareketi puzzle çözümünün parçası değildir.
- Presentation, kameraya göre gameplay legality üretmez.
- Hidden-grid kuralları camera projection'dan bağımsızdır.
- Item okunabilirliği kamera hareketi gerektirmeden sağlanmalıdır.

## Consequences

Artıları:

- Puzzle state daha hızlı okunur.
- Touch input daha basit kalır.
- Grid ile mesh ilişkisi daha tutarlı test edilir.
- Level tasarımında kamera kaynaklı belirsizlik azalır.
- Capture ve creative üretimi daha tekrarlanabilir olur.

Bedeli:

- Kamera tek başına okunabilirlik problemlerini gizleyemez.
- Container ve item asset'leri seçilen bakış açısında gerçekten iyi çalışmak zorundadır.
- Farklı aspect ratio'lar için framing sistemi gerekir.
