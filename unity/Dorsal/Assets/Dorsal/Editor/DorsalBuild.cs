using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dorsal.Unity.Editor
{
    /// <summary>
    /// Menú "Dorsal" del editor: configura el proyecto para Android y genera compilaciones.
    /// Identificador de aplicación provisional: cámbialo antes de publicar (debe ser único en Google Play).
    /// </summary>
    public static class DorsalBuild
    {
        public const string AppId = "com.dorsal.footballcareer";
        private const string OutDir = "Builds/Android";

        [MenuItem("Dorsal/Configurar proyecto para Android")]
        public static void ConfigureAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Dorsal";
            PlayerSettings.productName = "DORSAL";
            PlayerSettings.bundleVersion = DorsalApp.Version;
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26; // Android 8.0
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            Debug.Log("[Dorsal] Proyecto configurado para Android (IL2CPP, ARM64, API mínima 26, horizontal).");
        }

        [MenuItem("Dorsal/Compilar APK de desarrollo (con Profiler)")]
        public static void BuildDevelopmentApk() => Build(development: true, appBundle: false);

        [MenuItem("Dorsal/Compilar APK de prueba (release)")]
        public static void BuildReleaseApk() => Build(development: false, appBundle: false);

        [MenuItem("Dorsal/Compilar AAB para Google Play")]
        public static void BuildAab() => Build(development: false, appBundle: true);

        private static void Build(bool development, bool appBundle)
        {
            ConfigureAndroid();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                // El juego se monta solo (DorsalApp), basta con una escena vacía.
                var any = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
                if (any == null)
                {
                    Debug.LogError("[Dorsal] No hay ninguna escena en Assets. Crea una escena vacía y vuelve a intentarlo.");
                    return;
                }
                scenes = new[] { any };
            }
            EditorUserBuildSettings.buildAppBundle = appBundle;
            Directory.CreateDirectory(OutDir);
            string file = Path.Combine(OutDir, $"dorsal-{DorsalApp.Version}{(development ? "-dev" : "")}.{(appBundle ? "aab" : "apk")}");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = file,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"[Dorsal] Compilación correcta: {file} ({report.summary.totalSize / (1024 * 1024)} MB)");
            else
                Debug.LogError($"[Dorsal] La compilación falló: {report.summary.result}. Revisa la consola.");
        }
    }
}
