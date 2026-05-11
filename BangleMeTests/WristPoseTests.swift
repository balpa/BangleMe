import XCTest
import simd
@testable import BangleMe

final class WristPoseTests: XCTestCase {
    func test_identityPose_hasZeroPositionAndIdentityRotation() {
        let pose = WristPose.identity
        XCTAssertEqual(pose.position, SIMD3<Float>(0, 0, 0))
        XCTAssertEqual(pose.rotation.vector, simd_quatf(ix: 0, iy: 0, iz: 0, r: 1).vector)
        XCTAssertEqual(pose.confidence, 0)
    }

    func test_pose_storesPositionRotationAndConfidence() {
        let pose = WristPose(
            position: SIMD3<Float>(1, 2, 3),
            rotation: simd_quatf(angle: .pi / 2, axis: SIMD3<Float>(0, 1, 0)),
            confidence: 0.8
        )
        XCTAssertEqual(pose.position, SIMD3<Float>(1, 2, 3))
        XCTAssertEqual(pose.confidence, 0.8)
    }
}
