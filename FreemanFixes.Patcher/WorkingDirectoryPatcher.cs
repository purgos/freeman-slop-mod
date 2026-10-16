using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;

namespace FreemanFixes.Patcher
{
    // BepInEx preloader patcher: runs at Mono startup, before any game code (the plugin chainloader is too late).
    //
    // The launcher (StartFGW.exe) starts the game exe without setting a working directory. On Windows it inherits
    // the launcher's folder; under Proton/Wine it starts in the exe's own folder. The game reads ./Config.json and
    // ./Freeman Guerrilla Warfare/..._Data/StreamingAssets/<mod>/Table relative to the launcher folder, so from the
    // wrong folder it loads no tables and sits on a white screen.
    public static class WorkingDirectoryPatcher
    {
        public static IEnumerable<string> TargetDLLs { get { return new string[0]; } }

        public static void Initialize()
        {
            ManualLogSource log = Logger.CreateLogSource("FreemanFixes.Patcher");
            string cwd = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(cwd, "Config.json")))
            {
                log.LogInfo("Working directory OK: " + cwd);
                return;
            }
            DirectoryInfo parent = Directory.GetParent(Paths.GameRootPath);
            if (parent != null && File.Exists(Path.Combine(parent.FullName, "Config.json")))
            {
                Directory.SetCurrentDirectory(parent.FullName);
                log.LogInfo("Working directory " + cwd + " -> " + parent.FullName);
            }
            else
            {
                log.LogWarning("Config.json not found next to " + cwd + "; launch through Steam / StartFGW.exe");
            }
        }

        public static void Patch(AssemblyDefinition assembly)
        {
        }
    }
}
