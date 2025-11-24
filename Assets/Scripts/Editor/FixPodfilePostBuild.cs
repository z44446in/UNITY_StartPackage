using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;

public class FixPodfilePostBuild
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        string podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
        if (!File.Exists(podfilePath))
        {
            UnityEngine.Debug.LogWarning("[Podfile Fixer] Podfile not found.");
            return;
        }

        string podfileText = File.ReadAllText(podfilePath);

        // ✅ 이미 post_install 블록이 없을 때만 추가
        if (!podfileText.Contains("post_install do |installer|"))
        {
            string fixBlock = @"

# ✅ Auto-added by Unity PostProcessBuild to fix iOS deployment target
post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'
    end
  end
end
";
            File.AppendAllText(podfilePath, fixBlock);
            UnityEngine.Debug.Log("[Podfile Fixer] Added deployment target fix to Podfile ✅");
        }
        else
        {
            UnityEngine.Debug.Log("[Podfile Fixer] post_install block already exists. Skipped.");
        }
    }
}
