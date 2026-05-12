import SwiftUI
import RealityKit

struct BraceletSceneView: UIViewRepresentable {
    let scene: BraceletScene

    func makeUIView(context: Context) -> ARView {
        return scene.arView
    }

    func updateUIView(_ uiView: ARView, context: Context) {}
}
