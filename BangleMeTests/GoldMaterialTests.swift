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
