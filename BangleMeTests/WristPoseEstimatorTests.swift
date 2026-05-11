import XCTest
import simd
@testable import BangleMe

final class WristPoseEstimatorTests: XCTestCase {
    let estimator = WristPoseEstimator(
        focalLengthPx: 1000,
        imageSize: CGSize(width: 1920, height: 1080),
        avgPalmWidthMeters: 0.075
    )

    func test_handCentered_andPalmFillsTenthOfImageWidth_yieldsExpectedDepth() {
        let wrist2D = CGPoint(x: 0.5, y: 0.5)
        let palmWidth2D: Float = 192.0 / 1920.0

        let pose = estimator.estimate(
            wristNormalized: wrist2D,
            palmWidthNormalized: palmWidth2D,
            forearmDirectionNormalized: CGPoint(x: 0, y: -1),
            palmNormalNormalized: SIMD3<Float>(0, 0, 1),
            confidence: 0.9
        )

        XCTAssertEqual(pose.position.z, -0.39, accuracy: 0.02)
        XCTAssertEqual(pose.position.x, 0, accuracy: 0.001)
        XCTAssertEqual(pose.position.y, 0, accuracy: 0.001)
        XCTAssertEqual(pose.confidence, 0.9)
    }

    func test_handToRight_yieldsPositiveX() {
        let pose = estimator.estimate(
            wristNormalized: CGPoint(x: 0.75, y: 0.5),
            palmWidthNormalized: 0.1,
            forearmDirectionNormalized: CGPoint(x: 0, y: -1),
            palmNormalNormalized: SIMD3<Float>(0, 0, 1),
            confidence: 0.8
        )
        XCTAssertGreaterThan(pose.position.x, 0)
    }

    func test_zeroPalmWidth_returnsLowConfidence() {
        let pose = estimator.estimate(
            wristNormalized: CGPoint(x: 0.5, y: 0.5),
            palmWidthNormalized: 0,
            forearmDirectionNormalized: CGPoint(x: 0, y: -1),
            palmNormalNormalized: SIMD3<Float>(0, 0, 1),
            confidence: 0.5
        )
        XCTAssertEqual(pose.confidence, 0, "Zero palm width = unreliable depth")
    }
}
