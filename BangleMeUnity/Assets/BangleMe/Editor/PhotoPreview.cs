using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BangleMe.Editor
{
    // Renders the main scene over a still photo instead of the live camera, to check tracking
    // alignment and the look of the bracelets without holding a wrist up to the camera.
    public static class PhotoPreview
    {
        const string ScenePath = "Assets/BangleMe/Scenes/Main.unity";
        const string ModelPath = "Assets/BangleMe/Models/hand_landmarker.bytes";
        const string DefaultPhotoPath = "Assets/BangleMe/Tests/Editor/Fixtures/pointing_up.jpg";
        const string OutputDirectory = "Logs/BangleMePreview";
        // Drop your own wrist photos here (project root, not tracked by git) to preview on them.
        const string ExtraPhotosDirectory = "PreviewPhotos";

        [MenuItem("BangleMe/Render Preview From Photo")]
        public static void RenderDefault()
        {
            for (var design = 0; design < BraceletDesign.Catalog.Length; design++)
            {
                Render(DefaultPhotoPath, $"design{design}", new[] { design, design, design });
            }

            var thinSet = new int[49];
            for (var i = 0; i < thinSet.Length; i++) thinSet[i] = BraceletDesign.ThinSetDesign(i + 1);
            Render(DefaultPhotoPath, "set", thinSet);

            var flatSet = new int[48];
            for (var i = 0; i < flatSet.Length; i++) flatSet[i] = BraceletDesign.Catalog.Length - 1;

            var mixed = new int[24];
            for (var i = 0; i < mixed.Length; i++) mixed[i] = i % BraceletDesign.Catalog.Length;
            Render(DefaultPhotoPath, "mixed", mixed);

            if (!Directory.Exists(ExtraPhotosDirectory)) return;
            var few = new[] { 0, 1, 2, 3, 4, 5, 0, 3 };
            foreach (var photo in Directory.GetFiles(ExtraPhotosDirectory))
            {
                var extension = Path.GetExtension(photo).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png") continue;
                Render(photo, "mixed", few);
                Render(photo, "set", thinSet);
                for (var finish = 1; finish < BraceletFinish.Catalog.Length; finish++) Render(photo, $"set_finish{finish}", thinSet, finish);
                Render(photo, "flat_rose", flatSet, 1);
            }
        }

        static void Render(string photoPath, string label, int[] designs, int finish = 0)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var rig = Object.FindFirstObjectByType<ArCameraRig>();
            var stack = Object.FindFirstObjectByType<BraceletStack>();
            var camera = rig.GetComponent<Camera>();

            var photo = new Texture2D(2, 2);
            photo.LoadImage(File.ReadAllBytes(photoPath));
            var scale = Mathf.Max(1, 1600 / photo.width);
            var target = new RenderTexture(photo.width * scale, photo.height * scale, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 4
            };
            camera.targetTexture = target;
            rig.Configure(photo);

            foreach (var design in designs) stack.Add(design);
            stack.FinishIndex = finish;

            var observation = new HandObservation();
            var fit = default(WristFit);
            var found = false;
            using (var tracker = new HandTracker(AssetDatabase.LoadAssetAtPath<TextAsset>(ModelPath).bytes, liveStream: false))
            {
                if (tracker.Detect(photo, observation) && tracker.TryGetFrame(observation.TimestampMs, out var pixels, out var width, out var height))
                {
                    var solver = new WristPoseSolver(rig.FocalLengthPx, new Vector2(photo.width, photo.height));
                    var pose = solver.Solve(observation.ImagePoints, observation.WorldPoints, observation.Confidence);
                    var measurement = WristMeasurer.Measure(pixels.AsReadOnlySpan(), width, height, observation.ImagePoints, observation.WorldPoints);
                    var pipeline = Object.FindFirstObjectByType<WristTrackingPipeline>();
                    pipeline.MatchSceneLighting(pixels.AsReadOnlySpan(), measurement, 1f);
                    fit = new WristFitEstimator().Estimate(pose, measurement, 0, rig.FocalLengthPx * width / photo.width);
                    pipeline.RefreshReflections(photo, fit);
                    found = pose.Confidence > 0;
                    Debug.Log($"[BangleMe] {label}: measured={measurement.Valid} width={measurement.Width:0.0}px offset={measurement.Offset} " +
                              $"direction={measurement.Direction} shading={measurement.Shading} halfWidth={fit.HalfWidth * 1000f:0.0}mm growth={fit.GrowthPerMeter:0.00}/m");
                }
            }

            // Long enough for the bangles to come to rest where they would on this arm.
            if (found) stack.Apply(fit, true, 3f);

            Object.FindFirstObjectByType<BraceletLayer>().RenderNow();
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            capture.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllBytes($"{OutputDirectory}/{Path.GetFileNameWithoutExtension(photoPath)}_{label}.png", capture.EncodeToPNG());

            SaveScene(camera, stack, $"{OutputDirectory}/{Path.GetFileNameWithoutExtension(photoPath)}_{label}_scene.json");
            SaveCoverage(Object.FindFirstObjectByType<BraceletLayer>().Texture as RenderTexture,
                $"{OutputDirectory}/{Path.GetFileNameWithoutExtension(photoPath)}_{label}_mask.png");

            camera.targetTexture = null;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(photo);
            // Leaves the scene on disk untouched: the bracelets spawned above are preview-only.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // Which pixels the bracelets cover, for anything that reworks only those.
        static void SaveCoverage(RenderTexture layer, string path)
        {
            if (layer == null) return;
            var previous = RenderTexture.active;
            RenderTexture.active = layer;
            var pixels = new Texture2D(layer.width, layer.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, layer.width, layer.height), 0, 0);
            RenderTexture.active = previous;

            var colors = pixels.GetPixels32();
            for (var index = 0; index < colors.Length; index++)
            {
                var coverage = colors[index].a;
                colors[index] = new Color32(coverage, coverage, coverage, 255);
            }
            pixels.SetPixels32(colors);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
        }

        [System.Serializable]
        sealed class SceneExport
        {
            public float verticalFovDegrees;
            public int width;
            public int height;
            public Vector3 towardLight;
            public Color metal;
            public RingExport[] rings;
            public int armColumns;
            public Vector3[] armVertices;
        }

        [System.Serializable]
        sealed class RingExport
        {
            public string design;
            public Vector3 center;
            public Vector3 axis;
            public float innerRadius;
            public float width;
            public float thickness;
        }

        // Where everything sits as the camera sees it, for an offline renderer to draw the same
        // bracelets on the same photo.
        static void SaveScene(Camera camera, BraceletStack stack, string path)
        {
            var view = camera.transform;
            var rings = new System.Collections.Generic.List<RingExport>();
            var export = new SceneExport
            {
                verticalFovDegrees = camera.fieldOfView,
                width = camera.pixelWidth,
                height = camera.pixelHeight,
                towardLight = view.InverseTransformDirection(stack.transform.parent.TransformDirection(stack.LightDirection)),
                metal = stack.FinishColor,
            };

            foreach (var renderer in stack.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (renderer.gameObject.layer == RenderLayers.ArmOccluder)
                {
                    var vertices = mesh.vertices;
                    for (var i = 0; i < vertices.Length; i++) vertices[i] = view.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i]));
                    export.armVertices = vertices;
                    export.armColumns = ArmColumns(vertices.Length);
                    continue;
                }

                var design = System.Array.Find(BraceletDesign.Catalog, candidate => candidate.Name == mesh.name);
                rings.Add(new RingExport
                {
                    design = mesh.name,
                    center = view.InverseTransformPoint(renderer.transform.position),
                    axis = view.InverseTransformDirection(renderer.transform.up),
                    innerRadius = BraceletMeshFactory.ReferenceInnerRadius * renderer.transform.lossyScale.x,
                    width = design.Width,
                    thickness = design.Thickness,
                });
            }

            export.rings = rings.ToArray();
            File.WriteAllText(path, JsonUtility.ToJson(export));
        }

        // The arm stand-in is a grid whose rows run along the arm; its column count is whichever
        // divisor of the vertex count the mesh was built with.
        static int ArmColumns(int vertexCount)
        {
            for (var columns = 40; columns > 2; columns--)
            {
                if (vertexCount % columns == 0) return columns;
            }
            return vertexCount;
        }
    }
}
