using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2_ShunMod.Events;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 将遗物交易所事件注入到游戏事件列表。
/// 修改 Overgrowth.AllEvents（第一章地图），
/// 如需出现在其他章节，同样 patch City / Beyond。
/// </summary>
[HarmonyPatch(typeof(Overgrowth), "AllEvents", MethodType.Getter)]
public static class EventRegistration_RelicExchange
{
    static void Postfix(ref IReadOnlyList<EventModel> __result)
    {
        var list = __result.ToList();
        var relicExchange = ModelDb.GetByIdOrNull<EventModel>(
            ModelDb.GetId(typeof(ShunModRelicExchange)));
        if (relicExchange != null)
            list.Add(relicExchange);
        __result = list;
    }
}
