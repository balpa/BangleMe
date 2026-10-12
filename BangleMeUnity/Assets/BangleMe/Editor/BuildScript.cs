using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BangleMe.Editor
{
    public static class BuildScript
    {
        [MenuItem("BangleMe/Build/Android APK")]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android, "Builds/Android/BangleMe.apk");
        }

        // Produces an Xcode project; signing and upload happen in Xcode on the Mac.
        [MenuItem("BangleMe/Build/iOS Xcode Project")]
        public static void BuildIOS()
        {
            Build(BuildTarget.iOS, "Builds/iOS");
        }

        static void Build(BuildTarget target, string outputPath)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"{target} build {summary.result}: {summary.totalErrors} error(s)");
            }
        }
    }
}
