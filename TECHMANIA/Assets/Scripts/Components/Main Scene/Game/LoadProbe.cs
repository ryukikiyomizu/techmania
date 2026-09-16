using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

// Opt-in, file-only timing trace. Launch with -loadProbe and optionally
// -loadProbeSkipBga to compare the same chart without video preparation.
internal sealed class LoadProbe
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private long previousMs;
    private readonly string logPath;
    internal readonly bool skipBga;
    internal readonly bool sequentialBga;

    private LoadProbe(bool skipBga, bool sequentialBga)
    {
        this.skipBga = skipBga;
        this.sequentialBga = sequentialBga;
        string customPath = Environment.GetEnvironmentVariable("TECHMANIA_LOAD_PROBE_LOG");
        logPath = string.IsNullOrEmpty(customPath)
            ? Path.Combine(Application.persistentDataPath, "load-probe.tsv")
            : customPath;
        Write("start", "skipBga=" + skipBga + "\tsequentialBga=" + sequentialBga);
    }

    internal static LoadProbe Create()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool enabled = Array.Exists(args, a => a == "-loadProbe");
        if (!enabled) return null;
        return new LoadProbe(
            Array.Exists(args, a => a == "-loadProbeSkipBga"),
            Array.Exists(args, a => a == "-loadProbeSequentialBga"));
    }

    internal void Mark(string stage, string detail = "")
    {
        long now = clock.ElapsedMilliseconds;
        Write(stage, "deltaMs=" + (now - previousMs) + "\ttotalMs=" + now +
            (string.IsNullOrEmpty(detail) ? "" : "\t" + detail));
        previousMs = now;
    }

    internal void Event(string stage, string detail = "")
    {
        Write(stage, "totalMs=" + clock.ElapsedMilliseconds +
            (string.IsNullOrEmpty(detail) ? "" : "\t" + detail));
    }

    private void Write(string stage, string detail)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.AppendAllText(logPath, DateTime.UtcNow.ToString("o") + "\t" +
                stage + "\t" + detail.Replace('\n', ' ').Replace('\r', ' ') +
                Environment.NewLine);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("LoadProbe log failed: " + ex.Message);
        }
    }
}
