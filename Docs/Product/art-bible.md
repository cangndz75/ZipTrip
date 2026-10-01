# ZipTrip — MG-1 mini art bible

**Yön:** Adult-first, global-first; 3D, toy-like, tactile, rounded, colorful ve polished. Temiz ışık, güçlü malzeme ayrımı ve ilk bakışta okunur siluetler. Preschool görünüm, gameplay alanını örten dekor ve küçük süsleme metinleri kullanılmaz.

**Kamera ve ölçek:** ADR-0002'deki sabit orthographic 75° kamera. Board XZ düzleminde; 1 logical cell = 1 Unity world unit. 360 genişlikli portrait viewport'ta yaklaşık Cabin 53, Backpack 62 ekran birimi/hücre. Bunlar fiziksel piksel kabul değeri değildir.

**Işık:** Ekranın sol üstünden geniş, yumuşak key; karşı yönden nötr fill. Görsel key/fill ilişkisi yaklaşık 2.5:1. Kısa, yumuşak temas gölgeleri; gölgeler occupied cell gibi okunmamalı. İlk sandbox için aynı yönü tekrar üreten basit Directional Light yeterlidir.

**Arka plan:** Sıcak, düşük kontrastlı stone/off-white (`#E9E5DD` referansı). Bu gameplay çalışma arka planıdır, marka kimliği kararı değildir.

**Palet:** Deep blue-green `#31515D`, teal `#4E9FA2`, terracotta `#C36F58`, mustard `#D7AA58` ilk malzeme ailesidir; her item'ın bu renklerden birini kullanması zorunlu değildir. Orta doygunluk, net siluet ve malzeme ayrımı önceliklidir.

**Malzeme:** Çoğunlukla mat/soft-touch. Kumaş sert objeden daha yumuşak okunur; fermuarda daha sonra ölçülü metal vurgu kullanılabilir. Footprint sınırlarını gizleyen sert yansıma ve yoğun mikro doku kullanılmaz.

**Detay:** Siluet > yüzey detayı. Gameplay ölçeğinde minik yazı, etiket veya dekoratif desen gerekmez. Dekoratif/bevel görsel taşma hedefi MG-1 doğrulamasından önce en çok `0.1` cell'dir; bu gameplay collision kuralı değildir.
