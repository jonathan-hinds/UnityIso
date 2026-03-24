using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class TacticsBuildUtility
{
    [MenuItem("Tools/Tactics/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        string projectRoot = Directory.GetCurrentDirectory();
        string outputDirectory = Path.Combine(projectRoot, "Builds", "Windows");
        string executablePath = Path.Combine(outputDirectory, "IsometricRPG.exe");

        Directory.CreateDirectory(outputDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = executablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception($"Windows build failed with result: {report.summary.result}");
        }

        Console.WriteLine($"Build succeeded: {executablePath}");
    }
}
