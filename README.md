# BangleMe

iOS AR uygulaması — kullanıcı telefonun kamerasını koluna tutar, alttan karuselden altın bilezik seçer ve bileğinde hiper-gerçekçi şekilde görür. Sosyal medya filtresi karakterinde bir eğlence uygulaması.

## Durum

Planlama aşaması. Kod henüz yazılmadı. Tasarım dokümanı ve ilk uygulama planı hazır.

## Tech Stack

- Swift 5.9 + SwiftUI
- AVFoundation (kamera)
- Vision framework (bilek/el algılama)
- RealityKit + Reality Composer Pro (3D render, PBR materyal, IBL)
- ReplayKit (video kaydı)
- Hedef: iOS 16+, iPhone 12 ve üstü

## Klasör Yapısı

```
BangleMe/
├── README.md                          (bu dosya)
├── CLAUDE.md                          (Claude oturumları için proje rehberi)
├── .gitignore
├── docs/
│   └── superpowers/
│       ├── specs/
│       │   └── 2026-05-11-bangleme-design.md       (tasarım dokümanı)
│       └── plans/
│           └── 2026-05-11-bangleme-foundation.md   (Plan 1: kamera + bilek tracking)
└── BangleMe/                          (Xcode projesi — Plan 1 Task 1'de yaratılacak)
    └── BangleMe.xcodeproj
```

## Geliştirme Yol Haritası

| # | Plan | Çıktı |
|---|---|---|
| 1 | Foundation: Kamera + Bilek Tracking | Bileğin üstüne yapışan debug küresi |
| 2 | RealityKit + Bilezik Render | Tek altın bilezik bileğin üstünde |
| 3 | Stack + Material Variants | Çoklu bilezik, altın/gümüş/rose seçenekleri |
| 4 | UI (karusel + sheet'ler) | Tam kullanıcı arayüzü |
| 5 | Fizik Sistemi | Kayma, dönme, çarpışma |
| 6 | Kayıt + Paylaşım + Polish | Foto/video, share, App Store hazır |

Her plan kendi başına çalışan bir milestone üretir. Plan 1 → Plan 6 sıralı geliştirilir.

## Başlamak İçin

1. Xcode 15+ kur
2. `docs/superpowers/plans/2026-05-11-bangleme-foundation.md` dosyasını aç
3. Task 1'den başla (Xcode projesi yaratma)
4. Her task TDD adımlarıyla yazılmış — sırayla takip et
5. Task 11 sonunda telefonda çalışan bir wrist tracker olacak

## Lisans / Attribution

Başlangıç bilezik modeli:
- [Gold Bracelet](https://sketchfab.com/3d-models/gold-bracelet-dd2c51b6a90345a9b062e7a9961c1db7) by **Tahir.Muhamad.Ajmal** — CC Attribution 4.0
- Attribution app içi "Credits" ekranında gösterilecek
