using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Events;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 将 ShunModRelicExchange 事件注册到游戏事件列表。
/// </summary>
[HarmonyPatch]
public static class EventRegistration_RelicExchange
{
    static IEnumerable<EventModel> Postfix(IEnumerable<EventModel> events)
    {
        foreach (var e in events)
            yield return e;

        var relicExchange = ModelDb.GetByIdOrNull<EventModel>(
            ModelDb.GetId(typeof(ShunModRelicExchange)));
        if (relicExchange != null)
            yield return relicExchange;
    }
}
