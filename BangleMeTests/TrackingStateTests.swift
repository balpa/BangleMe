import XCTest
@testable import BangleMe

final class TrackingStateTests: XCTestCase {
    func test_initialState_isLost() {
        let state = TrackingState()
        XCTAssertEqual(state.opacity, 0)
        XCTAssertFalse(state.isTracking)
    }

    func test_detectedHand_increasesOpacityOverFadeInDuration() {
        var state = TrackingState()
        state.update(detected: true, timestamp: 0)
        state.update(detected: true, timestamp: 0.1)
        XCTAssertGreaterThan(state.opacity, 0)
        XCTAssertLessThan(state.opacity, 1)

        state.update(detected: true, timestamp: 0.3)
        XCTAssertEqual(state.opacity, 1, accuracy: 0.01)
    }

    func test_lostHand_fadesOutAfterDelay() {
        var state = TrackingState()
        state.update(detected: true, timestamp: 0)
        state.update(detected: true, timestamp: 0.3)
        XCTAssertEqual(state.opacity, 1, accuracy: 0.01)

        state.update(detected: false, timestamp: 0.4)
        XCTAssertGreaterThan(state.opacity, 0.5)

        state.update(detected: false, timestamp: 0.71)
        XCTAssertEqual(state.opacity, 0, accuracy: 0.05)
    }
}
