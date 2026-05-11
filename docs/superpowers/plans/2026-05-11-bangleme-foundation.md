# BangleMe Foundation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working iOS app skeleton that opens the camera, detects the wrist in real time using Apple's Vision framework, and draws a debug sphere on the wrist. This is the foundation everything else (bracelet rendering, stack, physics, UI) is built on.

**Architecture:** Native iOS Swift app. AVFoundation for camera, Vision for hand detection, custom pose math (2D→3D + One Euro filter) in Swift. SwiftUI for the app shell. RealityKit/PBR added in Plan 2.

**Tech Stack:** Swift 5.9, SwiftUI, AVFoundation, Vision, XCTest. Target: iOS 16+.

**Testing strategy:** Pure logic (pose math, One Euro filter, depth estimation) → XCTest in simulator. Camera + Vision integration → manual run-on-device verification (camera/Vision require a real device). Each task notes which testing mode applies.

---

## File Structure

```
BangleMe/
├── BangleMe.xcodeproj
├── BangleMe/
│   ├── App/
│   │   └── BangleMeApp.swift          # @main, app entry
│   ├── Camera/
│   │   ├── CameraSession.swift        # AVCaptureSession wrapper
│   │   └── CameraPreviewView.swift    # SwiftUI camera preview
│   ├── Tracking/
│   │   ├── HandPoseDetector.swift     # Vision wrapper
│   │   ├── WristPose.swift            # data type
│   │   ├── WristPoseEstimator.swift   # 2D keypoints → 3D pose
│   │   ├── OneEuroFilter.swift        # smoothing filter
│   │   └── TrackingState.swift        # tracked / lost / fading
│   ├── Debug/
│   │   └── DebugSphereOverlay.swift   # visual marker on wrist
│   ├── UI/
│   │   └── ContentView.swift          # root view
│   └── Info.plist
└── BangleMeTests/
    ├── WristPoseEstimatorTests.swift
    ├── OneEuroFilterTests.swift
    └── HandPoseDetectorTests.swift    # uses fixture image
```

---

## Task 1: Create Xcode project

**Files:**
- Create: `BangleMe.xcodeproj` (via Xcode UI, not file)
- Create: `BangleMe/App/BangleMeApp.swift`
- Create: `BangleMe/UI/ContentView.swift`

- [ ] **Step 1: Create new Xcode project**

In Xcode → File → New → Project → iOS App. Settings:
- Product Name: `BangleMe`
- Interface: SwiftUI
- Language: Swift
- Storage: None
- Bundle Identifier: `com.bangleme.app` (or your own)
- Save to: `/Users/berke.altiparmak/Documents/BangleMe/`
- Deployment target: iOS 16.0

- [ ] **Step 2: Verify default project runs**

Cmd+R in simulator. Expected: blank screen with "Hello, world!" SwiftUI default.

- [ ] **Step 3: Create folder groups in project**

In Xcode Project Navigator, right-click `BangleMe` → New Group: `App`, `Camera`, `Tracking`, `Debug`, `UI`.
Move `BangleMeApp.swift` → `App/`, `ContentView.swift` → `UI/`.

- [ ] **Step 4: Initialize git repo and first commit**

```bash
cd /Users/berke.altiparmak/Documents/BangleMe
git init
git add .
git commit -m "feat: initial Xcode project scaffold"
```

---

## Task 2: Configure Info.plist permissions

**Files:**
- Modify: `BangleMe/Info.plist` (or target settings)

- [ ] **Step 1: Add camera usage description**

Xcode → Target → Info → Custom iOS Target Properties. Add row:
- Key: `NSCameraUsageDescription`
- Value: `Bileziği bileğinde görebilmen için kameranı kullanıyoruz`

- [ ] **Step 2: Add photo library usage description**

Add row:
- Key: `NSPhotoLibraryAddUsageDescription`
- Value: `Çektiğin foto ve videoları telefonuna kaydetmek için`

- [ ] **Step 3: Add microphone usage description**

Add row:
- Key: `NSMicrophoneUsageDescription`
- Value: `Videolarına ses eklemek için (opsiyonel)`

- [ ] **Step 4: Commit**

```bash
git add BangleMe.xcodeproj
git commit -m "feat: add camera, photo, microphone usage descriptions"
```

---

## Task 3: Define WristPose data type with tests

**Files:**
- Create: `BangleMe/Tracking/WristPose.swift`
- Create: `BangleMeTests/WristPoseTests.swift`

- [ ] **Step 1: Write the failing test**

`BangleMeTests/WristPoseTests.swift`:

```swift
import XCTest
import simd
@testable import BangleMe

final class WristPoseTests: XCTestCase {
    func test_identityPose_hasZeroPositionAndIdentityRotation() {
        let pose = WristPose.identity
        XCTAssertEqual(pose.position, SIMD3<Float>(0, 0, 0))
        XCTAssertEqual(pose.rotation, simd_quatf(ix: 0, iy: 0, iz: 0, r: 1))
        XCTAssertEqual(pose.confidence, 0)
    }

    func test_pose_storesPositionRotationAndConfidence() {
        let pose = WristPose(
            position: SIMD3<Float>(1, 2, 3),
            rotation: simd_quatf(angle: .pi / 2, axis: SIMD3<Float>(0, 1, 0)),
            confidence: 0.8
        )
        XCTAssertEqual(pose.position, SIMD3<Float>(1, 2, 3))
        XCTAssertEqual(pose.confidence, 0.8)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `Cmd+U` in Xcode, filter to `WristPoseTests`.
Expected: FAIL — "Cannot find 'WristPose' in scope".

- [ ] **Step 3: Implement WristPose**

`BangleMe/Tracking/WristPose.swift`:

```swift
import simd

/// Bilek 3D pozisyonu, oryantasyonu ve algılama güvenirliği.
public struct WristPose: Equatable {
    public let position: SIMD3<Float>       // metre cinsinden, kamera koordinatlarında
    public let rotation: simd_quatf
    public let confidence: Float            // 0..1

    public static let identity = WristPose(
        position: .zero,
        rotation: simd_quatf(ix: 0, iy: 0, iz: 0, r: 1),
        confidence: 0
    )

    public init(position: SIMD3<Float>, rotation: simd_quatf, confidence: Float) {
        self.position = position
        self.rotation = rotation
        self.confidence = confidence
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `Cmd+U`. Expected: 2/2 pass.

- [ ] **Step 5: Commit**

```bash
git add BangleMe/Tracking/WristPose.swift BangleMeTests/WristPoseTests.swift
git commit -m "feat: add WristPose data type"
```

---

## Task 4: Implement One Euro Filter with tests

**Files:**
- Create: `BangleMe/Tracking/OneEuroFilter.swift`
- Create: `BangleMeTests/OneEuroFilterTests.swift`

**Why this filter:** AR endüstrisi standardı. Düşük lag, durağanken titremez. ~80 satır Swift.

- [ ] **Step 1: Write failing tests**

`BangleMeTests/OneEuroFilterTests.swift`:

```swift
import XCTest
@testable import BangleMe

final class OneEuroFilterTests: XCTestCase {
    func test_firstSample_returnsItself() {
        var filter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
        let result = filter.filter(value: 5.0, timestamp: 0)
        XCTAssertEqual(result, 5.0, accuracy: 0.0001)
    }

    func test_constantSignal_remainsConstant() {
        var filter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
        _ = filter.filter(value: 10.0, timestamp: 0)
        let r1 = filter.filter(value: 10.0, timestamp: 0.016)
        let r2 = filter.filter(value: 10.0, timestamp: 0.032)
        XCTAssertEqual(r1, 10.0, accuracy: 0.01)
        XCTAssertEqual(r2, 10.0, accuracy: 0.01)
    }

    func test_noisySignal_isSmoothedAroundMean() {
        var filter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
        var t = 0.0
        var lastResult: Float = 0
        // Simulate 60Hz noisy signal oscillating ±0.1 around 5.0
        for i in 0..<30 {
            let noise: Float = i.isMultiple(of: 2) ? 0.1 : -0.1
            lastResult = filter.filter(value: 5.0 + noise, timestamp: t)
            t += 1.0 / 60.0
        }
        XCTAssertEqual(lastResult, 5.0, accuracy: 0.05)
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `Cmd+U`, filter `OneEuroFilterTests`. Expected: FAIL — "Cannot find 'OneEuroFilter' in scope".

- [ ] **Step 3: Implement OneEuroFilter**

`BangleMe/Tracking/OneEuroFilter.swift`:

```swift
import Foundation

/// One Euro Filter — düşük gecikmeli adaptif düşük-geçiş filtresi.
/// Referans: Casiez, Roussel, Vogel, "1€ Filter: A Simple Speed-based Low-pass
/// Filter for Noisy Input in Interactive Systems", CHI 2012.
public struct OneEuroFilter {
    public let minCutoff: Float
    public let beta: Float
    public let dCutoff: Float

    private var lastValue: Float = 0
    private var lastDerivative: Float = 0
    private var lastTimestamp: Double = -1

    public init(minCutoff: Float = 1.0, beta: Float = 0.007, dCutoff: Float = 1.0) {
        self.minCutoff = minCutoff
        self.beta = beta
        self.dCutoff = dCutoff
    }

    public mutating func filter(value: Float, timestamp: Double) -> Float {
        defer { lastTimestamp = timestamp }
        guard lastTimestamp >= 0 else {
            lastValue = value
            lastDerivative = 0
            return value
        }
        let dt = Float(timestamp - lastTimestamp)
        guard dt > 0 else { return lastValue }

        // Derivative
        let dv = (value - lastValue) / dt
        let dAlpha = smoothingAlpha(cutoff: dCutoff, dt: dt)
        let smoothedDerivative = dAlpha * dv + (1 - dAlpha) * lastDerivative

        // Adaptive cutoff
        let cutoff = minCutoff + beta * abs(smoothedDerivative)
        let alpha = smoothingAlpha(cutoff: cutoff, dt: dt)
        let smoothed = alpha * value + (1 - alpha) * lastValue

        lastValue = smoothed
        lastDerivative = smoothedDerivative
        return smoothed
    }

    private func smoothingAlpha(cutoff: Float, dt: Float) -> Float {
        let tau = 1.0 / (2 * .pi * cutoff)
        return 1.0 / (1.0 + tau / dt)
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `Cmd+U`. Expected: 3/3 pass.

- [ ] **Step 5: Commit**

```bash
git add BangleMe/Tracking/OneEuroFilter.swift BangleMeTests/OneEuroFilterTests.swift
git commit -m "feat: add One Euro Filter for pose smoothing"
```

---

## Task 5: Implement WristPoseEstimator (2D keypoints → 3D pose) with tests

**Files:**
- Create: `BangleMe/Tracking/WristPoseEstimator.swift`
- Create: `BangleMeTests/WristPoseEstimatorTests.swift`

**Algorithm:** Pinhole camera + average palm width (7.5cm reference) to estimate depth, then unproject wrist 2D keypoint to 3D.

- [ ] **Step 1: Write failing tests**

`BangleMeTests/WristPoseEstimatorTests.swift`:

```swift
import XCTest
import simd
@testable import BangleMe

final class WristPoseEstimatorTests: XCTestCase {
    let estimator = WristPoseEstimator(
        focalLengthPx: 1000,
        imageSize: CGSize(width: 1920, height: 1080),
        avgPalmWidthMeters: 0.075
    )

    func test_handCentered_andPalmFillsTenthOfImageWidth_yieldsExpectedDepth() {
        // Palm width = 192px (1920/10). With 7.5cm real width and 1000px focal,
        // depth ≈ (0.075 * 1000) / 192 = 0.39m
        let wrist2D = CGPoint(x: 0.5, y: 0.5)
        let palmWidth2D: Float = 192.0 / 1920.0  // normalized

        let pose = estimator.estimate(
            wristNormalized: wrist2D,
            palmWidthNormalized: palmWidth2D,
            forearmDirectionNormalized: CGPoint(x: 0, y: -1),
            palmNormalNormalized: SIMD3<Float>(0, 0, 1),
            confidence: 0.9
        )

        XCTAssertEqual(pose.position.z, -0.39, accuracy: 0.02)
        XCTAssertEqual(pose.position.x, 0, accuracy: 0.001)
        XCTAssertEqual(pose.position.y, 0, accuracy: 0.001)
        XCTAssertEqual(pose.confidence, 0.9)
    }

    func test_handToRight_yieldsPositiveX() {
        let pose = estimator.estimate(
            wristNormalized: CGPoint(x: 0.75, y: 0.5),
            palmWidthNormalized: 0.1,
            forearmDirectionNormalized: CGPoint(x: 0, y: -1),
            palmNormalNormalized: SIMD3<Float>(0, 0, 1),
            confidence: 0.8
        )
        XCTAssertGreaterThan(pose.position.x, 0)
    }

    func test_zeroPalmWidth_returnsLowConfidence() {
        let pose = estimator.estimate(
            wristNormalized: CGPoint(x: 0.5, y: 0.5),
            palmWidthNormalized: 0,
            forearmDirectionNormalized: CGPoint(x: 0, y: -1),
            palmNormalNormalized: SIMD3<Float>(0, 0, 1),
            confidence: 0.5
        )
        XCTAssertEqual(pose.confidence, 0, "Zero palm width = unreliable depth")
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `Cmd+U`, filter `WristPoseEstimatorTests`. Expected: FAIL.

- [ ] **Step 3: Implement WristPoseEstimator**

`BangleMe/Tracking/WristPoseEstimator.swift`:

```swift
import Foundation
import simd
import CoreGraphics

/// 2D bilek keypoint + 2D avuç genişliği → 3D bilek pose (kamera koordinatlarında).
public struct WristPoseEstimator {
    public let focalLengthPx: Float
    public let imageSize: CGSize
    public let avgPalmWidthMeters: Float
    private let minPalmWidthNormalized: Float = 0.01

    public init(focalLengthPx: Float, imageSize: CGSize, avgPalmWidthMeters: Float = 0.075) {
        self.focalLengthPx = focalLengthPx
        self.imageSize = imageSize
        self.avgPalmWidthMeters = avgPalmWidthMeters
    }

    /// - Parameters:
    ///   - wristNormalized: bilek 2D pozisyonu, [0..1] normalize, sol-üst orijin
    ///   - palmWidthNormalized: index taban → küçük parmak taban 2D mesafesi, [0..1]
    ///   - forearmDirectionNormalized: bilek → dirsek yön vektörü (UI koordinatları)
    ///   - palmNormalNormalized: avuç düzlemi normali (kamera koordinatları)
    ///   - confidence: Vision'dan gelen güvenirlik [0..1]
    public func estimate(
        wristNormalized: CGPoint,
        palmWidthNormalized: Float,
        forearmDirectionNormalized: CGPoint,
        palmNormalNormalized: SIMD3<Float>,
        confidence: Float
    ) -> WristPose {

        // Derinlik tahmini güvenilir değilse confidence=0 döndür
        guard palmWidthNormalized >= minPalmWidthNormalized else {
            return WristPose(position: .zero, rotation: simd_quatf(angle: 0, axis: [0,1,0]), confidence: 0)
        }

        // Avuç genişliği px cinsinden
        let palmWidthPx = palmWidthNormalized * Float(imageSize.width)
        // Pinhole formülü: depth = (real_size * focal) / px_size
        let depth = (avgPalmWidthMeters * focalLengthPx) / palmWidthPx

        // 2D normalize → kamera koordinatları (merkez orijin)
        let cx = Float(wristNormalized.x - 0.5) * Float(imageSize.width)
        let cy = Float(0.5 - wristNormalized.y) * Float(imageSize.height)  // Y ters
        let worldX = cx * depth / focalLengthPx
        let worldY = cy * depth / focalLengthPx
        let worldZ = -depth  // kamera -Z'ye bakar

        let position = SIMD3<Float>(worldX, worldY, worldZ)

        // Rotasyon: palm normal + forearm yönünden quaternion
        let forearmDir3D = normalize(SIMD3<Float>(
            Float(forearmDirectionNormalized.x),
            Float(-forearmDirectionNormalized.y),
            0
        ))
        let rotation = orientationFrom(
            forearm: forearmDir3D,
            palmNormal: normalize(palmNormalNormalized)
        )

        return WristPose(position: position, rotation: rotation, confidence: confidence)
    }

    private func orientationFrom(forearm: SIMD3<Float>, palmNormal: SIMD3<Float>) -> simd_quatf {
        // Forearm = local X ekseni, palmNormal = local Y ekseni
        let xAxis = forearm
        let yAxis = palmNormal
        let zAxis = normalize(cross(xAxis, yAxis))
        let yOrtho = normalize(cross(zAxis, xAxis))

        let rotMatrix = simd_float3x3(columns: (xAxis, yOrtho, zAxis))
        return simd_quatf(rotMatrix)
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `Cmd+U`. Expected: 3/3 pass.

- [ ] **Step 5: Commit**

```bash
git add BangleMe/Tracking/WristPoseEstimator.swift BangleMeTests/WristPoseEstimatorTests.swift
git commit -m "feat: add 2D→3D wrist pose estimator with pinhole + palm width depth"
```

---

## Task 6: Implement CameraSession

**Files:**
- Create: `BangleMe/Camera/CameraSession.swift`

**Note:** Bu modül donanım kullanır → birim testi yok. Task 11'de görsel olarak doğrulanır.

- [ ] **Step 1: Write CameraSession**

`BangleMe/Camera/CameraSession.swift`:

```swift
import AVFoundation
import Combine
import CoreVideo

/// AVCaptureSession sarmalayıcısı. Her frame'i delegate'e yollar.
public final class CameraSession: NSObject {
    public typealias FrameHandler = (CVPixelBuffer, CMTime) -> Void

    private let session = AVCaptureSession()
    private let videoOutput = AVCaptureVideoDataOutput()
    private let sessionQueue = DispatchQueue(label: "BangleMe.CameraSession")
    private var frameHandler: FrameHandler?

    public private(set) var focalLengthPx: Float = 1000  // default; calibrated on start
    public private(set) var imageSize: CGSize = CGSize(width: 1920, height: 1080)

    public func start(handler: @escaping FrameHandler) {
        self.frameHandler = handler
        sessionQueue.async { [weak self] in self?.configureAndStart() }
    }

    public func stop() {
        sessionQueue.async { [weak self] in self?.session.stopRunning() }
    }

    private func configureAndStart() {
        session.beginConfiguration()
        session.sessionPreset = .hd1920x1080

        guard let device = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .back),
              let input = try? AVCaptureDeviceInput(device: device),
              session.canAddInput(input) else {
            session.commitConfiguration()
            return
        }
        session.addInput(input)

        // Calibrate focal length from device intrinsics if available
        if let format = device.activeFormat as AVCaptureDevice.Format? {
            let dims = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
            imageSize = CGSize(width: Int(dims.width), height: Int(dims.height))
        }

        videoOutput.videoSettings = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA]
        videoOutput.alwaysDiscardsLateVideoFrames = true
        videoOutput.setSampleBufferDelegate(self, queue: sessionQueue)
        if session.canAddOutput(videoOutput) { session.addOutput(videoOutput) }

        // Enable intrinsic matrix delivery (for accurate focal length)
        if let connection = videoOutput.connection(with: .video) {
            if connection.isCameraIntrinsicMatrixDeliverySupported {
                connection.isCameraIntrinsicMatrixDeliveryEnabled = true
            }
            connection.videoOrientation = .portrait
        }

        session.commitConfiguration()
        session.startRunning()
    }
}

extension CameraSession: AVCaptureVideoDataOutputSampleBufferDelegate {
    public func captureOutput(
        _ output: AVCaptureOutput,
        didOutput sampleBuffer: CMSampleBuffer,
        from connection: AVCaptureConnection
    ) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let timestamp = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)

        // Update focal length from intrinsics if attached
        if let intrinsics = CMGetAttachment(sampleBuffer, key: kCMSampleBufferAttachmentKey_CameraIntrinsicMatrix, attachmentModeOut: nil) as? Data {
            intrinsics.withUnsafeBytes { ptr in
                let matrix = ptr.load(as: matrix_float3x3.self)
                self.focalLengthPx = matrix.columns.0.x  // fx
            }
        }

        frameHandler?(pixelBuffer, timestamp)
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add BangleMe/Camera/CameraSession.swift
git commit -m "feat: add CameraSession with frame delivery and intrinsics"
```

---

## Task 7: Build SwiftUI camera preview view

**Files:**
- Create: `BangleMe/Camera/CameraPreviewView.swift`

- [ ] **Step 1: Write CameraPreviewView**

`BangleMe/Camera/CameraPreviewView.swift`:

```swift
import SwiftUI
import AVFoundation
import UIKit

/// SwiftUI sarmalayıcı: AVCaptureVideoPreviewLayer'ı bir UIView'a koyar.
public struct CameraPreviewView: UIViewRepresentable {
    let session: AVCaptureSession

    public func makeUIView(context: Context) -> PreviewUIView {
        let view = PreviewUIView()
        view.previewLayer.session = session
        view.previewLayer.videoGravity = .resizeAspectFill
        return view
    }

    public func updateUIView(_ uiView: PreviewUIView, context: Context) {}

    public final class PreviewUIView: UIView {
        public override class var layerClass: AnyClass { AVCaptureVideoPreviewLayer.self }
        var previewLayer: AVCaptureVideoPreviewLayer { layer as! AVCaptureVideoPreviewLayer }
    }
}
```

- [ ] **Step 2: Expose session from CameraSession**

Modify `BangleMe/Camera/CameraSession.swift` — change `private let session` to `public let session`:

```swift
public let session = AVCaptureSession()
```

- [ ] **Step 3: Commit**

```bash
git add BangleMe/Camera/CameraPreviewView.swift BangleMe/Camera/CameraSession.swift
git commit -m "feat: add SwiftUI camera preview view"
```

---

## Task 8: Implement HandPoseDetector (Vision wrapper)

**Files:**
- Create: `BangleMe/Tracking/HandPoseDetector.swift`

**Note:** Vision live frame'lere bağlı → birim testi sınırlı. Task 11'de görsel doğrulama.

- [ ] **Step 1: Write HandPoseDetector**

`BangleMe/Tracking/HandPoseDetector.swift`:

```swift
import Vision
import CoreVideo
import CoreGraphics
import simd

/// Bir avuç için algılanan keypoint'ler (Vision çıktısının BangleMe formatı).
public struct HandObservation {
    public let wrist: CGPoint              // [0..1] normalize
    public let indexBase: CGPoint          // avuç tabanı, index
    public let littleBase: CGPoint         // avuç tabanı, küçük parmak
    public let middleTip: CGPoint          // orta parmak ucu
    public let palmWidthNormalized: Float  // indexBase ↔ littleBase mesafesi
    public let forearmDirection: CGPoint   // bilek → dirsek doğrultusu (yaklaşık)
    public let palmNormal: SIMD3<Float>    // avuç düzlemi normali
    public let confidence: Float
}

public final class HandPoseDetector {
    private let request = VNDetectHumanHandPoseRequest()
    private let confidenceThreshold: Float = 0.3

    public init() {
        request.maximumHandCount = 2
    }

    public func detect(pixelBuffer: CVPixelBuffer) -> [HandObservation] {
        let handler = VNImageRequestHandler(cvPixelBuffer: pixelBuffer, orientation: .right, options: [:])
        do {
            try handler.perform([request])
        } catch {
            return []
        }
        guard let results = request.results else { return [] }
        return results.compactMap { observation in
            extractObservation(from: observation)
        }
    }

    private func extractObservation(from obs: VNHumanHandPoseObservation) -> HandObservation? {
        guard let points = try? obs.recognizedPoints(.all) else { return nil }
        guard let wrist = points[.wrist], wrist.confidence > confidenceThreshold,
              let indexBase = points[.indexMCP], indexBase.confidence > confidenceThreshold,
              let littleBase = points[.littleMCP], littleBase.confidence > confidenceThreshold,
              let middleTip = points[.middleTip] else {
            return nil
        }

        let wristPt = CGPoint(x: wrist.location.x, y: 1 - wrist.location.y)
        let indexPt = CGPoint(x: indexBase.location.x, y: 1 - indexBase.location.y)
        let littlePt = CGPoint(x: littleBase.location.x, y: 1 - littleBase.location.y)
        let midPt = CGPoint(x: middleTip.location.x, y: 1 - middleTip.location.y)

        let dx = Float(indexPt.x - littlePt.x)
        let dy = Float(indexPt.y - littlePt.y)
        let palmWidth = sqrt(dx * dx + dy * dy)

        // Forearm yönü = bilek → orta parmak'ın TERSİ (yaklaşık)
        let forearmDir = CGPoint(x: wristPt.x - midPt.x, y: wristPt.y - midPt.y)
        let forearmLen = sqrt(forearmDir.x * forearmDir.x + forearmDir.y * forearmDir.y)
        let forearmNorm = CGPoint(x: forearmDir.x / forearmLen, y: forearmDir.y / forearmLen)

        // Palm normal'ı 3D olarak yaklaşık: avuç düzlemine dik (default kameraya doğru)
        // Tam doğru hesap için 3D üçgen gerekir; MVP için (0, 0, 1) yeterli.
        let palmNormal = SIMD3<Float>(0, 0, 1)

        let avgConfidence = (wrist.confidence + indexBase.confidence + littleBase.confidence) / 3.0

        return HandObservation(
            wrist: wristPt,
            indexBase: indexPt,
            littleBase: littlePt,
            middleTip: midPt,
            palmWidthNormalized: palmWidth,
            forearmDirection: forearmNorm,
            palmNormal: palmNormal,
            confidence: avgConfidence
        )
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add BangleMe/Tracking/HandPoseDetector.swift
git commit -m "feat: add Vision HandPoseDetector wrapper"
```

---

## Task 9: Add TrackingState for fade-out logic with tests

**Files:**
- Create: `BangleMe/Tracking/TrackingState.swift`
- Create: `BangleMeTests/TrackingStateTests.swift`

- [ ] **Step 1: Write failing tests**

`BangleMeTests/TrackingStateTests.swift`:

```swift
import XCTest
@testable import BangleMe

final class TrackingStateTests: XCTestCase {
    func test_initialState_isLost() {
        let state = TrackingState()
        XCTAssertEqual(state.opacity, 0)
        XCTAssertFalse(state.isTracking)
    }

    func test_detectedHand_increasesOpacityOverFadeInDuration() {
        var state = TrackingState()
        state.update(detected: true, timestamp: 0)
        state.update(detected: true, timestamp: 0.1)
        XCTAssertGreaterThan(state.opacity, 0)
        XCTAssertLessThan(state.opacity, 1)

        state.update(detected: true, timestamp: 0.3)
        XCTAssertEqual(state.opacity, 1, accuracy: 0.01)
    }

    func test_lostHand_fadesOutAfterDelay() {
        var state = TrackingState()
        // First reach full opacity
        state.update(detected: true, timestamp: 0)
        state.update(detected: true, timestamp: 0.3)
        XCTAssertEqual(state.opacity, 1, accuracy: 0.01)

        // Hand lost — still visible during fade-out window
        state.update(detected: false, timestamp: 0.4)
        XCTAssertGreaterThan(state.opacity, 0.5)

        // After fade-out duration (300ms)
        state.update(detected: false, timestamp: 0.71)
        XCTAssertEqual(state.opacity, 0, accuracy: 0.05)
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `Cmd+U`, filter `TrackingStateTests`. Expected: FAIL.

- [ ] **Step 3: Implement TrackingState**

`BangleMe/Tracking/TrackingState.swift`:

```swift
import Foundation

/// Bileziğin görünürlüğünü yöneten state machine.
/// Tracking gelir → 200ms fade-in. Tracking kaybı → 300ms fade-out.
public struct TrackingState {
    public private(set) var opacity: Float = 0
    public private(set) var isTracking: Bool = false

    private var lastDetectedTimestamp: Double?
    private var lastUpdateTimestamp: Double?

    private let fadeInDuration: Float = 0.2
    private let fadeOutDuration: Float = 0.3

    public init() {}

    public mutating func update(detected: Bool, timestamp: Double) {
        defer {
            isTracking = detected
            lastUpdateTimestamp = timestamp
            if detected { lastDetectedTimestamp = timestamp }
        }

        guard let lastUpdate = lastUpdateTimestamp else {
            if detected { lastDetectedTimestamp = timestamp }
            return
        }
        let dt = Float(timestamp - lastUpdate)

        if detected {
            opacity = min(1.0, opacity + dt / fadeInDuration)
        } else {
            opacity = max(0.0, opacity - dt / fadeOutDuration)
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `Cmd+U`. Expected: 3/3 pass.

- [ ] **Step 5: Commit**

```bash
git add BangleMe/Tracking/TrackingState.swift BangleMeTests/TrackingStateTests.swift
git commit -m "feat: add TrackingState for fade-in/fade-out opacity"
```

---

## Task 10: Build DebugSphereOverlay (visual marker)

**Files:**
- Create: `BangleMe/Debug/DebugSphereOverlay.swift`

**Why:** Bracelet render Plan 2'de gelecek. Şimdilik bileğin doğru takip edildiğini görmek için SwiftUI çemberi.

- [ ] **Step 1: Write DebugSphereOverlay**

`BangleMe/Debug/DebugSphereOverlay.swift`:

```swift
import SwiftUI

/// Bileğin algılandığı pozisyona çizilen debug çemberi.
/// Plan 2'de RealityKit bilezikle değiştirilecek.
struct DebugSphereOverlay: View {
    let wristNormalized: CGPoint?  // nil ise tracking yok
    let opacity: Float
    let confidence: Float

    var body: some View {
        GeometryReader { geo in
            if let wrist = wristNormalized {
                Circle()
                    .strokeBorder(Color.yellow, lineWidth: 3)
                    .background(Circle().fill(Color.yellow.opacity(0.3)))
                    .frame(width: 60, height: 60)
                    .position(
                        x: wrist.x * geo.size.width,
                        y: wrist.y * geo.size.height
                    )
                    .opacity(Double(opacity))

                Text(String(format: "conf: %.2f", confidence))
                    .font(.caption.monospaced())
                    .foregroundColor(.yellow)
                    .padding(4)
                    .background(Color.black.opacity(0.5))
                    .cornerRadius(4)
                    .position(
                        x: wrist.x * geo.size.width,
                        y: wrist.y * geo.size.height + 50
                    )
                    .opacity(Double(opacity))
            }
        }
        .allowsHitTesting(false)
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add BangleMe/Debug/DebugSphereOverlay.swift
git commit -m "feat: add debug sphere overlay for wrist visualization"
```

---

## Task 11: Wire it all in ContentView

**Files:**
- Modify: `BangleMe/UI/ContentView.swift`

- [ ] **Step 1: Write the wired ContentView**

`BangleMe/UI/ContentView.swift`:

```swift
import SwiftUI
import simd
import CoreGraphics

struct ContentView: View {
    @StateObject private var tracker = WristTrackingPipeline()

    var body: some View {
        ZStack {
            CameraPreviewView(session: tracker.camera.session)
                .ignoresSafeArea()

            DebugSphereOverlay(
                wristNormalized: tracker.lastWristNormalized,
                opacity: tracker.trackingState.opacity,
                confidence: tracker.lastConfidence
            )
            .ignoresSafeArea()

            VStack {
                Spacer()
                Text(tracker.statusText)
                    .font(.caption.monospaced())
                    .foregroundColor(.white)
                    .padding(8)
                    .background(Color.black.opacity(0.6))
                    .cornerRadius(8)
                    .padding(.bottom, 40)
            }
        }
        .preferredColorScheme(.dark)
        .onAppear { tracker.start() }
        .onDisappear { tracker.stop() }
    }
}

#Preview {
    ContentView()
}
```

- [ ] **Step 2: Create WristTrackingPipeline as the coordinator**

Create `BangleMe/Tracking/WristTrackingPipeline.swift`:

```swift
import Foundation
import CoreVideo
import CoreGraphics
import simd
import Combine

/// Kamera → Vision → estimator → filter → state akışını koordine eden ana pipeline.
@MainActor
final class WristTrackingPipeline: ObservableObject {
    let camera = CameraSession()
    private let detector = HandPoseDetector()
    private var estimator: WristPoseEstimator?

    private var posXFilter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
    private var posYFilter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
    private var posZFilter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)

    @Published var lastWristNormalized: CGPoint?
    @Published var lastConfidence: Float = 0
    @Published var lastPose: WristPose = .identity
    @Published var trackingState = TrackingState()
    @Published var statusText: String = "Starting..."

    func start() {
        camera.start { [weak self] pixelBuffer, time in
            Task { @MainActor [weak self] in
                self?.process(pixelBuffer: pixelBuffer, time: time)
            }
        }
        statusText = "Bileğini kameraya göster"
    }

    func stop() {
        camera.stop()
    }

    private func process(pixelBuffer: CVPixelBuffer, time: CMTime) {
        let timestamp = CMTimeGetSeconds(time)
        let observations = detector.detect(pixelBuffer: pixelBuffer)

        // Lazy-init estimator with calibrated focal length on first frame
        if estimator == nil {
            estimator = WristPoseEstimator(
                focalLengthPx: camera.focalLengthPx,
                imageSize: camera.imageSize
            )
        }

        guard let obs = observations.first, let est = estimator else {
            trackingState.update(detected: false, timestamp: timestamp)
            statusText = "Bilek aranıyor..."
            return
        }

        let rawPose = est.estimate(
            wristNormalized: obs.wrist,
            palmWidthNormalized: obs.palmWidthNormalized,
            forearmDirectionNormalized: obs.forearmDirection,
            palmNormalNormalized: obs.palmNormal,
            confidence: obs.confidence
        )

        let smoothedPos = SIMD3<Float>(
            posXFilter.filter(value: rawPose.position.x, timestamp: timestamp),
            posYFilter.filter(value: rawPose.position.y, timestamp: timestamp),
            posZFilter.filter(value: rawPose.position.z, timestamp: timestamp)
        )

        lastWristNormalized = obs.wrist
        lastConfidence = obs.confidence
        lastPose = WristPose(position: smoothedPos, rotation: rawPose.rotation, confidence: rawPose.confidence)
        trackingState.update(detected: rawPose.confidence > 0, timestamp: timestamp)
        statusText = String(format: "✓ tracking · depth: %.2fm · conf: %.2f", -smoothedPos.z, obs.confidence)
    }
}
```

- [ ] **Step 3: Build and run on a real device**

Plug in iPhone. Select device in Xcode. Cmd+R.

Expected:
- Camera permission prompt appears
- After granting, camera preview fills screen
- Show your wrist to the back camera
- A yellow translucent circle tracks your wrist
- Status text at bottom shows depth in meters and confidence

If circle is laggy or wrong position: adjust `WristPoseEstimator.avgPalmWidthMeters` or check `videoOrientation` in CameraSession.

- [ ] **Step 4: Commit**

```bash
git add BangleMe/UI/ContentView.swift BangleMe/Tracking/WristTrackingPipeline.swift
git commit -m "feat: wire camera + Vision + estimator + filter into live wrist tracker"
```

---

## Task 12: Manual verification + tracking quality validation

**Files:** none changed — this is a verification task.

- [ ] **Step 1: Run on device — verify five wrist tracking scenarios**

For each, observe whether the circle stays correctly on the wrist:

| Scenario | Pass criteria |
|---|---|
| Hand still, palm facing camera | Circle stays steady, no jitter |
| Hand moves slowly side-to-side | Circle follows with <100ms lag |
| Hand moves fast | Circle catches up within 300ms, no overshoot |
| Hand rotates (palm → back) | Circle stays on wrist, no jumping |
| Hand exits frame and returns | Circle fades out in ~300ms, fades back in ~200ms |

- [ ] **Step 2: If issues found, tune One Euro filter parameters**

In `WristTrackingPipeline.swift`, adjust:
- More jitter → decrease `minCutoff` (e.g. 0.5)
- More lag during fast motion → increase `beta` (e.g. 0.015)
- Re-run on device after each change

- [ ] **Step 3: If depth is wrong (circle too small/large relative to wrist)**

Adjust `avgPalmWidthMeters` in `WristPoseEstimator` init (currently 0.075). Try 0.08 or 0.07.

- [ ] **Step 4: Final foundation commit**

```bash
git add -u
git commit -m "tune: foundation tracking quality verified on device"
git tag plan-1-foundation-complete
```

---

## Self-Review

Spec → plan coverage check:

| Spec Requirement | Task |
|---|---|
| iOS 16+ native app skeleton | 1, 2 |
| Camera akışı (AVFoundation) | 6, 7 |
| Vision el algılama | 8 |
| 21 keypoint → 3D bilek pose | 5 (with 8 providing keypoints) |
| Pinhole + palm width depth | 5 |
| One Euro Filter smoothing | 4 |
| Tracking loss → fade out (300ms) | 9 |
| Tracking return → fade in (200ms) | 9 |
| İzin metinleri (kamera/foto/mic) | 2 |

**Not covered in Plan 1 (will be in later plans):**
- RealityKit + bilezik render → Plan 2
- PBR materyal, IBL, hiper-gerçekçi altın → Plan 2
- Stack sistemi, material variants → Plan 3
- UI (karusel, sheet, ayarlar) → Plan 4
- Fizik sistemi → Plan 5
- Kayıt/paylaşım/sticker → Plan 6
- MediaPipe yedek tracker → eklenir (final tracker comparison)
- LiDAR-tabanlı derinlik (Pro modeller) → eklenir Plan 2'de optional path

**Type consistency check:** `WristPose.position` SIMD3<Float> heryerde tutarlı. `HandObservation.palmWidthNormalized` Float, `WristPoseEstimator.estimate` Float bekliyor → uyuşuyor. `simd_quatf` her yerde rotation tipi.

**Placeholder scan:** Hiç TBD/TODO/"implement later" yok. Tüm kod adımlarında tam kod var.

---

## What's Next

Bu plan tamamlandığında:
- iPhone'da kamera açılıyor
- El gösterildiğinde bileğin üstünde sarı bir daire görünüyor
- Daire düzgün takip ediyor, titremiyor
- El kaybolunca yumuşak fade-out

**Sonraki plan (Plan 2 — RealityKit + Bilezik Render):**
- ARKit/RealityKit integration
- Sketchfab'dan altın bilezik modelini import et
- Sarı dairenin yerine altın bileziği bind et
- PBR materyal, IBL, hiper-gerçekçi altın görünümü
- Bileğin oryantasyonuna göre bilezik dön
