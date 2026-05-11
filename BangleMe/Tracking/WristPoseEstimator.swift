import Foundation
import simd
import CoreGraphics

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

    public func estimate(
        wristNormalized: CGPoint,
        palmWidthNormalized: Float,
        forearmDirectionNormalized: CGPoint,
        palmNormalNormalized: SIMD3<Float>,
        confidence: Float
    ) -> WristPose {

        guard palmWidthNormalized >= minPalmWidthNormalized else {
            return WristPose(
                position: .zero,
                rotation: simd_quatf(angle: 0, axis: SIMD3<Float>(0, 1, 0)),
                confidence: 0
            )
        }

        let palmWidthPx = palmWidthNormalized * Float(imageSize.width)
        let depth = (avgPalmWidthMeters * focalLengthPx) / palmWidthPx

        let cx = Float(wristNormalized.x - 0.5) * Float(imageSize.width)
        let cy = Float(0.5 - wristNormalized.y) * Float(imageSize.height)
        let worldX = cx * depth / focalLengthPx
        let worldY = cy * depth / focalLengthPx
        let worldZ = -depth

        let position = SIMD3<Float>(worldX, worldY, worldZ)

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
        let xAxis = forearm
        let yAxis = palmNormal
        let zAxis = normalize(cross(xAxis, yAxis))
        let yOrtho = normalize(cross(zAxis, xAxis))

        let rotMatrix = simd_float3x3(columns: (xAxis, yOrtho, zAxis))
        return simd_quatf(rotMatrix)
    }
}
