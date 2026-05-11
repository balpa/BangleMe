import SwiftUI
import simd
import CoreGraphics

struct ContentView: View {
    @StateObject private var tracker = WristTrackingPipeline()

    var body: some View {
        ZStack {
            CameraPreviewView(session: tracker.camera.session)
                .ignoresSafeArea()

            DebugSphereOverlay(
                wristNormalized: tracker.lastWristNormalized,
                opacity: tracker.trackingState.opacity,
                confidence: tracker.lastConfidence
            )
            .ignoresSafeArea()

            VStack {
                Spacer()
                Text(tracker.statusText)
                    .font(.caption.monospaced())
                    .foregroundColor(.white)
                    .padding(8)
                    .background(Color.black.opacity(0.6))
                    .cornerRadius(8)
                    .padding(.bottom, 40)
            }
        }
        .preferredColorScheme(.dark)
        .onAppear { tracker.start() }
        .onDisappear { tracker.stop() }
    }
}

#Preview {
    ContentView()
}
