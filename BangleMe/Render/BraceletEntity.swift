import RealityKit
import simd

final class BraceletEntity {
    let entity: ModelEntity
    var modelLocalRotationFixup: simd_quatf
    var scale: Float = 1.0

    init(
        entity: ModelEntity,
        modelLocalRotationFixup: simd_quatf = simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0))
    ) {
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
