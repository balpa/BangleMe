import Vision
import CoreVideo
import CoreGraphics
import ImageIO
import simd

public struct HandObservation {
    public let wrist: CGPoint
    public let indexBase: CGPoint
    public let littleBase: CGPoint
    public let middleTip: CGPoint
    public let palmWidthNormalized: Float
    public let forearmDirection: CGPoint
    public let palmNormal: SIMD3<Float>
    public let confidence: Float
}

public final class HandPoseDetector {
    private let request = VNDetectHumanHandPoseRequest()
    private let confidenceThreshold: Float = 0.3

    public init() {
        request.maximumHandCount = 2
    }

    public func detect(pixelBuffer: CVPixelBuffer, orientation: CGImagePropertyOrientation = .right) -> [HandObservation] {
        let handler = VNImageRequestHandler(cvPixelBuffer: pixelBuffer, orientation: orientation, options: [:])
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

        let forearmDir = CGPoint(x: wristPt.x - midPt.x, y: wristPt.y - midPt.y)
        let forearmLen = sqrt(forearmDir.x * forearmDir.x + forearmDir.y * forearmDir.y)
        let forearmNorm = forearmLen > 0
            ? CGPoint(x: forearmDir.x / forearmLen, y: forearmDir.y / forearmLen)
            : CGPoint(x: 0, y: -1)

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
