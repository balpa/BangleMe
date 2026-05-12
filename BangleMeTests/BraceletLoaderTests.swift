import XCTest
import RealityKit
@testable import BangleMe

final class BraceletLoaderTests: XCTestCase {
    func test_placeholder_returnsModelEntityWithGoldMaterial() {
        let placeholder = BraceletLoader.placeholder()
        XCTAssertNotNil(placeholder.model)
        guard let materials = placeholder.model?.materials,
              let first = materials.first as? PhysicallyBasedMaterial else {
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
