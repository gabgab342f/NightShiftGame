using System;
using UnityEditor;
using UnityEngine;

// Unity 2020.3 batchmode intermittently answers
// "executeMethod class 'SchoolBuilder' could not be found" even though the
// type is compiled and loaded, so headless runs drive the builder through an
// [InitializeOnLoad] hook instead. It stays completely inert unless the
// environment variable NS_AUTOBUILD is set, so opening the project normally
// never triggers a build.
//
//   $env:NS_AUTOBUILD = "1"; $env:NS_STAGE = "build"   -> SchoolBuilder.BuildAll
//   $env:NS_AUTOBUILD = "1"; $env:NS_STAGE = "tmp"     -> SchoolBuilder.ImportTmpEssentials
[InitializeOnLoad]
public static class NightShiftAutoBuild
{
    const string Flag = "NS_AUTOBUILD";

    static int ticks;

    static NightShiftAutoBuild()
    {
        if (Environment.GetEnvironmentVariable(Flag) != "1") return;
        EditorApplication.update += WaitForEditor;
    }

    // Let the initial asset refresh and any script compilation finish first.
    // Note: this needs a batchmode run WITHOUT -quit, otherwise the editor
    // quits before the update loop ever ticks.
    static void WaitForEditor()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || ++ticks < 2)
            return;
        EditorApplication.update -= WaitForEditor;
        Run();
    }

    static void Run()
    {
        string stage = Environment.GetEnvironmentVariable("NS_STAGE");
        try
        {
            if (stage == "tmp") SchoolBuilder.ImportTmpEssentials();
            else if (stage == "validate") SchoolBuilder.Validate();
            else SchoolBuilder.BuildAll();
        }
        catch (Exception e)
        {
            Debug.LogError("[NightShiftAutoBuild] FAILED: " + e);
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("[NightShiftAutoBuild] stage '" + (stage ?? "build") + "' finished.");
        EditorApplication.Exit(0);
    }
}
