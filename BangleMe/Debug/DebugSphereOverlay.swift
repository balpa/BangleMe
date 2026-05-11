import SwiftUI

struct DebugSphereOverlay: View {
    let wristNormalized: CGPoint?
    let opacity: Float
    let confidence: Float

    var body: some View {
        GeometryReader { geo in
            if let wrist = wristNormalized {
                Circle()
                    .strokeBorder(Color.yellow, lineWidth: 3)
                    .background(Circle().fill(Color.yellow.opacity(0.3)))
                    .frame(width: 60, height: 60)
                    .position(
                        x: wrist.x * geo.size.width,
                        y: wrist.y * geo.size.height
                    )
                    .opacity(Double(opacity))

                Text(String(format: "conf: %.2f", confidence))
                    .font(.caption.monospaced())
                    .foregroundColor(.yellow)
                    .padding(4)
                    .background(Color.black.opacity(0.5))
                    .cornerRadius(4)
                    .position(
                        x: wrist.x * geo.size.width,
                        y: wrist.y * geo.size.height + 50
                    )
                    .opacity(Double(opacity))
            }
        }
        .allowsHitTesting(false)
    }
}
