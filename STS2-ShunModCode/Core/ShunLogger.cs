using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace STS2_ShunMod.Core;

/// <summary>
///     独立日志 — 写入 Mods/STS2-ShunMod/logs/ 目录，与游戏本体日志分离。
/// </summary>
public static class ShunLogger
{
    private static readonly object _lock = new();
    private static string? _logPath;

    /// <summary>
    ///     日志文件路径（懒初始化，延迟到 mod 目录可确定时）
    /// </summary>
    private static string LogPath
    {
        get
        {
            if (_logPath != null) return _logPath;

            // 从 DLL 位置推算 mod 根目录
            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                         ?? AppContext.BaseDirectory;

            var logsDir = Path.Combine(dllDir, "logs");
            Directory.CreateDirectory(logsDir);

            // 按日期分文件，避免单文件爆炸
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            _logPath = Path.Combine(logsDir, $"shunmod-{date}.log");
            return _logPath;
        }
    }

    public static void Info(string patch, string msg) => Write("INFO", patch, msg);
    public static void Warn(string patch, string msg) => Write("WARN", patch, msg);
    public static void Error(string patch, string msg) => Write("ERROR", patch, msg);
    public static void Error(string patch, Exception ex)
    {
        Write("ERROR", patch, $"{ex.GetType().Name}: {ex.Message}");
        // 堆栈单独一行，方便追踪调用链
        if (ex.StackTrace != null)
            Write("TRACE", patch, ex.StackTrace);
    }

    /// <summary>
    ///     记录状态快照，用于排查"为什么没触发"类的 bug。
    /// </summary>
    public static void Debug(string patch, string msg) => Write("DEBUG", patch, msg);

    private static void Write(string level, string patch, string msg)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] [{patch}] {msg}{Environment.NewLine}";

        lock (_lock)
        {
            try
            {
                File.AppendAllText(LogPath, line);
            }
            catch
            {
                // 日志写入失败不能炸游戏
            }
        }
    }

    /// <summary>
    ///     PatchAll 完成后写一条汇总，方便确认哪些 patch 已加载。
    /// </summary>
    public static void Summary(string modId)
    {
        Info(modId, "══════════ 日志已启动 ══════════");
    }
}