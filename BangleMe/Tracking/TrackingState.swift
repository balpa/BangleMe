import Foundation

public struct TrackingState {
    public private(set) var opacity: Float = 0
    public private(set) var isTracking: Bool = false

    private var lastDetectedTimestamp: Double?
    private var lastUpdateTimestamp: Double?

    private let fadeInDuration: Float = 0.2
    private let fadeOutDuration: Float = 0.3

    public init() {}

    public mutating func update(detected: Bool, timestamp: Double) {
        defer {
            isTracking = detected
            lastUpdateTimestamp = timestamp
            if detected { lastDetectedTimestamp = timestamp }
        }

        guard let lastUpdate = lastUpdateTimestamp else {
            if detected { lastDetectedTimestamp = timestamp }
            return
        }
        let dt = Float(timestamp - lastUpdate)

        if detected {
            opacity = min(1.0, opacity + dt / fadeInDuration)
        } else {
            opacity = max(0.0, opacity - dt / fadeOutDuration)
        }
    }
}
