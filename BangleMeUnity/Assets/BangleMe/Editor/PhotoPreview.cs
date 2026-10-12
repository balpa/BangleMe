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

            var mixed = new int[24];
            for (var i = 0; i < mixed.Length; i++) mixed[i] = i % BraceletDesign.Catalog.Length;
            Render(DefaultPhotoPath, "mixed", mixed);

            if (!Directory.Exists(ExtraPhotosDirectory)) return;
            var few = new[] { 0, 1, 2, 3, 4, 5, 0, 3 };
            foreach (var photo in Directory.GetFiles(ExtraPhotosDirectory))
            {
                var extension = Path.GetExtension(photo).ToLowerInvariant();
                if (extension == ".jpg" || extension == ".jpeg" || extension == ".png") Render(photo, "mixed", few);
            }
        }

        static void Render(string photoPath, string label, int[] designs)
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
                    fit = new WristFitEstimator().Estimate(pose, measurement, 0);
                    found = pose.Confidence > 0;
                    Debug.Log($"[BangleMe] {label}: measured={measurement.Valid} width={measurement.WidthMeters * 1000f:0.0}mm " +
                              $"lateral={measurement.LateralMeters * 1000f:0.0}mm bend={measurement.BendRadians * Mathf.Rad2Deg:0.0}deg " +
                              $"halfWidth={fit.HalfWidth * 1000f:0.0}mm growth={fit.GrowthPerMeter:0.00}/m");
                }
            }

            foreach (var design in designs) stack.Add(design);
            if (found) stack.Apply(fit, true);

            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var capture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            capture.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllBytes($"{OutputDirectory}/{Path.GetFileNameWithoutExtension(photoPath)}_{label}.png", capture.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(photo);
            // Leaves the scene on disk untouched: the bracelets spawned above are preview-only.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
