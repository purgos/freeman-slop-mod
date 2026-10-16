using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using Application = UnityEngine.Application;
using LogType = UnityEngine.LogType;

namespace FreemanFixes
{
    // BepInEx's LogOutput.log (and Unity's output_log.txt) are buffered and, under Proton, often only land on disk
    // when the game exits. This listener writes every log event to BepInEx/LiveLog.txt with AutoFlush, so it can be
    // tailed while the game runs. Unity's own log is taken straight from Application.logMessageReceived at Error /
    // Exception level only (the game floods Warning/Info: NavMesh, missing scripts), with the stack trace that
    // BepInEx's Unity log source drops.
    internal class LiveLog : ILogListener
    {
        private const string FileName = "LiveLog.txt";
        private const string StartedKey = "FreemanSlopMod.LiveLogStarted"; // survives ScriptEngine reloads

        private readonly StreamWriter writer;

        private LiveLog(StreamWriter writer)
        {
            this.writer = writer;
        }

        internal static LiveLog Start()
        {
            try
            {
                // Fresh file per game launch; append across hot reloads within the same launch.
                bool firstThisLaunch = AppDomain.CurrentDomain.GetData(StartedKey) == null;
                AppDomain.CurrentDomain.SetData(StartedKey, true);
                string path = Path.Combine(Paths.BepInExRootPath, FileName);
                var stream = new FileStream(path, firstThisLaunch ? FileMode.Create : FileMode.Append,
                    FileAccess.Write, FileShare.ReadWrite);
                var writer = new StreamWriter(stream);
                writer.AutoFlush = true;
                var live = new LiveLog(writer);
                Logger.Listeners.Add(live);
                Application.logMessageReceived += live.OnUnityLog;
                return live;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Live log unavailable: " + e.Message);
                return null;
            }
        }

        public void LogEvent(object sender, LogEventArgs e)
        {
            var source = sender as ILogSource;
            string sourceName = source != null ? source.SourceName : "?";
            if (sourceName == "Unity Log")
            {
                return; // written by OnUnityLog, with stack traces
            }
            try
            {
                writer.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " [" + e.Level + ":" + sourceName + "] " + e.Data);
            }
            catch (Exception)
            {
                // Never let logging break the game.
            }
        }

        private void OnUnityLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }
            try
            {
                writer.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " [" + type + ":Unity] " + message);
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    writer.WriteLine("    " + stackTrace.Trim().Replace("\n", "\n    "));
                }
            }
            catch (Exception)
            {
            }
        }

        public void Dispose()
        {
            Application.logMessageReceived -= OnUnityLog;
            Logger.Listeners.Remove(this);
            try
            {
                writer.Close();
            }
            catch (Exception)
            {
            }
        }
    }
}
