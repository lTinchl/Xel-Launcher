using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace XelLauncher.Helpers;

internal static class GamePlaytime
{
    private static readonly object Sync = new();
    private static readonly string FilePath = Path.Combine(ConfigHelper.ConfigDir, "playtime.json");
    private static Dictionary<string, Dictionary<string, double>> Data = Load();
    private static readonly Dictionary<string, DateTime> Last = new();
    private static Dictionary<string, Dictionary<string, double>> Load()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }
    }
    private static readonly Dictionary<int, System.Threading.Timer> Monitors = new();
    public static void Track(System.Diagnostics.Process process)
    {
        lock (Sync)
        {
            if (Monitors.ContainsKey(process.Id)) return;
            var handle = System.Diagnostics.Process.GetProcessById(process.Id);
            var id = process.Id;
            Observe("Arknights", true);
            Monitors[id] = new System.Threading.Timer(_ =>
            {
                lock (Sync)
                {
                    bool running;
                    try { running = !handle.HasExited; } catch { running = false; }
                    Observe("Arknights", running);
                    if (!running && Monitors.Remove(id, out var timer)) { timer.Dispose(); handle.Dispose(); }
                }
            }, null, 5000, 5000);
        }
    }    public static void Observe(string game, bool running)
    {
        lock (Sync)
        {
            var now = DateTime.Now;
            if (Last.TryGetValue(game, out var previous))
            {
                // Do not count sleep, suspended monitoring, or launcher downtime.
                if ((now - previous).TotalSeconds is > 0 and < 10)
                {
                    if (!Data.TryGetValue(game, out var days)) Data[game] = days = new();
                    while (previous < now)
                    {
                        var end = previous.Date.AddDays(1) < now ? previous.Date.AddDays(1) : now;
                        var key = previous.ToString("yyyy-MM-dd");
                        days[key] = days.GetValueOrDefault(key) + (end - previous).TotalSeconds;
                        previous = end;
                    }
                    try
                    {
                        Directory.CreateDirectory(ConfigHelper.ConfigDir);
                        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(Data));
                        File.Move(FilePath + ".tmp", FilePath, true);
                    }
                    catch (Exception ex) { LogHelper.LogError(ex, "Save game playtime"); }
                }
            }
            if (running) Last[game] = now; else Last.Remove(game);
        }
    }
    public static string GetText(string game)
    {
        lock (Sync)
        {
            var monday = DateTime.Today.AddDays(-((int)DateTime.Today.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd");
            var days = Data.GetValueOrDefault(game) ?? new();
            var week = days.Where(x => string.CompareOrdinal(x.Key, monday) >= 0).Sum(x => x.Value);
            return $"周时长：{week / 3600:0.0} 小时    游戏时长：{days.Values.Sum() / 3600:0.0} 小时";
        }
    }
}
