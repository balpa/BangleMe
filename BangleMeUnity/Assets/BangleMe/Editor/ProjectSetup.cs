using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace BangleMe.Editor
{
    // Rebuilds the main scene, its materials and the player settings from code, so the setup is
    // reviewable in git and reproducible on the Mac used for iOS builds.
    public static class ProjectSetup
    {
        const string Root = "Assets/BangleMe";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string MaterialsPath = Root + "/Materials";
        const string TexturesPath = Root + "/Textures";
        const string ThumbnailsPath = Root + "/UI/Thumbnails";
        const string EnvironmentPath = Root + "/Environment/Studio.hdr";
        const string PanelSettingsPath = Root + "/UI/PanelSettings.asset";
        const string BundleIdentifier = "com.bangleme.BangleMe";

        [MenuItem("BangleMe/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
            foreach (var directory in new[] { MaterialsPath, TexturesPath, ThumbnailsPath, Root + "/Scenes" })
            {
                Directory.CreateDirectory(directory);
            }
            AssetDatabase.Refresh();

            ConfigureRenderPipeline();
            ConfigureLayers();
            var designMaterials = new Material[BraceletDesign.Catalog.Length];
            for (var i = 0; i < designMaterials.Length; i++) designMaterials[i] = CreateDesignMaterial(BraceletDesign.Catalog[i]);
            var occluderMaterial = CreateMaterial("Arm Occluder", "BangleMe/DepthOccluder");
            var backgroundMaterial = CreateMaterial("Camera Background", "BangleMe/CameraBackground");
            var compositeMaterial = CreateMaterial("Bracelet Composite", "BangleMe/BraceletComposite");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.2f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            var studio = ImportEnvironment();
            RenderSettings.customReflectionTexture = studio;

            var lightObject = new GameObject("Key Light");
            lightObject.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;

            var thumbnails = DesignThumbnails.Render(designMaterials, ThumbnailsPath);

            var cameraObject = new GameObject("AR Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.allowHDR = false;

            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Camera Background";
            Object.DestroyImmediate(background.GetComponent<Collider>());
            background.transform.SetParent(cameraObject.transform, false);
            var backgroundRenderer = background.GetComponent<MeshRenderer>();
            backgroundRenderer.sharedMaterial = backgroundMaterial;
            backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            backgroundRenderer.receiveShadows = false;

            var rig = cameraObject.AddComponent<ArCameraRig>();
            Assign(rig, ("renderCamera", camera), ("background", backgroundRenderer));

            // The bracelets are drawn by a camera of their own and laid over the camera image by
            // a quad in front of the scene camera.
            camera.cullingMask = ~(1 << RenderLayers.Bracelets);
            var layerObject = new GameObject("Bracelet Layer");
            layerObject.transform.SetParent(cameraObject.transform, false);
            var layerCamera = layerObject.AddComponent<Camera>();
            layerCamera.clearFlags = CameraClearFlags.SolidColor;
            layerCamera.backgroundColor = Color.clear;
            layerCamera.cullingMask = (1 << RenderLayers.Bracelets) | (1 << RenderLayers.ArmOccluder);
            layerCamera.allowHDR = false;

            var composite = GameObject.CreatePrimitive(PrimitiveType.Quad);
            composite.name = "Bracelet Composite";
            Object.DestroyImmediate(composite.GetComponent<Collider>());
            composite.transform.SetParent(cameraObject.transform, false);
            composite.transform.localPosition = new Vector3(0f, 0f, 1f);
            composite.transform.localScale = new Vector3(100f, 100f, 1f);
            var compositeRenderer = composite.GetComponent<MeshRenderer>();
            compositeRenderer.sharedMaterial = compositeMaterial;
            compositeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            compositeRenderer.receiveShadows = false;

            var braceletLayer = layerObject.AddComponent<BraceletLayer>();
            Assign(braceletLayer, ("sceneCamera", camera), ("composite", compositeRenderer));

            var stackObject = new GameObject("Bracelets");
            stackObject.transform.SetParent(cameraObject.transform, false);
            var stack = stackObject.AddComponent<BraceletStack>();
            Assign(stack, ("occluderMaterial", occluderMaterial));
            AssignArray(stack, "designMaterials", designMaterials);

            var app = new GameObject("App");
            var feed = app.AddComponent<CameraFeed>();
            Assign(feed, ("blitShader", Shader.Find("Hidden/BangleMe/CameraBlit")));
            var reflection = app.AddComponent<SceneReflection>();
            Assign(reflection, ("shader", Shader.Find("Hidden/BangleMe/SceneReflection")), ("studio", studio));
            var pipeline = app.AddComponent<WristTrackingPipeline>();
            Assign(pipeline,
                ("cameraFeed", feed),
                ("cameraRig", rig),
                ("braceletStack", stack),
                ("keyLight", light),
                ("sceneReflection", reflection),
                ("handLandmarkerModel", AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Models/hand_landmarker.bytes")));

            var ui = new GameObject("UI");
            var document = ui.AddComponent<UIDocument>();
            document.panelSettings = CreatePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/TryOn.uxml");
            var screen = ui.AddComponent<TryOnScreen>();
            Assign(screen, ("pipeline", pipeline), ("braceletStack", stack), ("cameraFeed", feed));
            AssignArray(screen, "designThumbnails", thumbnails);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[BangleMe] Rebuilt {ScenePath}");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "BangleMe";
            PlayerSettings.productName = "BangleMe";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleIdentifier);
            PlayerSettings.iOS.cameraUsageDescription = "Bileziği bileğinde görebilmen için kameranı kullanıyoruz";
            PlayerSettings.iOS.targetOSVersionString = "16.0";

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        // The camera image must reach the screen untouched: an HDR color buffer bands its smooth
        // gradients (skin, walls) and a reduced render scale blurs it, so the pipeline stays LDR at
        // full resolution. MSAA keeps thin bangles from shimmering.
        static void ConfigureRenderPipeline()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                asset.supportsHDR = false;
                asset.msaaSampleCount = 4;
                asset.renderScale = 1f;
                EditorUtility.SetDirty(asset);
            }
        }

        static void ConfigureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(RenderLayers.Bracelets).stringValue = RenderLayers.BraceletsName;
            layers.GetArrayElementAtIndex(RenderLayers.ArmOccluder).stringValue = RenderLayers.ArmOccluderName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static PanelSettings CreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/BangleMeTheme.tss");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(390, 844);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static Cubemap ImportEnvironment()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(EnvironmentPath);
            if (importer.textureShape != TextureImporterShape.TextureCube)
            {
                importer.textureShape = TextureImporterShape.TextureCube;
                importer.maxTextureSize = 1024;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
                settings.seamlessCubemap = true;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Cubemap>(EnvironmentPath);
        }

        // The finish (color, polish) is applied per bracelet at runtime; the material carries
        // what belongs to the design: its surface pattern.
        static Material CreateDesignMaterial(BraceletDesign design)
        {
            var material = CreateMaterial(design.Name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", BraceletFinish.Catalog[0].Color);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", design.Smoothness);

            if (design.Shape == BraceletShape.Band && design.Pattern != BandPattern.None)
            {
                var (normal, occlusion) = PatternTextures.GetOrCreate(design.Pattern, TexturesPath);
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_OcclusionMap", occlusion);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            else
            {
                // Plain bands and twisted wire: fine polishing scratches, and darkening toward
                // the band's edges or into the groove between the wires.
                material.SetTexture("_BumpMap", PatternTextures.GetOrCreateBrushedNormal(TexturesPath));
                material.SetFloat("_BumpScale", 0.3f);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_OcclusionMap", PatternTextures.GetOrCreateEdgeOcclusion(TexturesPath));
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateMaterial(string name, string shaderName)
        {
            var path = $"{MaterialsPath}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new FileNotFoundException($"Shader not found: {shaderName}");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                // A material asset keeps the values it was saved with, so a default changed in the
                // shader would never reach it: start again from the shader's own defaults.
                var fresh = new Material(shader);
                EditorUtility.CopySerialized(fresh, material);
                material.name = name;
                Object.DestroyImmediate(fresh);
            }
            return material;
        }

        static void Assign(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in references)
            {
                if (value == null) throw new MissingReferenceException($"{target.GetType().Name}.{field} has nothing to reference");
                serialized.FindProperty(field).objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
