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
            rotation: simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0)),
            confidence: 0
        ))
        XCTAssertFalse(model.isEnabled)
    }

    func test_applyPose_positiveConfidenceShowsEntity() {
        let model = makeEntity()
        let bracelet = BraceletEntity(entity: model)
        bracelet.applyPose(WristPose(
            position: .zero,
            rotation: simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0)),
            confidence: 0.5
        ))
        XCTAssertTrue(model.isEnabled)
    }
}
