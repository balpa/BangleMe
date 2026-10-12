using System.IO;
using UnityEditor;
using UnityEngine;

namespace BangleMe.Editor
{
    // Renders one carousel picture per design from the real mesh and material, so the carousel
    // never drifts from what gets drawn on the wrist. Uses the open scene's lighting.
    public static class DesignThumbnails
    {
        const int Size = 256;
        static readonly Color CardColor = new Color32(28, 28, 30, 255);

        public static Texture2D[] Render(Material[] designMaterials, string directory)
        {
            var cameraObject = new GameObject("Thumbnail Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CardColor;
            camera.fieldOfView = 24f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5f;
            camera.transform.position = new Vector3(0f, 0f, -0.24f);

            var ring = new GameObject("Thumbnail Ring");
            ring.transform.rotation = Quaternion.Euler(-66f, 0f, 12f);
            var filter = ring.AddComponent<MeshFilter>();
            var renderer = ring.AddComponent<MeshRenderer>();

            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            camera.targetTexture = target;
            var capture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var paths = new string[BraceletDesign.Catalog.Length];

            for (var i = 0; i < paths.Length; i++)
            {
                var mesh = BraceletMeshFactory.Create(BraceletDesign.Catalog[i], highDetail: true);
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = designMaterials[i];
                camera.Render();

                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                RenderTexture.active = null;

                paths[i] = $"{directory}/Design {i}.png";
                File.WriteAllBytes(paths[i], capture.EncodeToPNG());
                Object.DestroyImmediate(mesh);
            }

            camera.targetTexture = null;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(ring);
            Object.DestroyImmediate(cameraObject);

            var thumbnails = new Texture2D[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                AssetDatabase.ImportAsset(paths[i]);
                var importer = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
                thumbnails[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
            }
            return thumbnails;
        }
    }
}
