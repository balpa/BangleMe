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
