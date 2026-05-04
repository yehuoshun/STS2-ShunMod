using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2_ShunMod.Core.Registration;
using STS2_ShunMod.Patches;

namespace STS2_ShunMod;

/// <summary>
/// Mod 入口 — 负责 Harmony 补丁注入与内容自动注册。
/// </summary>
/// <remarks>
/// 初始化流程：
/// <list type="number">
/// <item>Harmony.PatchAll() 扫描并应用所有 [HarmonyPatch] 标注的补丁类</item>
/// <item>ContentRegistry.RegisterAll() 扫描所有 [Pool] 标注的卡牌/遗物等并自动注册到游戏卡池</item>
/// </list>
/// </remarks>
[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    /// <summary>
    /// Mod 唯一标识符，与模组清单中 id 字段一致。
    /// </summary>
    public const string ModId = "STS2-ShunMod";

    /// <summary>
    /// Harmony 补丁实例，用于 IL 运行时注入。
    /// </summary>
    private static readonly Harmony _harmony = new(ModId);

    /// <summary>
    /// Mod 初始化入口，由游戏通过 [ModInitializer] 反射调用。
    /// </summary>
    /// <exception cref="Exception">Harmony 补丁或内容注册失败时捕获并记录日志</exception>
    public static void Initialize()
    {
        try
        {
            _harmony.PatchAll();
            ContentRegistry.RegisterAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            Log.Error(ModId + " - 加载失败");
            Log.Error(e.Message);
            return;
        }
        Log.Info(ModId + " - 加载成功!");
    }
}
