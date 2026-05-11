import simd

public struct WristPose: Equatable {
    public let position: SIMD3<Float>
    public let rotation: simd_quatf
    public let confidence: Float

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

    public static func == (lhs: WristPose, rhs: WristPose) -> Bool {
        return lhs.position == rhs.position
            && lhs.rotation.vector == rhs.rotation.vector
            && lhs.confidence == rhs.confidence
    }
}
