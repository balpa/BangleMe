import XCTest
@testable import BangleMe

final class OneEuroFilterTests: XCTestCase {
    func test_firstSample_returnsItself() {
        var filter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
        let result = filter.filter(value: 5.0, timestamp: 0)
        XCTAssertEqual(result, 5.0, accuracy: 0.0001)
    }

    func test_constantSignal_remainsConstant() {
        var filter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
        _ = filter.filter(value: 10.0, timestamp: 0)
        let r1 = filter.filter(value: 10.0, timestamp: 0.016)
        let r2 = filter.filter(value: 10.0, timestamp: 0.032)
        XCTAssertEqual(r1, 10.0, accuracy: 0.01)
        XCTAssertEqual(r2, 10.0, accuracy: 0.01)
    }

    func test_noisySignal_isSmoothedAroundMean() {
        var filter = OneEuroFilter(minCutoff: 1.0, beta: 0.007)
        var t = 0.0
        var lastResult: Float = 0
        for i in 0..<30 {
            let noise: Float = i.isMultiple(of: 2) ? 0.1 : -0.1
            lastResult = filter.filter(value: 5.0 + noise, timestamp: t)
            t += 1.0 / 60.0
        }
        XCTAssertEqual(lastResult, 5.0, accuracy: 0.05)
    }
}
