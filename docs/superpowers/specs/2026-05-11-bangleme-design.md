# BangleMe — Tasarım Dokümanı

**Tarih:** 2026-05-11
**Durum:** Onaylandı (brainstorming → planlama aşamasına geçiş)
**Platform:** iOS 16+
**Tip:** Native iOS uygulaması (Swift)

---

## 1. Vizyon

BangleMe, iOS native bir AR (artırılmış gerçeklik) uygulamasıdır. Kullanıcı kamerasını koluna tutar, alttan karuselden altın bilezik seçer ve bileğinde gerçek zamanlı, hiper-gerçekçi şekilde görür. Birden fazla bilezik üst üste takabilir, materyal/boyut değiştirebilir, kol oynayınca bilezikler doğal şekilde kayar (fizik). Video/foto kaydedip TikTok ve Instagram'a tek tıkla paylaşır.

**Konum:** Sosyal medya filtresi karakterinde bir eğlence uygulaması. Hiper-gerçekçi altın görüntüsü ile diğer AR oyuncak filtrelerinden ayrışır.

**Hedef kullanıcı:** 18-35 yaş, sosyal medya aktif, aksesuar/takı seven kişiler.

**Minimum cihaz:** iPhone 12 ve üstü (60fps hedef için iPhone 13+).

**Başlangıç içeriği:** Tek bir altın bilezik modeli — [Sketchfab — "Gold Bracelet" by Tahir.Muhamad.Ajmal](https://sketchfab.com/3d-models/gold-bracelet-dd2c51b6a90345a9b062e7a9961c1db7), CC Attribution 4.0 lisanslı. Attribution app içi "Credits" ekranında gösterilecek.

---

## 2. Mimari

### 2.1 Yüksek Seviye

```
┌─────────────────────────────────────────────────────────────┐
│                     SwiftUI Katmanı                         │
│   Karusel UI · Materyal picker · Boyut slider · Kayıt       │
└───────────────────────────┬─────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────┐
│                  ARSession Coordinator                      │
│         AVFoundation kamera · ARKit world tracking          │
└─────────┬────────────────────────────────────┬──────────────┘
          │ kamera frame'leri                  │
          ▼                                    ▼
┌─────────────────────┐               ┌────────────────────────┐
│  Wrist Tracker      │               │  RealityKit Sahne      │
│  Vision/MediaPipe   │──poses──────▶│  Bilezik 3D modelleri  │
│  21 hand keypoint   │  60fps        │  PBR materyal · IBL    │
│  → 3D wrist pose    │               │  Fizik simülasyonu     │
└─────────────────────┘               └────────────┬───────────┘
                                                   │
                                      ┌────────────▼───────────┐
                                      │  ReplayKit Kayıt       │
                                      │  Video/foto + paylaşım │
                                      └────────────────────────┘
```

### 2.2 Modüller

| Modül | Sorumluluğu | Bağımlılıkları |
|---|---|---|
| `CameraSession` | AVFoundation kamera akışı | — |
| `WristTracker` | Bilek 2D→3D pose çıkarımı | CameraSession frame |
| `BraceletScene` | RealityKit sahne, model yükleme, materyal | WristTracker pose |
| `BraceletCatalog` | .usdz model + materyal varyantları | — |
| `PhysicsController` | Bilezik kayma/dönme simülasyonu | BraceletScene |
| `RecordingService` | ReplayKit ile video/foto kaydı | tüm scene |
| `ShareService` | iOS share sheet (Instagram/TikTok hedefli) | RecordingService |
| `UIRoot` | SwiftUI ekranı, karusel, ayar paneli | BraceletCatalog |

Her modül kendi protokolü arkasında — biri değiştiğinde diğerleri etkilenmez, mock'larla test edilebilir.

### 2.3 Teknoloji Stack'i

- **Swift 5.9 + SwiftUI** — UI
- **AVFoundation** — kamera akışı
- **Vision framework** — birincil el algılama (`VNDetectHumanHandPoseRequest`)
- **MediaPipe Hand Landmarker** — yedek hand tracker (entegrasyon hazır, kalite kıyaslamasından sonra karar)
- **RealityKit + Reality Composer Pro** — 3D render, PBR materyal, IBL
- **ReplayKit** — video kaydı
- **AVCapturePhotoOutput** — foto çekimi

---

## 3. Bilek Takibi Pipeline'ı

### 3.1 Veri Akışı

```
Kamera frame (60fps)
   ↓
1. El algılama (Vision VNDetectHumanHandPose) → 21 keypoint 2D
   ↓
2. Bilek 3D pozisyonu (pinhole + avuç genişliği referansı)
   ↓
3. Bilek oryantasyonu (palm normal + önkol yönü → quaternion)
   ↓
4. One Euro Filter (jitter azaltma)
   ↓
BraceletScene → her frame pose güncellemesi
```

### 3.2 Kritik Teknik Kararlar

**Vision birincil, MediaPipe yedek.** Vision iOS native, sıfır setup, donanım hızlandırmalı; MediaPipe daha iyi rotasyon doğruluğu sunabilir. MVP'nin son haftasında yan yana test edilip kazanan production'a alınır.

**Derinlik tahmini:** Sıradan kamerada derinlik yok. Stratejiler:
- LiDAR varsa otomatik kullan (Pro modeller, ~30% pazar)
- Yoksa: ortalama avuç genişliği (~7.5cm) + pinhole kamera formülü
- Kullanıcı bazlı kalibrasyon "Boyut slider"ı zaten bu hatayı kompanze ediyor

**One Euro Filter** AR endüstrisi standardı — hızlı harekette düşük lag, durağanda titreme yok. ~100 satır Swift implementasyonu.

**Tracking kaybı:** Pose son bilinen yerde 300ms fade-out; 1sn'de el dönmezse bilezik gizlenir, tooltip "Bileğini kameraya göster". Geri gelince 200ms fade-in.

**İki el desteği:** Vision aynı anda iki ele kadar algılar. Her bilek bağımsız pose stream'i — sol ve sağ kola farklı bilezikler atanabilir.

### 3.3 Performans Hedefi

| Metrik | Hedef |
|---|---|
| Tracking gecikmesi | <50ms (frame → ekran) |
| Frame rate | 60fps (iPhone 13+), 30fps (iPhone 12) |
| Pil tüketimi | 5dk kullanım → max %3 batarya |
| Bilek konum doğruluğu | ±5mm sapma içinde |

---

## 4. Asset Pipeline ve Hiper-Gerçekçi Altın Render

### 4.1 Model Hazırlık Pipeline'ı

```
Sketchfab "Gold Bracelet" (18.1k tris, CC Attribution)
   ↓
Blender:
   • Decimate → 6-8k tris (stack için şart)
   • UV kontrol
   • Pivot point bilek merkezine
   ↓
Reality Composer Pro:
   • PBR materyal sıfırdan kur
   • Material variants: Altın / Gümüş / Rose / Mat
   • .usdz export (anchor: wrist)
   ↓
BangleMe.xcodeproj/Resources/bracelets/
   ├── classic_bangle.usdz
   └── classic_bangle_thumb.png
```

### 4.2 PBR Materyal — Hiper-Gerçekçi Altın

```swift
var goldMaterial = PhysicallyBasedMaterial()
goldMaterial.baseColor = .init(tint: UIColor(red: 0.83, green: 0.69, blue: 0.22, alpha: 1.0))
goldMaterial.metallic = 1.0
goldMaterial.roughness = 0.18
goldMaterial.clearcoat = 0.3
goldMaterial.clearcoatRoughness = 0.05
goldMaterial.anisotropy = 0.4  // fırçalanmış altın hissi (opsiyonel)
```

### 4.3 Material Variants

| Variant | Base Color | Roughness | Clearcoat |
|---|---|---|---|
| Sarı Altın | `#D4B038` | 0.18 | 0.30 |
| Beyaz Altın / Gümüş | `#E8E8E8` | 0.15 | 0.25 |
| Rose Altın | `#E0B0A0` | 0.20 | 0.30 |
| Mat Altın | `#C9A833` | 0.55 | 0.00 |

### 4.4 IBL (Image-Based Lighting)

İki ışıklandırma kaynağı:
1. **Studio HDRI (1024px, ~500KB gömülü)** — mücevher fotoğrafı stüdyo ışığı
2. **Canlı kamera HDRI** — kameranın gördüğü ortamı düşük rezolüsyonda environment map olarak kullan → bilezik gerçek ortamı yansıtır

### 4.5 Stack Sistemi

| Stack Sırası | Önkol Ekseninde Offset |
|---|---|
| 1. bilezik | 0 mm (bilek merkezi) |
| 2. bilezik | +8 mm |
| 3. bilezik | +16 mm |
| 4. bilezik | -8 mm |
| 5. bilezik | -16 mm |

Maksimum 5 bilezik (performans + ergonomi limiti). Her bileziğin kendi materyalini seçebilirsin.

### 4.6 Boyut Slider

Kullanıcı `0.85x – 1.15x` arası bileziği ölçekler. İnce ve kalın bilekler için.

### 4.7 Performans Bütçesi

| Kalem | Limit |
|---|---|
| Tek bilezik | ≤8k tris, 1024px texture |
| Maks stack (5 bilezik) | ≤40k tris toplam |
| Texture memory | <20MB toplam |
| Materyal varyant değişimi | GPU shader-side, anlık |

---

## 5. Fizik Sistemi

### 5.1 Hedef Davranışlar

| Senaryo | Doğal Davranış |
|---|---|
| Kol yatay hareket | Bilezik rijit takip |
| Kol hızla sallanır | Bilezik momentumla biraz kayar, dengeye gelir |
| Bilek döner | Bilezik bir tık geç döner (atalet) |
| Kol yana çevrilir | Bilezik yerçekimiyle biraz döner |
| Stack 3+ bilezik | Bilezikler birbirine çarpar, iç içe geçmez |

### 5.2 Spring-Damper Yaklaşımı

RealityKit tam fizik motoru bu use case için overkill. Her bilezik için özel spring-damper integrator — %95 doğal his, sıfır performans maliyeti.

```swift
struct BraceletDynamics {
    var position: SIMD3<Float>
    var velocity: SIMD3<Float>
    let stiffness: Float = 180.0
    let damping: Float = 18.0

    mutating func update(restPose: SIMD3<Float>, dt: Float) {
        let displacement = restPose - position
        let springForce = displacement * stiffness
        let dampingForce = -velocity * damping
        let acceleration = springForce + dampingForce
        velocity += acceleration * dt
        position += velocity * dt
    }
}
```

Rotasyon için aynı mantık quaternion + SLERP + angular velocity ile.

### 5.3 Yerçekimi Etkisi

Önkol yönünün dünya Y eksenine göre açısı hesaplanır, max 5mm offset uygulanır → kol yatayken bilezik bilekte hafif "asılı" durur.

### 5.4 Stack Collision

Her bilezik için **kapsül collision shape** (gerçek mesh değil — basit). Adjacent bilezikler arasındaki mesafe yarı eksenler toplamından küçükse iter. O(n²) ama n=5 max → maliyetsiz.

### 5.5 Tuning Parametreleri

| Parametre | Aralık | Etkisi |
|---|---|---|
| `stiffness` | 100-300 | "Gevşek" vs "sıkı oturmuş" |
| `damping` | 10-30 | Sallanma vs anında durma |
| `gravity_influence` | 0-1 | Yerçekimi etkisi gücü |
| `rotational_lag` | 0-0.3 sn | Bilek dönüşüne lag süresi |

Geliştirme sırasında debug ekranında slider'lar; production'da sabit.

### 5.6 Stress Testleri (MVP öncesi zorunlu)

1. Yavaş el sallama → akıcı kayma
2. Hızlı kol döndürme (1sn'de 360°) → bilezik fırlayıp gitmemeli
3. Stack tam dolu (5 bilezik) → çarpışma doğal mı
4. Bilek kayma → el geometrisini geçmemeli (Vision el sınırları collision wall olarak kullanılır)

### 5.7 Performans

| Kalem | Maliyet |
|---|---|
| 5 bilezik × spring-damper | ~0.02ms/frame |
| Collision check | ~0.05ms/frame |
| **Toplam fizik bütçesi** | <0.5ms (frame bütçesinin %3'ü) |

---

## 6. UI ve Akış

### 6.1 Ekranlar

**Onboarding (sadece ilk açılış):**
1. Hoşgeldin ekranı
2. 3 kart: nasıl çalışır
3. Kamera izni iste
4. CTA: "Bileğini göster"

**Ana Kamera Ekranı (HOME):**
- Tam ekran kamera + AR bilezik render
- Alt karusel: bilezik thumbnail'ları yatay kayar (Snapchat/IG filtre tarzı)
- Üst-sol: ⚙ ayarlar
- Alt-orta: 📷 foto · ⬤ kayıt · 🎬 video · ✨ sticker
- Tracking kaybı durumunda merkez üstte tooltip

**Bilezik Detay Sheet (uzun bas):**
- Materyal: Altın / Beyaz / Rose / Mat
- Boyut slider: 0.85x–1.15x
- Hangi bileğe: Sol / Sağ / İkisi
- Sil / Çoğalt

### 6.2 Etkileşim Kuralları

| UI Olayı | Davranış |
|---|---|
| Karuselde tık | Aktif bileğe ekle (stack varsa son sıra) |
| Karuselde uzun bas | Detay sheet aç |
| Çift tık bileziğe | Hızlı sil |
| Karusel swipe sola | Stack'i sıfırla (onayla) |
| Tracking kaybı | 300ms fade-out + tooltip |
| Tracking dönüşü | 200ms fade-in |
| Foto/video butonu | Anında çek, share sheet otomatik |

### 6.3 Tema
- **Karanlık tema** zorunlu (kamera bozulmasın, parlama yok)
- Aksan: altın sarısı `#D4B038`
- Tipografi: SF Pro Display (iOS native)

---

## 7. Kayıt ve Paylaşım

| Özellik | Teknoloji | Detay |
|---|---|---|
| Foto | `AVCapturePhotoOutput` + composited overlay | 4K destek |
| Video | `RPScreenRecorder` (ReplayKit) | 1080p@60fps, max 60sn |
| Paylaşım | `UIActivityViewController` | Instagram, TikTok, Mesaj, Kaydet için kısayollar |
| Watermark | Köşede küçük "BangleMe" logosu | App ayarından kapatılabilir |
| Sticker modu | Foto sonrası overlay | 8-10 hazır: emoji, "altın damlası" tarzı |

---

## 8. İzinler

| İzin | Ne zaman | Açıklama metni |
|---|---|---|
| Kamera | Onboarding sonunda | "Bileziği bileğinde görebilmen için kameranı kullanıyoruz" |
| Foto kütüphanesi | İlk kayıt anında | "Kayıtlarını telefonuna kaydetmek için" |
| Mikrofon | İlk video kaydında | "Videolarına ses eklemek için (opsiyonel)" |

Reddedilirse: "Ayarlardan açabilirsin" fallback link'i.

---

## 9. Geliştirme Timeline (6-10 hafta)

| Hafta | İş |
|---|---|
| 1 | Proje setup, kamera akışı, Vision el algılama POC |
| 2 | Bilek 3D pose pipeline, One Euro filter, ilk bilezik render |
| 3 | PBR materyal, IBL, hiper-gerçekçi altın tuning |
| 4 | Stack sistemi, multi-bracelet, material variants |
| 5 | UI — karusel, sheet, ayarlar |
| 6-7 | Fizik sistemi (spring-damper, çarpışma) — en riskli kısım |
| 8 | Kayıt/paylaşım/sticker |
| 9 | Tracking kaybı UX, polish, performans optimizasyonu |
| 10 | TestFlight beta, son düzeltmeler, App Store submit |

---

## 10. Riskler ve Çözümler

| Risk | Olasılık | Etki | Çözüm |
|---|---|---|---|
| Vision el algılama yetersiz | Orta | Yüksek | MediaPipe yedek hazır |
| Fizik tuning uzar | Yüksek | Orta | 2 hafta buffer, gerekirse Faz 2'ye |
| Düşük cihazda FPS düşer | Orta | Orta | Adaptive quality: iPhone 12'de stack max 3, tris düşür |
| App Store reddi (kamera) | Düşük | Yüksek | Privacy nutrition labels eksiksiz, açıklama metinleri net |
| 3D model lisans ihlali | Düşük | Yüksek | Sketchfab modeli için Credits ekranında attribution |

---

## 11. Kapsam Dışı (Faz 2+)

- Kullanıcı kendi 3D model yükleme
- Backend / hesap sistemi
- E-ticaret entegrasyonu
- Android sürüm
- Yüzük, küpe, kolye gibi diğer takılar
- AI ile özel tasarım üretimi (Meshy entegrasyonu)
- Topluluk paylaşım galerisi

---

## 12. Başarı Kriterleri (MVP Launch)

- App Store onayı ilk denemede
- iPhone 13+: stabil 60fps, 5 bilezik stack
- iPhone 12: stabil 30fps, 3 bilezik stack
- Bilek tracking doğruluğu: ±5mm
- 5 dakika kullanım: max %3 pil
- TestFlight beta: 20+ kullanıcı, çoğunluk "tracking inandırıcı" feedback'i

---

**Onay Zinciri:**
- Brainstorming → Bu doküman (2026-05-11) → Implementation plan (writing-plans skill)
