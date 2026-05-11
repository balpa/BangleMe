import Foundation

public struct OneEuroFilter {
    public let minCutoff: Float
    public let beta: Float
    public let dCutoff: Float

    private var lastValue: Float = 0
    private var lastDerivative: Float = 0
    private var lastTimestamp: Double = -1

    public init(minCutoff: Float = 1.0, beta: Float = 0.007, dCutoff: Float = 1.0) {
        self.minCutoff = minCutoff
        self.beta = beta
        self.dCutoff = dCutoff
    }

    public mutating func filter(value: Float, timestamp: Double) -> Float {
        defer { lastTimestamp = timestamp }
        guard lastTimestamp >= 0 else {
            lastValue = value
            lastDerivative = 0
            return value
        }
        let dt = Float(timestamp - lastTimestamp)
        guard dt > 0 else { return lastValue }

        let dv = (value - lastValue) / dt
        let dAlpha = smoothingAlpha(cutoff: dCutoff, dt: dt)
        let smoothedDerivative = dAlpha * dv + (1 - dAlpha) * lastDerivative

        let cutoff = minCutoff + beta * abs(smoothedDerivative)
        let alpha = smoothingAlpha(cutoff: cutoff, dt: dt)
        let smoothed = alpha * value + (1 - alpha) * lastValue

        lastValue = smoothed
        lastDerivative = smoothedDerivative
        return smoothed
    }

    private func smoothingAlpha(cutoff: Float, dt: Float) -> Float {
        let tau = 1.0 / (2 * .pi * cutoff)
        return 1.0 / (1.0 + tau / dt)
    }
}
