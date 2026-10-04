# ZipTrip — Art bible (ADR-0007 güncellemesi)

> **Durum:** ADR-0007 (Visual Direction Lock) kapsamında öneri; insan review'ü bekliyor. MG-1 mini art bible'ın yerine geçer. Görsel tavan artık "orta doygunluk" değil; hedef mockup'ların görsel kalitesidir. Gameplay kontratı değişmez.

**Yön:** ZipTrip, canlı bir premium-casual seyahat-bulmaca estetiğini hedefler: dokunsal, zengin malzemeli, telefon ölçeğinde çok okunur, sıcak ve heves uyandıran. Adult-first ve global-first kalır. 3D, rounded ve polished.

- **Kaçınılacaklar (Blueprint §16, aynen geçerli):**
  - preschool görünüm
  - gameplay alanını örten karakter veya dekor
  - büyük gameplay logosu
  - child-like UI
- **Canlı ≠ çocuksu:**
  - eşyalarda yüz veya göz yok
  - bebek pasteli yok
  - kalın çizgi-roman outline'ı yok
  - doygunluk gerçek malzeme hissiyle birlikte gelir

**Kamera ve ölçek:** ADR-0002'deki sabit orthographic 75° kamera.
- Board XZ düzleminde; 1 logical cell = 1 Unity world unit.
- 360 genişlikli portrait viewport'ta yaklaşık Cabin 53, Backpack 62 ekran birimi/hücre.
- Bunlar fiziksel piksel kabul değeri değildir. Kabul kanıtı yalnız native 1080×2340 capture'dır (ADR-0007 Gate G).

**Işık:** Ekranın sol üstünden geniş, sıcak key; karşı yönden nötr fill. Key/fill yaklaşık 2.5:1.
- Temas gölgeleri ve AO MG-1'e göre daha belirgin olabilir. Eşyalar astara "oturmuş" görünmeli ve derinlik okunmalı.
- Gölgeler hâlâ occupied cell gibi okunmamalı.
- Rim/specular vurgusu deri, plastik ve metalde malzeme ayrımı için kullanılabilir.

**Arka plan:** Destinasyon fantezisi taşıyabilir: güneşli flat-lay, seyahat objeleri, destinasyon motifleri.
- Hiyerarşi kuralı kesindir: arka plan ve prop'lar hiçbir zaman puzzle'dan yüksek kontrast almaz (ADR-0007 Gate C).
- Bavul ana obje kalır (Gate D).

## Palet token'ları

Değerler AI mockup'tan RGB kopyası değildir. Başlangıç token'larıdır ve in-engine Golden Lv1 referans capture'ında ayarlanır (ADR-0007 Karar 5–6).

| Token | Değer | Kullanım |
|---|---|---|
| `ink` | `#22313A` | Başlık ve gövde metni |
| `ink-muted` | `#6E6A62` | İkincil metin |
| `paper` | `#FFF9EE` | Kart yüzeyi |
| `cream` | `#F6ECDA` | Kart zemini, buton dolgusu |
| `aegean` | `#2B6CD4` | Seyahat mavisi: vurgulu kural kelimesi, numara rozeti, seçim |
| `teal` | `#1E9EA3` | Sağlanan kural (✓), kimlik |
| `coral` | `#EC6A4A` | Sıcak vurgu, ihlal uyarısı (yalnız renkle değil: Δ-12) |
| `sun` | `#F3B23A` | Sıcak vurgu, bağlamsal aksiyon (Döndür) |
| `passport-red` | `#9C2230` | Pasaport ve derin kırmızı malzemeler |
| `cognac` | `#A65A2A` | Deri, kemer, sap |
| `brass` | `#D6A64A` | Metal donanım, fermuar çekeceği |
| `hero-green` | `#3DBA3F` → under-edge `#24892B` | **Yalnız** Packed sonrası "Sonraki" (ADR-0007 Karar 3) |

Kurallar:
- Aynı anda ekranda olan komşu eşyalar en az ton veya parlaklık farkıyla ayrışır. Aynı tonlu iki eşya yan yana silüet kaybetmemeli.
- Saf primer renk (`#FF0000` gibi) kullanılmaz; doygunluk malzeme gölgelemesiyle gelir.
- `hero-green` aktif oyun sırasında hiçbir yerde kullanılmaz.

## Malzeme

Malzeme ailesi telefon ölçeğinde ayrışmalıdır (Gate B): en az base colour farkı ve roughness ya da derinlik farkı.

| Malzeme | Okuma |
|---|---|
| Kumaş (havlu, kazak) | Yumuşak puf gölgeleme, terry/örgü dokusu, yüksek roughness, kenarda hafif dalga |
| Deri (bavul, çanta) | Orta roughness, dikiş izi, kenar aşınması, hafif specular |
| Plastik (şampuan, gözlük çerçevesi) | Düşük roughness, net specular highlight, düz renk alanları |
| Kâğıt / karton (pasaport sayfası, etiket) | Mat, ince kenar kalınlığı |
| Metal (fermuar, menteşe, gözlük teli) | Brass/krom; küçük ve kontrollü parlama |
| Cam / şeffaf (gözlük camı, şeffaf poşet) | Koyu tonlu, yansıma ile okunur; arkasındaki footprint'i gizlemez |

Footprint sınırlarını gizleyen sert yansıma ve footprint'i bulandıran yoğun mikro doku hâlâ kullanılmaz.

## Detay

- Siluet > yüzey detayı, ama hedef seviye MG-1'den zengindir.
- Pasaport kapağındaki amblem, şampuan etiketi, havlu şeritleri gibi tanınırlığı artıran detaylar teşvik edilir. Bunlar okunur ölçekte tasarlanır, minik yazı olarak değil.
- Dekoratif/bevel görsel taşma hedefi en çok `0.1` cell'dir. Bu gameplay collision kuralı değildir.

## Onaylı varlıklar

ADR-0007 Karar 4 geçerlidir:
- Bavul, iç kısım (interior), kazak ve UI-SLICE-01.1'in geometrisi, etkileşimi ve layout'u kilitlidir.
- Malzeme, renk ve ışık ayarı yapılabilir.
- ART-GATE-02B eşyaları doğrudan bu dokümana göre üretilir.
