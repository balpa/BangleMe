import RealityKit
import Foundation

enum BraceletLoaderError: Error, Equatable {
    case modelNotFound
    case loadFailed(String)
}

struct BraceletLoader {
    func load(name: String) throws -> ModelEntity {
        guard let url = Bundle.main.url(forResource: name, withExtension: "usdz") else {
            throw BraceletLoaderError.modelNotFound
        }
        let entity: ModelEntity
        do {
            entity = try ModelEntity.loadModel(contentsOf: url)
        } catch {
            throw BraceletLoaderError.loadFailed(String(describing: error))
        }
        Self.applyGoldMaterial(to: entity)
        return entity
    }

    static func placeholder() -> ModelEntity {
        let mesh = MeshResource.generateBox(
            size: SIMD3<Float>(0.06, 0.02, 0.06),
            cornerRadius: 0.01
        )
        return ModelEntity(mesh: mesh, materials: [GoldMaterial.warmYellow()])
    }

    static func applyGoldMaterial(to entity: Entity) {
        if let modelEntity = entity as? ModelEntity, var model = modelEntity.model {
            model.materials = model.materials.map { _ in GoldMaterial.warmYellow() }
            modelEntity.model = model
        }
        for child in entity.children {
            applyGoldMaterial(to: child)
        }
    }
}
