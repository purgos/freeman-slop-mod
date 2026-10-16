using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;

namespace FreemanFixes
{
    // BepInEx's LogOutput.log (and Unity's output_log.txt) are buffered and, under Proton, often only land on disk
    // when the game exits. This listener writes every log event to BepInEx/LiveLog.txt with AutoFlush, so it can be
    // tailed while the game runs. Unity's own log is included at Error only (the game floods Warning/Info: NavMesh, missing scripts).
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
            if (sourceName == "Unity Log" && (e.Level & (LogLevel.Error | LogLevel.Fatal)) == 0)
            {
                return;
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

        public void Dispose()
        {
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
