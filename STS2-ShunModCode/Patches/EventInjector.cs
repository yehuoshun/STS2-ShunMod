using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Patches;

/// <summary>
///     将 ContentRegistry.CustomEvents 注入 ModelDb.AllSharedEvents。
///     Harmony PatchAll() 自动应用。
/// </summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.AllSharedEvents), MethodType.Getter)]
public static class EventInjector
{
    private static IEnumerable<EventModel> Postfix(IEnumerable<EventModel> __result)
    {
        return [.. __result, .. ContentRegistry.CustomEvents];
    }
}
