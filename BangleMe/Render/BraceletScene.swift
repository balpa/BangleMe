import RealityKit
import CoreGraphics
import simd
import UIKit

final class BraceletScene {
    let arView: ARView

    private let cameraAnchor = AnchorEntity(world: .zero)
    private let perspectiveCamera = PerspectiveCamera()

    private let braceletAnchor = AnchorEntity(world: .zero)
    private var bracelet: BraceletEntity?

    init() {
        let view = ARView(
            frame: .zero,
            cameraMode: .nonAR,
            automaticallyConfigureSession: false
        )
        view.environment.background = .color(.clear)
        view.renderOptions.insert(.disableMotionBlur)
        view.renderOptions.insert(.disableDepthOfField)
        view.renderOptions.insert(.disableGroundingShadows)
        self.arView = view

        perspectiveCamera.camera.near = 0.01
        perspectiveCamera.camera.far = 5.0
        perspectiveCamera.camera.fieldOfViewInDegrees = 60
        cameraAnchor.addChild(perspectiveCamera)
        view.scene.addAnchor(cameraAnchor)

        view.scene.addAnchor(braceletAnchor)

        loadStudioIBL()
    }

    func configureCamera(focalLengthPx: Float, imageSize: CGSize) {
        guard focalLengthPx > 0, imageSize.height > 0 else { return }
        let vFovRadians = 2 * atan(Float(imageSize.height) / (2 * focalLengthPx))
        perspectiveCamera.camera.fieldOfViewInDegrees = vFovRadians * 180 / .pi
    }

    func setBracelet(_ model: ModelEntity) {
        braceletAnchor.children.removeAll()
        braceletAnchor.addChild(model)
        bracelet = BraceletEntity(entity: model)
        bracelet?.applyPose(.identity)
    }

    func updatePose(_ pose: WristPose) {
        bracelet?.applyPose(pose)
    }

    private func loadStudioIBL() {
        do {
            let resource = try EnvironmentResource.load(named: "studio")
            arView.environment.lighting.resource = resource
            arView.environment.lighting.intensityExponent = 1.0
        } catch {
            print("[BraceletScene] HDRI load failed (\(error)). Using default lighting.")
        }
    }
}
