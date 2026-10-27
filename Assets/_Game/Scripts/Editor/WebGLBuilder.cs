using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DontAnswer.EditorTools
{
    /// <summary>
    /// One-click itch.io build: Gzip with decompression fallback (itch serves .gz without the right headers),
    /// the PC quality level, and the game scene, into Builds/WebGL. Zip the folder's contents and upload.
    /// </summary>
    public static class WebGLBuilder
    {
        const string OutDir = "Builds/WebGL";

        [MenuItem("Don't Answer/Build WebGL (itch.io)", priority = 40)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Don't Answer] Stop Play Mode before building.");
                return;
            }
            PlayerSettings.productName = "Don't Answer";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            // Our own page (Assets/WebGLTemplates/DontAnswer): the game fills its frame, so fullscreen is really fullscreen.
            PlayerSettings.WebGL.template = "PROJECT:DontAnswer";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/Main.unity" },
                locationPathName = OutDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                string zip = Package();
                long bytes = new FileInfo(zip).Length;
                Debug.Log($"[Don't Answer] WebGL build succeeded in {s.totalTime.TotalMinutes:F1} min. Upload {Path.GetFullPath(zip)} ({bytes / 1048576f:F1} MB) to itch.io.");
            }
            else
                Debug.LogError($"[Don't Answer] WebGL build {s.result}: {s.totalErrors} error(s). See the console above.");
        }

        /// <summary>
        /// Moves Unity's "..._DoNotShip" debug folders out of the build and zips the rest with index.html at the root,
        /// which is what itch.io expects. Returns the zip path.
        /// </summary>
        [MenuItem("Don't Answer/Zip WebGL Build for itch.io", priority = 41)]
        public static void PackageMenu()
        {
            string zip = Package();
            Debug.Log($"[Don't Answer] Packaged {Path.GetFullPath(zip)} ({new FileInfo(zip).Length / 1048576f:F1} MB). Upload it to itch.io.");
        }

        static string Package()
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(OutDir));
            foreach (var dir in Directory.GetDirectories(OutDir, "*_DoNotShip"))
            {
                string target = Path.Combine(parent, Path.GetFileName(dir));
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.Move(dir, target);
            }
            string zip = Path.Combine(parent, "DontAnswer-WebGL.zip");
            if (File.Exists(zip)) File.Delete(zip);
            System.IO.Compression.ZipFile.CreateFromDirectory(OutDir, zip, System.IO.Compression.CompressionLevel.Optimal, false);
            return zip;
        }
    }
}
