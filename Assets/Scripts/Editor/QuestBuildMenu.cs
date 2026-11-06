using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Kyalio.EditorTools
{
    /// <summary>
    /// Build menu for Meta Quest APKs.
    ///
    /// Quest Dev Build         — development APK; version code unchanged.
    /// Quest Dev Build and Run — same, then installs and launches it on the connected Quest.
    /// Quest Release           — bumps the Android version code, builds a release APK
    ///                           and verifies it is signed with the Store key.
    ///                           The version code is rolled back if the build fails.
    /// Check Quest Build Setup — runs the setup checks without building.
    ///
    /// Release builds are also blocked while a build scene has fake-data mode on
    /// (AppBootstrapper / DevBootstrapper "_useFakeData"), so a dev setup cannot ship.
    ///
    /// Output: Builds/SurgeryTraining_{yyyyMMdd}_{versionCode}[_dev].apk
    ///
    /// Keystore passwords come from environment variables so they never land in the repo:
    ///   KYALIO_KEYSTORE_PASS — keystore password
    ///   KYALIO_KEYALIAS_PASS — key alias password (falls back to the keystore password)
    /// </summary>
    public static class QuestBuildMenu
    {
        private const string Title = "Quest Build";
        private const string OutputDir = "Builds";
        private const string ApkPrefix = "SurgeryTraining";
        private const string DefaultKeystoreFile = "kyalioquest.keystore";
        private const string InProjectPrefix = "{inproject}:";
        private const string KeyAlias = "healthproduct";
        private const string KeystorePassEnv = "KYALIO_KEYSTORE_PASS";
        private const string KeyaliasPassEnv = "KYALIO_KEYALIAS_PASS";

        // SHA-256 of kyalioquest.keystore / healthproduct, the key already used on the Horizon Store.
        private const string StoreCertSha256 = "f775acfa1f3eb8279ff3c198bdf3d7074033d753f2c30223dc07924d55ae2573";

        private static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor;

        [MenuItem("Build/Quest Dev Build")]
        private static void BuildDev()
        {
            if (!Preflight(showWhenReady: false)) return;

            var code = PlayerSettings.Android.bundleVersionCode;
            Build(ApkPath(code, "_dev"), BuildOptions.Development);
        }

        [MenuItem("Build/Quest Dev Build and Run")]
        private static void BuildDevAndRun()
        {
            if (!Preflight(showWhenReady: false)) return;

            // AutoRunPlayer installs the APK on the connected Quest over adb and launches it.
            var code = PlayerSettings.Android.bundleVersionCode;
            Build(ApkPath(code, "_dev"), BuildOptions.Development | BuildOptions.AutoRunPlayer);
        }

        [MenuItem("Build/Quest Release")]
        private static void BuildRelease()
        {
            if (!Preflight(showWhenReady: false, release: true)) return;

            var previousCode = PlayerSettings.Android.bundleVersionCode;
            var code = previousCode + 1;

            var confirmed = EditorUtility.DisplayDialog("Quest Release",
                $"Package：      {PlayerSettings.applicationIdentifier}\n" +
                $"Version name：{PlayerSettings.bundleVersion}\n" +
                $"Version code：{previousCode} → {code}\n\n" +
                "Version name 是否已更新？\n（Project Settings > Player > Version）",
                "Build", "取消");
            if (!confirmed) return;

            SetVersionCode(code);

            var path = ApkPath(code, "");
            if (!Build(path, BuildOptions.None))
            {
                SetVersionCode(previousCode);
                EditorUtility.DisplayDialog("Quest Release",
                    $"Build 失敗，version code 已退回 {previousCode}。\n錯誤訊息請看 Console。", "OK");
                return;
            }

            if (!VerifyStoreSignature(path))
            {
                SetVersionCode(previousCode);
                EditorUtility.DisplayDialog("Quest Release",
                    "APK 不是用 Store 的 key 簽的，請勿上傳！\n" +
                    $"version code 已退回 {previousCode}，詳細內容請看 Console。", "OK");
                return;
            }

            EditorUtility.RevealInFinder(path);
            EditorUtility.DisplayDialog("Quest Release",
                $"完成：{Path.GetFileName(path)}\n\n" +
                "請 commit ProjectSettings.asset，讓新的 version code 進 repo：\n" +
                $"chore(settings): bump version code to {code}",
                "OK");
        }

        [MenuItem("Build/Check Quest Build Setup")]
        private static void CheckSetup() => Preflight(showWhenReady: true, release: true);

        /// <summary>
        /// Checks build target, keystore and passwords. Shows a checklist with
        /// the exact fix for every failing item; applies signing settings when ready.
        /// <paramref name="release"/> adds the checks that only matter for a Store build.
        /// </summary>
        private static bool Preflight(bool showWhenReady, bool release = false)
        {
            var lines = new List<string>();
            var fixes = new List<string>();
            var ready = true;

            // Build target
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                lines.Add("[✓] Build target：Android");
            }
            else
            {
                ready = false;
                lines.Add($"[✗] Build target：{EditorUserBuildSettings.activeBuildTarget}");
                fixes.Add("File > Build Profiles，選 Android 後按 Switch Platform。");
            }

            // Keystore file
            var keystorePath = ResolveKeystorePath();
            var keystoreFound = File.Exists(keystorePath);
            if (keystoreFound)
            {
                lines.Add($"[✓] Keystore：{keystorePath}");
            }
            else
            {
                ready = false;
                lines.Add($"[✗] Keystore：找不到\n      預期位置：{keystorePath}");
                fixes.Add($"從雲端備份把 {Path.GetFileName(keystorePath)} 複製到：\n      {Path.GetDirectoryName(keystorePath)}");
            }

            // Passwords
            var keystorePass = Environment.GetEnvironmentVariable(KeystorePassEnv);
            var keyaliasPass = Environment.GetEnvironmentVariable(KeyaliasPassEnv);
            var needsRestart = false;

            if (!string.IsNullOrEmpty(keystorePass))
            {
                lines.Add($"[✓] {KeystorePassEnv}：已設定");
            }
            else
            {
                ready = false;
                if (IsSetForUserButNotLoaded(KeystorePassEnv))
                {
                    needsRestart = true;
                    lines.Add($"[✗] {KeystorePassEnv}：已設定，但 Unity 還沒讀到（需要重開）");
                }
                else
                {
                    lines.Add($"[✗] {KeystorePassEnv}：未設定");
                    fixes.Add("開啟「命令提示字元」，輸入以下指令（把 <> 換成實際密碼，保留雙引號）：\n" +
                              $"      setx {KeystorePassEnv} \"<keystore 密碼>\"\n" +
                              $"      setx {KeyaliasPassEnv} \"<alias {KeyAlias} 的密碼>\"\n" +
                              "      兩個密碼相同時，第二行可省略。\n" +
                              "      密碼含 \" 或 % 時，改用 Project Settings 手動輸入。");
                    needsRestart = true;
                }
            }

            if (!string.IsNullOrEmpty(keyaliasPass))
                lines.Add($"[✓] {KeyaliasPassEnv}：已設定");
            else if (IsSetForUserButNotLoaded(KeyaliasPassEnv))
            {
                needsRestart = true;
                lines.Add($"[–] {KeyaliasPassEnv}：已設定，但 Unity 還沒讀到（需要重開）");
            }
            else
                lines.Add($"[–] {KeyaliasPassEnv}：未設定（將使用 keystore 密碼）");

            if (needsRestart)
                fixes.Add("關閉 Unity，並從右下角系統列「結束」Unity Hub，再重新開啟專案。\n" +
                          "      （環境變數只在程式啟動時讀取一次）");

            // Password check against the actual keystore, before spending minutes on a build
            if (keystoreFound && !string.IsNullOrEmpty(keystorePass))
            {
                var (ok, detail) = CheckKeystorePassword(keystorePath);
                if (ok)
                {
                    lines.Add($"[✓] Keystore 密碼正確，alias「{KeyAlias}」存在");
                }
                else
                {
                    ready = false;
                    lines.Add($"[✗] Keystore 驗證失敗：{detail}");
                    fixes.Add($"用 setx 重新設定正確的 {KeystorePassEnv}，然後重開 Unity Hub。");
                }
            }

            // Fake-data mode skips login and the API — fine for a dev build, never for the Store.
            if (release)
            {
                var fakeDataScenes = FindFakeDataScenes();
                if (fakeDataScenes.Count == 0)
                {
                    lines.Add("[✓] Fake data：關閉");
                }
                else
                {
                    ready = false;
                    lines.Add($"[✗] Fake data：開啟中（{string.Join("、", fakeDataScenes)}）");
                    fixes.Add("在場景的 AppBootstrapper（或 DevBootstrapper）取消勾選 Use Fake Data，並存檔場景。");
                }
            }

            var report = string.Join("\n", lines);
            if (fixes.Count > 0)
                report += "\n\n要做的事：\n" + string.Join("\n", fixes.Select((f, i) => $"  {i + 1}. {f}"));

            if (!ready)
            {
                Debug.LogWarning($"[QuestBuild] 設定尚未完成\n{report}");
                var choice = EditorUtility.DisplayDialogComplex(Title + "：設定尚未完成", report,
                    "複製 setx 指令", "關閉", "開啟專案資料夾");
                if (choice == 0)
                {
                    EditorGUIUtility.systemCopyBuffer =
                        $"setx {KeystorePassEnv} \"<keystore 密碼>\"\r\nsetx {KeyaliasPassEnv} \"<alias {KeyAlias} 的密碼>\"";
                    Debug.Log("[QuestBuild] setx 指令已複製到剪貼簿，貼上後把 <> 換成實際密碼。");
                }
                else if (choice == 2)
                {
                    EditorUtility.RevealInFinder(keystoreFound ? keystorePath : Path.Combine(ProjectRoot, "Assets"));
                }
                return false;
            }

            // Passwords are only held in memory for this Editor session; Unity does not serialize them.
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyaliasPass) ? keystorePass : keyaliasPass;
            EditorUserBuildSettings.buildAppBundle = false;

            Debug.Log($"[QuestBuild] 設定完成\n{report}");
            if (showWhenReady)
                EditorUtility.DisplayDialog(Title + "：設定完成", report, "OK");
            return true;
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// Enabled build scenes that have a "_useFakeData: 1" component. Reads the saved scene
        /// files, which is what the build uses — an unsaved Inspector change does not count.
        /// The field name is only used by AppBootstrapper and DevBootstrapper.
        /// </summary>
        private static List<string> FindFakeDataScenes() =>
            EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Where(s => File.ReadAllText(s.path).Contains("_useFakeData: 1"))
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .ToList();

        /// <summary>Resolves Player Settings' keystore name ("{inproject}: file" or an absolute path).</summary>
        private static string ResolveKeystorePath()
        {
            var name = PlayerSettings.Android.keystoreName;
            if (string.IsNullOrWhiteSpace(name))
                name = DefaultKeystoreFile;
            else if (name.StartsWith(InProjectPrefix))
                name = name.Substring(InProjectPrefix.Length).Trim();

            return Path.GetFullPath(Path.IsPathRooted(name) ? name : Path.Combine(ProjectRoot, name));
        }

        /// <summary>True when the variable exists in the user's registry but not in this process (Editor not restarted).</summary>
        private static bool IsSetForUserButNotLoaded(string name) =>
            IsWindows && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User));

        private static (bool ok, string detail) CheckKeystorePassword(string keystorePath)
        {
            var keytool = Path.Combine(AndroidExternalToolsSettings.jdkRootPath, "bin", IsWindows ? "keytool.exe" : "keytool");
            if (!File.Exists(keytool))
                return (true, "keytool 不存在，略過檢查");

            // -storepass:env reads the password from the variable, keeping it off the command line.
            var psi = new ProcessStartInfo(keytool,
                $"-J-Duser.language=en -list -keystore \"{keystorePath}\" -alias {KeyAlias} -storepass:env {KeystorePassEnv}");
            var (exitCode, output) = Run(psi);
            if (exitCode == 0) return (true, "");

            if (output.Contains("password was incorrect")) return (false, "密碼錯誤");
            if (output.Contains("does not exist")) return (false, $"keystore 裡沒有 alias「{KeyAlias}」");
            return (false, output.Trim());
        }

        private static bool Build(string path, BuildOptions options)
        {
            Directory.CreateDirectory(OutputDir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                options = options,
            });

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[QuestBuild] Build {summary.result}: {path} ({summary.totalErrors} errors)");
                return false;
            }

            Debug.Log($"[QuestBuild] {path} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss})");
            return true;
        }

        private static bool VerifyStoreSignature(string apkPath)
        {
            var buildToolsRoot = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "build-tools");
            var buildTools = Directory.Exists(buildToolsRoot)
                ? Directory.GetDirectories(buildToolsRoot).OrderBy(d => d).LastOrDefault()
                : null;
            if (buildTools == null)
            {
                Debug.LogWarning("[QuestBuild] 找不到 Android build-tools，略過簽章檢查。");
                return true;
            }

            var apksigner = Path.Combine(buildTools, IsWindows ? "apksigner.bat" : "apksigner");
            var apk = Path.GetFullPath(apkPath);

            // cmd.exe mangles a quoted .bat path followed by quoted args unless wrapped with /s /c "...".
            var psi = IsWindows
                ? new ProcessStartInfo("cmd.exe", $"/s /c \"\"{apksigner}\" verify --print-certs \"{apk}\"\"")
                : new ProcessStartInfo(apksigner, $"verify --print-certs \"{apk}\"");
            psi.EnvironmentVariables["JAVA_HOME"] = AndroidExternalToolsSettings.jdkRootPath;

            var (exitCode, output) = Run(psi);
            if (exitCode != 0)
            {
                Debug.LogError($"[QuestBuild] apksigner 執行失敗：\n{output}");
                return false;
            }

            if (!output.Contains(StoreCertSha256))
            {
                Debug.LogError($"[QuestBuild] 簽章不符，預期 SHA-256 {StoreCertSha256}\n{output}");
                return false;
            }

            Debug.Log($"[QuestBuild] 簽章確認：Store key（{KeyAlias}）。");
            return true;
        }

        private static (int exitCode, string output) Run(ProcessStartInfo psi)
        {
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using var process = Process.Start(psi);
            var stderr = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout + stderr.Result);
        }

        private static void SetVersionCode(int code)
        {
            PlayerSettings.Android.bundleVersionCode = code;
            AssetDatabase.SaveAssets();
        }

        private static string ApkPath(int versionCode, string suffix) =>
            $"{OutputDir}/{ApkPrefix}_{DateTime.Now:yyyyMMdd}_{versionCode}{suffix}.apk";
    }
}
