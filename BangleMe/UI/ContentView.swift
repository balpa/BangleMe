import SwiftUI
import RealityKit
import simd
import CoreGraphics

@MainActor
final class BraceletSceneHolder: ObservableObject {
    let scene = BraceletScene()
    private(set) var didLoadBracelet = false

    func loadBracelet() {
        guard !didLoadBracelet else { return }
        defer { didLoadBracelet = true }

        let loader = BraceletLoader()
        do {
            let model = try loader.load(name: "classic_bangle")
            scene.setBracelet(model)
        } catch {
            print("[BraceletSceneHolder] USDZ load failed (\(error)). Using placeholder.")
            scene.setBracelet(BraceletLoader.placeholder())
        }
    }
}

struct ContentView: View {
    @StateObject private var tracker = WristTrackingPipeline()
    @StateObject private var sceneHolder = BraceletSceneHolder()

    var body: some View {
        ZStack {
            CameraPreviewView(session: tracker.camera.session)
                .ignoresSafeArea()

            BraceletSceneView(scene: sceneHolder.scene)
                .ignoresSafeArea()
                .allowsHitTesting(false)

            if tracker.showDebugSphere {
                DebugSphereOverlay(
                    wristNormalized: tracker.lastWristNormalized,
                    opacity: tracker.trackingState.opacity,
                    confidence: tracker.lastConfidence
                )
                .ignoresSafeArea()
            }

            VStack {
                HStack {
                    Spacer()
                    Toggle("Debug", isOn: $tracker.showDebugSphere)
                        .labelsHidden()
                        .tint(.yellow)
                        .padding(.top, 12)
                        .padding(.trailing, 16)
                }
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
        .onAppear {
            sceneHolder.loadBracelet()
            tracker.braceletScene = sceneHolder.scene
            tracker.start()
        }
        .onDisappear { tracker.stop() }
    }
}

#Preview {
    ContentView()
}
