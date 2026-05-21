using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace STS2_ShunMod.Core;

/// <summary>
///     日志级别。
///     Minimal = 只输出 ERROR
///     Normal  = ERROR + WARN + INFO（默认）
///     Verbose = 全部（含 DEBUG + TRACE）
/// </summary>
public enum LogLevel
{
    Minimal,
    Normal,
    Verbose
}

/// <summary>
///     独立日志 — 写入 Mods/STS2-ShunMod/logs/ 目录，与游戏本体日志分离。
///     支持 <c>debug-config.json</c> 控制日志级别，修改后重启游戏生效。
/// </summary>
public static class ShunLogger
{
    private static readonly object _lock = new();
    private static string? _logPath;
    private static string? _configPath;
    private static LogLevel _level = LogLevel.Normal;

    /// <summary>
    ///     日志文件路径（懒初始化，延迟到 mod 目录可确定时）
    /// </summary>
    private static string LogPath
    {
        get
        {
            if (_logPath != null) return _logPath;

            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                         ?? AppContext.BaseDirectory;

            // 配置文件路径
            _configPath = Path.Combine(dllDir, "debug-config.json");
            LoadConfig();

            var logsDir = Path.Combine(dllDir, "logs");
            Directory.CreateDirectory(logsDir);

            var date = DateTime.Now.ToString("yyyy-MM-dd");
            _logPath = Path.Combine(logsDir, $"shunmod-{date}.log");
            return _logPath;
        }
    }

    /// <summary>
    ///     当前日志级别（可通过 debug-config.json 配置）
    /// </summary>
    public static LogLevel CurrentLevel => _level;

    private static void LoadConfig()
    {
        try
        {
            if (_configPath == null || !File.Exists(_configPath)) return;

            var json = File.ReadAllText(_configPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("logLevel", out var el) && el.ValueKind == JsonValueKind.String)
            {
                if (Enum.TryParse<LogLevel>(el.GetString(), ignoreCase: true, out var parsed))
                    _level = parsed;
            }
        }
        catch
        {
            // 配置解析失败用默认值，不炸游戏
        }
    }

    /// <summary>
    ///     运行时重新读取配置（无需重启游戏）
    /// </summary>
    public static void ReloadConfig()
    {
        LoadConfig();
        Info("ShunLogger", $"日志级别切换至 {_level}");
    }

    public static void Info(string patch, string msg)
    {
        if (_level >= LogLevel.Normal) Write("INFO", patch, msg);
    }

    public static void Warn(string patch, string msg)
    {
        if (_level >= LogLevel.Normal) Write("WARN", patch, msg);
    }

    public static void Error(string patch, string msg)
    {
        // ERROR 永远输出
        Write("ERROR", patch, msg);
    }

    public static void Error(string patch, Exception ex)
    {
        Write("ERROR", patch, $"{ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace != null && _level >= LogLevel.Verbose)
            Write("TRACE", patch, ex.StackTrace);
    }

    public static void Debug(string patch, string msg)
    {
        if (_level >= LogLevel.Verbose) Write("DEBUG", patch, msg);
    }

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
        Info(modId, $"══════════ 日志已启动 (级别: {_level}) ══════════");
    }
}