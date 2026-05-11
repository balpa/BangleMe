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

    @Published var lastWristNormalized: CGPoint?
    @Published var lastConfidence: Float = 0
    @Published var lastPose: WristPose = .identity
    @Published var trackingState = TrackingState()
    @Published var statusText: String = "Starting..."

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

        guard let obs = observations.first, let est = estimator else {
            trackingState.update(detected: false, timestamp: timestamp)
            if lastWristNormalized != nil && trackingState.opacity <= 0.01 {
                lastWristNormalized = nil
            }
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
        statusText = String(format: "tracking · depth: %.2fm · conf: %.2f", -smoothedPos.z, obs.confidence)
    }
}
