using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     劫持 EventModel.CreateInitialPortrait，将默认图片路径替换为 mod 资源路径。
///     其他 mod 的补丁（如 CreateInitialPortrait_Patch1）会先于我们的 mod 加载，
///     因此用 HarmonyPriority 确保本补丁在最前执行。
/// </summary>
[HarmonyPatch(typeof(EventModel), "CreateInitialPortrait")]
[HarmonyPriority(Priority.First)]
public static class EventPortraitRedirectPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EventModel __instance, ref Texture2D? __result)
    {
        var modId = __instance.Id.Entry;
        var modPath = ShunImageHelper.EventImage(modId.ToLowerInvariant());

        if (!ResourceLoader.Exists(modPath))
            return true; // 不是我们的 mod 事件，走原逻辑

        __result = ResourceLoader.Load<Texture2D>(modPath);
        ShunLogger.Info("事件立绘", $"劫持 {modId} → {modPath}");
        return false; // 跳过原方法（以及其他 mod 的补丁）
    }
}