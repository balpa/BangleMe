# BangleMe Plan 2 — Bracelet Render Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the yellow debug circle from Plan 1 with a hyper-realistic gold bracelet rendered by RealityKit, anchored to the detected wrist pose. Plan 1 produced wrist tracking; Plan 2 makes it look like an actual bracelet.

**Architecture:** RealityKit `ARView` in `.nonAR` mode is layered on top of the existing `CameraPreviewView` (which still owns the AVCaptureSession). A `PerspectiveCamera` entity has its FOV computed from the real camera's focal length + image size, so 3D positions in our wrist-tracking pinhole space project to the same screen pixels they would in the camera image. Image-Based Lighting (IBL) uses an embedded studio HDRI for realistic gold reflections. The `WristTrackingPipeline` writes each smoothed pose into a `BraceletScene` reference on every frame.

**Tech Stack:** RealityKit, ARView (non-AR mode), `PhysicallyBasedMaterial`, `EnvironmentResource`, USDZ (.usdz) model from Sketchfab, OpenEXR (.exr) HDRI from Poly Haven. iOS 16+ deployment target retained.

**Testing strategy:** Material factories, loader logic, and pose-application math are pure Swift on top of RealityKit value types → XCTest in simulator. Visual quality (gold tone, reflections, bracelet sitting on wrist) is verified on device — those are not unit testable.

---

## File Structure

New files created in this plan:

```
BangleMe/
├── Resources/
│   ├── bracelets/
│   │   ├── classic_bangle.usdz       # Sketchfab model, manual download
│   │   └── Credits.md                # CC Attribution text
│   └── env/
│       └── studio.exr                # Studio HDRI, Poly Haven (CC0)
├── Render/
│   ├── GoldMaterial.swift            # PhysicallyBasedMaterial factory
│   ├── BraceletLoader.swift          # USDZ → ModelEntity, applies material
│   ├── BraceletEntity.swift          # Wrapper: ModelEntity + applyPose
│   ├── BraceletScene.swift           # ARView + virtual camera + IBL
│   └── BraceletSceneView.swift       # UIViewRepresentable wrapper
└── (modified)
    ├── BangleMe/Tracking/WristTrackingPipeline.swift   # writes into BraceletScene
    ├── BangleMe/UI/ContentView.swift                   # adds BraceletSceneView layer
    └── project.yml                                     # Resources/, RealityKit linked
```

`BangleMeTests/` gains: `GoldMaterialTests.swift`, `BraceletLoaderTests.swift`, `BraceletEntityTests.swift`.

---

## Task 1: Add the starter bracelet USDZ to the bundle

**Files:**
- Create: `BangleMe/Resources/bracelets/classic_bangle.usdz` (manual download)
- Create: `BangleMe/Resources/bracelets/Credits.md`

This task is manual asset acquisition — no code. Sketchfab auto-converts the model to USDZ on download. For Plan 2 we use the 18.1k-tri version as-is; decimation to 6–8k tris is deferred to Plan 3 where stack performance starts to matter.

- [ ] **Step 1: Download the USDZ from Sketchfab**

Open in a browser: <https://sketchfab.com/3d-models/gold-bracelet-dd2c51b6a90345a9b062e7a9961c1db7>

Click **Download 3D Model** → choose **USDZ**. Save the file.

If you don't have a Sketchfab account, sign up for free first. Sketchfab is the only place that ships this model as USDZ directly; if the option is missing for any reason, choose **glTF (.glb)** and convert with Reality Converter (Xcode → Open Developer Tool → Reality Converter → drag glb → File → Export → USDZ).

- [ ] **Step 2: Move the file into the bundle path**

```bash
cd /Users/berke.altiparmak/Documents/BangleMe
mkdir -p BangleMe/Resources/bracelets
mv ~/Downloads/gold-bracelet*.usdz BangleMe/Resources/bracelets/classic_bangle.usdz
ls -la BangleMe/Resources/bracelets/
```

Expected: `classic_bangle.usdz` present, file size ~2–6 MB.

- [ ] **Step 3: Write Credits.md (CC Attribution requirement)**

`BangleMe/Resources/bracelets/Credits.md`:

```markdown
# 3D Asset Credits

## classic_bangle.usdz

- **Author:** Tahir.Muhamad.Ajmal
- **Source:** https://sketchfab.com/3d-models/gold-bracelet-dd2c51b6a90345a9b062e7a9961c1db7
- **License:** CC Attribution 4.0 (https://creativecommons.org/licenses/by/4.0/)
- **Used in:** BangleMe (starter bracelet model)
- **Modifications:** none in Plan 2; intended for material replacement in Plan 2 and mesh decimation in Plan 3.

Attribution must remain on the in-app Credits screen for as long as this
model is shipped with BangleMe.
```

- [ ] **Step 4: Commit**

```bash
git add BangleMe/Resources/bracelets/
git commit -m "feat: add starter gold bracelet USDZ and CC attribution"
```

---

## Task 2: Add a studio HDRI for image-based lighting

**Files:**
- Create: `BangleMe/Resources/env/studio.exr` (manual download)

A realistic gold bracelet needs a real environment to reflect. A 1K studio HDRI gives us photographable highlights without flooding the cinematic look.

- [ ] **Step 1: Download a free studio HDRI from Poly Haven**

Open <https://polyhaven.com/hdris/studio> in a browser. Pick any clean studio environment (e.g. *studio_small_09*, *photo_studio_01*). Click the chosen HDRI → set **Resolution: 1K** → **Format: EXR** → Download.

Why 1K and not 2K/4K: file size scales quadratically and we don't need the extra detail for a bracelet-sized reflector. 1K EXR is typically 1–3 MB.

- [ ] **Step 2: Move the file into the bundle path**

```bash
cd /Users/berke.altiparmak/Documents/BangleMe
mkdir -p BangleMe/Resources/env
mv ~/Downloads/studio_*.exr BangleMe/Resources/env/studio.exr
ls -la BangleMe/Resources/env/
```

Expected: `studio.exr` present, ~1–3 MB.

- [ ] **Step 3: Commit**

```bash
git add BangleMe/Resources/env/
git commit -m "feat: add studio HDRI for image-based lighting"
```

---

## Task 3: Regenerate Xcode project so Resources/ is picked up

**Files:**
- Modify: `project.yml` (only if needed)
- Regenerate: `BangleMe.xcodeproj`

The existing `sources: - path: BangleMe` entry already recurses into `BangleMe/Resources/`. xcodegen's default behavior places each file in its inferred build phase based on extension — `.usdz` and `.exr` go to **Copy Bundle Resources**, ending up flat at bundle root. That's exactly what `Bundle.main.url(forResource: "classic_bangle", withExtension: "usdz")` and `EnvironmentResource.load(named: "studio")` need. So Task 3 is mostly a regenerate-and-verify step.

- [ ] **Step 1: Regenerate the Xcode project**

```bash
cd /Users/berke.altiparmak/Documents/BangleMe
xcodegen generate
```

Expected output: `Created project at /Users/berke.altiparmak/Documents/BangleMe/BangleMe.xcodeproj`

- [ ] **Step 2: Confirm the assets appear in the project**

```bash
grep -E "classic_bangle\.usdz|studio\.exr" BangleMe.xcodeproj/project.pbxproj | head -4
```

Expected: at least one match per file (a `PBXFileReference` and a `PBXBuildFile` entry per asset).

If grep returns nothing, the regenerate didn't pick the files up — re-run `ls BangleMe/Resources/` to confirm the files are on disk first, then `xcodegen generate` again.

- [ ] **Step 3: Commit**

```bash
git add BangleMe.xcodeproj
git commit -m "build: regenerate Xcode project to include bracelet asset and HDRI"
```

---

## Task 4: GoldMaterial factory with tests

**Files:**
- Create: `BangleMe/Render/GoldMaterial.swift`
- Create: `BangleMeTests/GoldMaterialTests.swift`

Encapsulates the spec §4.2 PBR values for hyper-realistic warm gold. A factory (vs. a stored property) so each model entity gets its own material instance — RealityKit materials are value types but textures inside aren't always safe to share.

- [ ] **Step 1: Write the failing test**

`BangleMeTests/GoldMaterialTests.swift`:

```swift
import XCTest
import RealityKit
import UIKit
@testable import BangleMe

final class GoldMaterialTests: XCTestCase {
    func test_warmYellow_isMetallic() {
        let material = GoldMaterial.warmYellow()
        XCTAssertEqual(material.metallic.scale, 1.0, accuracy: 0.001)
    }

    func test_warmYellow_hasLowRoughness() {
        let material = GoldMaterial.warmYellow()
        XCTAssertEqual(material.roughness.scale, 0.18, accuracy: 0.001)
    }

    func test_warmYellow_baseColorIsWarmGold() {
        let material = GoldMaterial.warmYellow()
        let tint = material.baseColor.tint
        var r: CGFloat = 0, g: CGFloat = 0, b: CGFloat = 0, a: CGFloat = 0
        tint.getRed(&r, green: &g, blue: &b, alpha: &a)
        XCTAssertEqual(Float(r), 0.83, accuracy: 0.01)
        XCTAssertEqual(Float(g), 0.69, accuracy: 0.01)
        XCTAssertEqual(Float(b), 0.22, accuracy: 0.01)
    }

    func test_warmYellow_hasClearcoat() {
        let material = GoldMaterial.warmYellow()
        XCTAssertEqual(material.clearcoat.scale, 0.3, accuracy: 0.001)
        XCTAssertEqual(material.clearcoatRoughness.scale, 0.05, accuracy: 0.001)
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

In Xcode: ⌘U with the `GoldMaterialTests` filter. Expected: FAIL — "Cannot find 'GoldMaterial' in scope".

- [ ] **Step 3: Implement GoldMaterial**

`BangleMe/Render/GoldMaterial.swift`:

```swift
import RealityKit
import UIKit

enum GoldMaterial {
    static func warmYellow() -> PhysicallyBasedMaterial {
        var material = PhysicallyBasedMaterial()
        material.baseColor = .init(tint: UIColor(red: 0.83, green: 0.69, blue: 0.22, alpha: 1.0))
        material.metallic = .init(floatLiteral: 1.0)
        material.roughness = .init(floatLiteral: 0.18)
        material.clearcoat = .init(floatLiteral: 0.3)
        material.clearcoatRoughness = .init(floatLiteral: 0.05)
        return material
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

In Xcode: ⌘U. Expected: 4/4 pass.

- [ ] **Step 5: Commit**

```bash
git add BangleMe/Render/GoldMaterial.swift BangleMeTests/GoldMaterialTests.swift
git commit -m "feat: add hyper-realistic warm gold PBR material factory"
```

---

## Task 5: BraceletLoader for USDZ with placeholder fallback and tests

**Files:**
- Create: `BangleMe/Render/BraceletLoader.swift`
- Create: `BangleMeTests/BraceletLoaderTests.swift`

Loads `classic_bangle.usdz` from the bundle and rewrites every material on every `ModelEntity` in the hierarchy with our PBR gold (the Sketchfab model ships with a baked material that doesn't read correctly off our IBL). Falls back to a procedural torus-ish placeholder if the USDZ isn't there yet — this keeps the simulator path runnable even when only Sketchfab download is missing.

- [ ] **Step 1: Write the failing test**

`BangleMeTests/BraceletLoaderTests.swift`:

```swift
import XCTest
import RealityKit
@testable import BangleMe

final class BraceletLoaderTests: XCTestCase {
    func test_placeholder_returnsModelEntityWithGoldMaterial() {
        let placeholder = BraceletLoader.placeholder()
        XCTAssertNotNil(placeholder.model)
        guard let materials = placeholder.model?.materials, let first = materials.first as? PhysicallyBasedMaterial else {
            return XCTFail("Expected at least one PhysicallyBasedMaterial")
        }
        XCTAssertEqual(first.metallic.scale, 1.0, accuracy: 0.001)
    }

    func test_load_unknownName_throws() {
        XCTAssertThrowsError(try BraceletLoader().load(name: "this_does_not_exist_xyz")) { error in
            guard case BraceletLoaderError.modelNotFound = error else {
                return XCTFail("Expected modelNotFound, got \(error)")
            }
        }
    }

    func test_applyGoldMaterial_replacesAllMaterials() {
        let mesh = MeshResource.generateBox(size: 0.05)
        let dummyMat = SimpleMaterial(color: .green, isMetallic: false)
        let entity = ModelEntity(mesh: mesh, materials: [dummyMat, dummyMat])

        BraceletLoader.applyGoldMaterial(to: entity)

        let materials = entity.model?.materials ?? []
        XCTAssertEqual(materials.count, 2)
        XCTAssertTrue(materials.allSatisfy { $0 is PhysicallyBasedMaterial })
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

⌘U → BraceletLoaderTests. Expected: FAIL — "Cannot find 'BraceletLoader' in scope".

- [ ] **Step 3: Implement BraceletLoader**

`BangleMe/Render/BraceletLoader.swift`:

```swift
import RealityKit
import Foundation

enum BraceletLoaderError: Error, Equatable {
    case modelNotFound
    case loadFailed(String)
}

struct BraceletLoader {
    func load(name: String) throws -> ModelEntity {
        guard let url = Bundle.main.url(forResource: name, withExtension: "usdz") else {
            throw BraceletLoaderError.modelNotFound
        }
        let entity: ModelEntity
        do {
            entity = try ModelEntity.loadModel(contentsOf: url)
        } catch {
            throw BraceletLoaderError.loadFailed(String(describing: error))
        }
        Self.applyGoldMaterial(to: entity)
        return entity
    }

    static func placeholder() -> ModelEntity {
        // 6 cm box, height-flat, as a stand-in for a bracelet ring.
        // Gives a recognizable surface to verify IBL + material before the
        // real USDZ is in the bundle.
        let mesh = MeshResource.generateBox(size: SIMD3<Float>(0.06, 0.02, 0.06), cornerRadius: 0.01)
        let entity = ModelEntity(mesh: mesh, materials: [GoldMaterial.warmYellow()])
        return entity
    }

    static func applyGoldMaterial(to entity: Entity) {
        if let modelEntity = entity as? ModelEntity, var model = modelEntity.model {
            model.materials = model.materials.map { _ in GoldMaterial.warmYellow() }
            modelEntity.model = model
        }
        for child in entity.children {
            applyGoldMaterial(to: child)
        }
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

⌘U. Expected: 3/3 pass.

- [ ] **Step 5: Commit**

```bash
git add BangleMe/Render/BraceletLoader.swift BangleMeTests/BraceletLoaderTests.swift
git commit -m "feat: add USDZ bracelet loader with procedural placeholder fallback"
```

---

## Task 6: BraceletEntity pose-application wrapper with tests

**Files:**
- Create: `BangleMe/Render/BraceletEntity.swift`
- Create: `BangleMeTests/BraceletEntityTests.swift`

The wrapper holds a `ModelEntity` plus a static "model-local rotation fixup" (so Sketchfab/Blender axis quirks don't leak into pipeline code) and a uniform scale. Each frame, `applyPose(_:)` writes the smoothed wrist pose into the entity transform and toggles visibility on confidence.

- [ ] **Step 1: Write the failing test**

`BangleMeTests/BraceletEntityTests.swift`:

```swift
import XCTest
import RealityKit
import simd
@testable import BangleMe

final class BraceletEntityTests: XCTestCase {
    private func makeEntity() -> ModelEntity {
        let mesh = MeshResource.generateBox(size: 0.05)
        return ModelEntity(mesh: mesh, materials: [GoldMaterial.warmYellow()])
    }

    func test_applyPose_writesPosition() {
        let model = makeEntity()
        let bracelet = BraceletEntity(entity: model)
        let pose = WristPose(
            position: SIMD3<Float>(0.1, -0.05, -0.4),
            rotation: simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0)),
            confidence: 0.9
        )

        bracelet.applyPose(pose)

        XCTAssertEqual(model.position.x, 0.1, accuracy: 0.0001)
        XCTAssertEqual(model.position.y, -0.05, accuracy: 0.0001)
        XCTAssertEqual(model.position.z, -0.4, accuracy: 0.0001)
    }

    func test_applyPose_appliesScale() {
        let model = makeEntity()
        let bracelet = BraceletEntity(entity: model)
        bracelet.scale = 1.2
        bracelet.applyPose(WristPose.identity)
        XCTAssertEqual(model.scale.x, 1.2, accuracy: 0.0001)
        XCTAssertEqual(model.scale.y, 1.2, accuracy: 0.0001)
        XCTAssertEqual(model.scale.z, 1.2, accuracy: 0.0001)
    }

    func test_applyPose_zeroConfidenceHidesEntity() {
        let model = makeEntity()
        let bracelet = BraceletEntity(entity: model)
        bracelet.applyPose(WristPose(
            position: .zero,
            rotation: simd_quatf(angle: 0, axis: SIMD3<Float>(0,1,0)),
            confidence: 0
        ))
        XCTAssertFalse(model.isEnabled)
    }

    func test_applyPose_positiveConfidenceShowsEntity() {
        let model = makeEntity()
        let bracelet = BraceletEntity(entity: model)
        bracelet.applyPose(WristPose(
            position: .zero,
            rotation: simd_quatf(angle: 0, axis: SIMD3<Float>(0,1,0)),
            confidence: 0.5
        ))
        XCTAssertTrue(model.isEnabled)
    }
}
```

- [ ] **Step 2: Run tests, verify they fail**

⌘U → BraceletEntityTests. Expected: FAIL — "Cannot find 'BraceletEntity' in scope".

- [ ] **Step 3: Implement BraceletEntity**

`BangleMe/Render/BraceletEntity.swift`:

```swift
import RealityKit
import simd

final class BraceletEntity {
    let entity: ModelEntity
    var modelLocalRotationFixup: simd_quatf
    var scale: Float = 1.0

    init(entity: ModelEntity, modelLocalRotationFixup: simd_quatf = simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0))) {
        self.entity = entity
        self.modelLocalRotationFixup = modelLocalRotationFixup
    }

    func applyPose(_ pose: WristPose) {
        entity.position = pose.position
        entity.orientation = pose.rotation * modelLocalRotationFixup
        entity.scale = SIMD3<Float>(repeating: scale)
        entity.isEnabled = pose.confidence > 0
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

⌘U. Expected: 4/4 pass.

- [ ] **Step 5: Commit**

```bash
git add BangleMe/Render/BraceletEntity.swift BangleMeTests/BraceletEntityTests.swift
git commit -m "feat: add BraceletEntity wrapper applying smoothed pose to ModelEntity"
```

---

## Task 7: BraceletScene — ARView + virtual camera + IBL

**Files:**
- Create: `BangleMe/Render/BraceletScene.swift`

Hosts the RealityKit scene. Sits in `.nonAR` mode so we own the camera entirely — wrist 3D positions are world coords already (camera at origin, looking down `-Z`), so a virtual `PerspectiveCamera` at world origin with the matching FOV reproduces the real camera's projection. IBL is loaded from `studio.exr`; if absent (Task 2 not yet done), we keep the scene running without environment lighting.

This file has hardware/graphics-context dependencies, so no XCTest. Device verification happens in Task 11.

- [ ] **Step 1: Write BraceletScene**

`BangleMe/Render/BraceletScene.swift`:

```swift
import RealityKit
import CoreGraphics
import simd
import UIKit

final class BraceletScene {
    let arView: ARView

    private let cameraAnchor = AnchorEntity(world: .zero)
    private let perspectiveCamera = PerspectiveCamera()

    private let braceletAnchor = AnchorEntity(world: .zero)
    private var bracelet: BraceletEntity?

    init() {
        let view = ARView(
            frame: .zero,
            cameraMode: .nonAR,
            automaticallyConfigureSession: false
        )
        view.environment.background = .color(.clear)
        view.renderOptions.insert(.disableMotionBlur)
        view.renderOptions.insert(.disableDepthOfField)
        view.renderOptions.insert(.disableGroundingShadows)
        self.arView = view

        perspectiveCamera.camera.near = 0.01
        perspectiveCamera.camera.far = 5.0
        perspectiveCamera.camera.fieldOfViewInDegrees = 60
        cameraAnchor.addChild(perspectiveCamera)
        view.scene.addAnchor(cameraAnchor)

        view.scene.addAnchor(braceletAnchor)

        loadStudioIBL()
    }

    func configureCamera(focalLengthPx: Float, imageSize: CGSize) {
        guard focalLengthPx > 0, imageSize.height > 0 else { return }
        let vFovRadians = 2 * atan(Float(imageSize.height) / (2 * focalLengthPx))
        perspectiveCamera.camera.fieldOfViewInDegrees = vFovRadians * 180 / .pi
    }

    func setBracelet(_ model: ModelEntity) {
        braceletAnchor.children.removeAll()
        braceletAnchor.addChild(model)
        bracelet = BraceletEntity(entity: model)
        bracelet?.applyPose(.identity)
    }

    func updatePose(_ pose: WristPose) {
        bracelet?.applyPose(pose)
    }

    private func loadStudioIBL() {
        do {
            let resource = try EnvironmentResource.load(named: "studio")
            arView.environment.lighting.resource = resource
            arView.environment.lighting.intensityExponent = 1.0
        } catch {
            // HDRI is optional: scene still renders with default lighting.
            // Surface to console so it's diagnosable without crashing.
            print("[BraceletScene] HDRI load failed (\(error)). Using default lighting.")
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add BangleMe/Render/BraceletScene.swift
git commit -m "feat: add RealityKit BraceletScene with virtual camera and studio IBL"
```

---

## Task 8: BraceletSceneView SwiftUI bridge

**Files:**
- Create: `BangleMe/Render/BraceletSceneView.swift`

Thin `UIViewRepresentable` so SwiftUI can layer the ARView in a `ZStack`.

- [ ] **Step 1: Write BraceletSceneView**

`BangleMe/Render/BraceletSceneView.swift`:

```swift
import SwiftUI
import RealityKit

struct BraceletSceneView: UIViewRepresentable {
    let scene: BraceletScene

    func makeUIView(context: Context) -> ARView {
        return scene.arView
    }

    func updateUIView(_ uiView: ARView, context: Context) {}
}
```

- [ ] **Step 2: Commit**

```bash
git add BangleMe/Render/BraceletSceneView.swift
git commit -m "feat: add SwiftUI representable for the RealityKit bracelet scene"
```

---

## Task 9: Wire pipeline → BraceletScene (intrinsics + per-frame pose)

**Files:**
- Modify: `BangleMe/Tracking/WristTrackingPipeline.swift`

The pipeline already produces a smoothed `WristPose` every frame. We add two things: a `braceletScene` reference it can push updates into, and a one-shot call to `configureCamera` on the first frame where we have valid focal-length + image-size intrinsics from `CameraSession`. Keeping the coupling at the pipeline (not in `ContentView`) avoids per-frame SwiftUI invalidation.

- [ ] **Step 1: Modify WristTrackingPipeline**

Replace the entire contents of `BangleMe/Tracking/WristTrackingPipeline.swift` with:

```swift
import Foundation
import CoreVideo
import CoreMedia
import CoreGraphics
import simd
import Combine

@MainActor
final class WristTrackingPipeline: ObservableObject {
    let camera = CameraSession()
    private let detector = HandPoseDetector()
    private var estimator: WristPoseEstimator?

    private var posXFilter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
    private var posYFilter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
    private var posZFilter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)

    weak var braceletScene: BraceletScene?
    private var didConfigureSceneCamera = false

    @Published var lastWristNormalized: CGPoint?
    @Published var lastConfidence: Float = 0
    @Published var lastPose: WristPose = .identity
    @Published var trackingState = TrackingState()
    @Published var statusText: String = "Starting..."
    @Published var showDebugSphere: Bool = false

    func start() {
        camera.start { [weak self] pixelBuffer, time in
            let timestamp = CMTimeGetSeconds(time)
            Task { @MainActor [weak self] in
                self?.process(pixelBuffer: pixelBuffer, timestamp: timestamp)
            }
        }
        statusText = "Bileğini kameraya göster"
    }

    func stop() {
        camera.stop()
    }

    private func process(pixelBuffer: CVPixelBuffer, timestamp: Double) {
        let observations = detector.detect(pixelBuffer: pixelBuffer)

        if estimator == nil {
            estimator = WristPoseEstimator(
                focalLengthPx: camera.focalLengthPx,
                imageSize: camera.imageSize
            )
        }

        if !didConfigureSceneCamera, let scene = braceletScene,
           camera.focalLengthPx > 0, camera.imageSize.height > 0 {
            scene.configureCamera(focalLengthPx: camera.focalLengthPx, imageSize: camera.imageSize)
            didConfigureSceneCamera = true
        }

        guard let obs = observations.first, let est = estimator else {
            trackingState.update(detected: false, timestamp: timestamp)
            if lastWristNormalized != nil && trackingState.opacity <= 0.01 {
                lastWristNormalized = nil
            }
            statusText = "Bilek aranıyor..."
            let fadedPose = WristPose(
                position: lastPose.position,
                rotation: lastPose.rotation,
                confidence: trackingState.opacity > 0.01 ? trackingState.opacity : 0
            )
            braceletScene?.updatePose(fadedPose)
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
        lastPose = WristPose(
            position: smoothedPos,
            rotation: rawPose.rotation,
            confidence: rawPose.confidence
        )
        trackingState.update(detected: rawPose.confidence > 0, timestamp: timestamp)
        statusText = String(format: "tracking · depth: %.2fm · conf: %.2f", -smoothedPos.z, obs.confidence)
        braceletScene?.updatePose(lastPose)
    }
}
```

Changes vs. Plan 1:
1. Added `weak var braceletScene: BraceletScene?` so the pipeline can drive the render directly.
2. Added `didConfigureSceneCamera` one-shot to push intrinsics to the scene's virtual camera.
3. On tracking loss, push a pose with the last position but a confidence equal to the fade-out opacity, so the bracelet smoothly disappears in sync with the existing fade timeline.
4. Added `showDebugSphere` toggle so we can keep the debug circle visible while tuning the model — defaulted off.

- [ ] **Step 2: Compile-check**

```bash
SDK=$(xcrun --sdk iphoneos --show-sdk-path)
xcrun --sdk iphoneos swiftc -typecheck -target arm64-apple-ios16.0 -sdk "$SDK" \
  BangleMe/Tracking/*.swift \
  BangleMe/Camera/*.swift \
  BangleMe/Render/*.swift \
  BangleMe/Debug/*.swift \
  BangleMe/UI/*.swift \
  BangleMe/App/*.swift
echo "EXIT: $?"
```

Expected: `EXIT: 0` (no diagnostics).

- [ ] **Step 3: Commit**

```bash
git add BangleMe/Tracking/WristTrackingPipeline.swift
git commit -m "feat: push wrist pose into BraceletScene and configure virtual camera FOV"
```

---

## Task 10: Replace the debug overlay with BraceletSceneView in ContentView

**Files:**
- Modify: `BangleMe/UI/ContentView.swift`

The bracelet scene becomes the visible AR layer. The yellow debug circle stays behind a toggle so we can re-enable it for tracking diagnostics without recompiling.

- [ ] **Step 1: Replace ContentView with the wired version**

`BangleMe/UI/ContentView.swift`:

```swift
import SwiftUI
import RealityKit
import simd
import CoreGraphics

@MainActor
final class BraceletSceneHolder: ObservableObject {
    let scene = BraceletScene()
    private(set) var didLoadBracelet = false

    func loadBracelet() {
        guard !didLoadBracelet else { return }
        defer { didLoadBracelet = true }

        let loader = BraceletLoader()
        do {
            let model = try loader.load(name: "classic_bangle")
            scene.setBracelet(model)
        } catch {
            print("[BraceletSceneHolder] USDZ load failed (\(error)). Using placeholder.")
            scene.setBracelet(BraceletLoader.placeholder())
        }
    }
}

struct ContentView: View {
    @StateObject private var tracker = WristTrackingPipeline()
    @StateObject private var sceneHolder = BraceletSceneHolder()

    var body: some View {
        ZStack {
            CameraPreviewView(session: tracker.camera.session)
                .ignoresSafeArea()

            BraceletSceneView(scene: sceneHolder.scene)
                .ignoresSafeArea()
                .allowsHitTesting(false)

            if tracker.showDebugSphere {
                DebugSphereOverlay(
                    wristNormalized: tracker.lastWristNormalized,
                    opacity: tracker.trackingState.opacity,
                    confidence: tracker.lastConfidence
                )
                .ignoresSafeArea()
            }

            VStack {
                HStack {
                    Spacer()
                    Toggle("Debug", isOn: $tracker.showDebugSphere)
                        .labelsHidden()
                        .tint(.yellow)
                        .padding(.top, 12)
                        .padding(.trailing, 16)
                }
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
        .onAppear {
            sceneHolder.loadBracelet()
            tracker.braceletScene = sceneHolder.scene
            tracker.start()
        }
        .onDisappear { tracker.stop() }
    }
}

#Preview {
    ContentView()
}
```

- [ ] **Step 2: Compile-check**

```bash
SDK=$(xcrun --sdk iphoneos --show-sdk-path)
xcrun --sdk iphoneos swiftc -typecheck -target arm64-apple-ios16.0 -sdk "$SDK" \
  BangleMe/Tracking/*.swift \
  BangleMe/Camera/*.swift \
  BangleMe/Render/*.swift \
  BangleMe/Debug/*.swift \
  BangleMe/UI/*.swift \
  BangleMe/App/*.swift
echo "EXIT: $?"
```

Expected: `EXIT: 0`.

- [ ] **Step 3: Commit**

```bash
git add BangleMe/UI/ContentView.swift
git commit -m "feat: render gold bracelet over camera, debug sphere behind a toggle"
```

---

## Task 11: On-device verification and PBR tuning

**Files:** none changed unless tuning is needed.

This is the milestone validation task. Without a real phone we cannot prove gold-on-wrist looks right.

- [ ] **Step 1: Ensure iOS platform component is installed**

If `xcodebuild -showsdks` lists "iOS SDKs" but build fails with "iOS 26.4 is not installed", run:

```bash
xcodebuild -downloadPlatform iOS
```

This is slow (8–12 GB). Alternative: Xcode → Settings → Platforms → iOS → Get.

- [ ] **Step 2: Build and run on a real iPhone (iOS 16+)**

In Xcode: select the connected iPhone → ⌘R.

- [ ] **Step 3: Verify all visual scenarios**

| Scenario | Pass criteria |
|---|---|
| Bracelet appears on wrist when hand is in frame | Gold shape replaces the (no-longer-shown) yellow circle, sitting on the wrist |
| Bracelet follows wrist as the arm moves | Less than ~100 ms positional lag, no obvious offset |
| Bracelet rotates as the wrist rotates | Stays oriented along the forearm; not "stuck flat to screen" |
| Hand exits frame | Bracelet fades out in ~300 ms |
| Hand returns | Bracelet fades back in ~200 ms |
| Material reads as gold | Warm yellow-orange tone, visible specular highlight from environment, not a flat plastic look |
| Frame rate stays at 60 fps on iPhone 13+ | Use the Xcode FPS HUD (Debug → View Debugging → Rendering → Show FPS Counter) |

- [ ] **Step 4: If the bracelet sits the wrong way around the wrist**

The Sketchfab model's local axes may not align with our pipeline's convention (X = forearm direction, Y = palm normal, Z = inward). Adjust `BraceletEntity.modelLocalRotationFixup` in `BangleMe/Render/BraceletScene.swift` at the construction site:

```swift
bracelet = BraceletEntity(
    entity: model,
    modelLocalRotationFixup: simd_quatf(angle: .pi / 2, axis: SIMD3<Float>(0, 1, 0))
)
```

Try 90°, 180°, 270° rotations around X / Y / Z until the bracelet's ring opening faces the forearm.

- [ ] **Step 5: If the bracelet is way too big or too small**

The model is in its authored scale (often "1 unit = 1 meter" but Sketchfab models vary). In `BraceletSceneHolder.loadBracelet()`, after `scene.setBracelet(model)`, set:

```swift
// e.g. model came out 5x too large
model.scale = SIMD3<Float>(repeating: 0.2)
```

Adult wrist diameter ≈ 6.5 cm — the bracelet's outer diameter should look ~7–8 cm on the wrist.

- [ ] **Step 6: If the gold looks dull or plasticky**

Open `GoldMaterial.swift` and try:

- Lower `roughness` to 0.12 for more mirror-like polish.
- Increase `clearcoat` to 0.5 for stronger lacquer highlight.
- If reflections look flat (no environment showing on the gold), HDRI didn't load — check console for `[BraceletScene] HDRI load failed`. Verify `studio.exr` is in the built `.app` bundle.

- [ ] **Step 7: Commit any tuning changes**

```bash
git add -u
git commit -m "tune: bracelet scale, axis fixup, gold material verified on device"
git tag plan-2-render-complete
```

---

## Self-Review

Spec → plan coverage check (spec §4 — Asset Pipeline and Hyper-Realistic Gold Render):

| Spec Requirement | Task |
|---|---|
| §4.1 Asset pipeline (Sketchfab → bundle) | 1 |
| §4.2 PBR gold material (metallic 1.0, roughness 0.18, clearcoat 0.3) | 4 |
| §4.4 IBL with studio HDRI | 2, 7 |
| §4.7 Performance budget (1 bracelet ≤ 8k tris, 60 fps) | 11 |
| CC Attribution preserved | 1 (Credits.md) |
| Bracelet replaces debug overlay | 10 |
| Tracking loss / return fade | 9 (existing 200/300 ms behavior reused via TrackingState.opacity → confidence) |

**Not covered (later plans, as designed):**
- §4.3 Material variants (Sarı/Beyaz/Rose/Mat) — Plan 3
- §4.5 Stack system (multi-bracelet, offsets) — Plan 3
- §4.6 Size slider (0.85x–1.15x) — Plan 4 (UI) plus `BraceletEntity.scale` plumbing
- §4.4 Live-camera HDRI environment map — Plan 3 polish
- §5 Physics — Plan 5

**Placeholder scan:** Each task either contains full code or an explicit manual operation (download a file, run a command). No "TBD" or "implement later" steps.

**Type consistency check:**
- `BraceletLoader.load(name:)` → `ModelEntity`; `BraceletScene.setBracelet(_ model: ModelEntity)` consumes it — matches.
- `BraceletEntity.applyPose(_ pose: WristPose)`; `WristTrackingPipeline` calls `braceletScene?.updatePose(lastPose)` which forwards into `BraceletEntity.applyPose` via the wrapper — matches.
- `GoldMaterial.warmYellow()` → `PhysicallyBasedMaterial`; `BraceletLoader.applyGoldMaterial(to:)` and `BraceletLoader.placeholder()` both use the same factory — matches.
- `WristPose.confidence` is a `Float` everywhere, including the fade-out path in Task 9 where we pass `trackingState.opacity` directly into it.

---

## What's Next

After this plan:
- iPhone shows the live camera feed with a real gold bracelet sitting on the wrist.
- Gold tone, specular highlight, and roughness read as real metal under the studio HDRI.
- Bracelet fades out / in cleanly on tracking loss / return.

**Plan 3 (Stack + Material Variants):**
- Promote the single bracelet to a `BraceletStack` (1–5 bracelets along the forearm axis)
- Wire the size slider (0.85x–1.15x) through `BraceletEntity.scale`
- Add the three remaining material variants (white/silver, rose gold, matte)
- Per-bracelet material assignment
- Live camera HDRI environment update (polish)
