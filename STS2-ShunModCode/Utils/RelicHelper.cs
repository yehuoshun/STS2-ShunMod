using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Utils;

/// <summary>
/// 遗物操作工具 — 反射移除遗物（等找到公共 API 再换）。
/// </summary>
public static class RelicHelper
{
    private static readonly FieldInfo? RelicsField =
        typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? RelicRemovedEvent =
        typeof(Player).GetField("RelicRemoved", BindingFlags.Public | BindingFlags.Instance);

    public static bool RemoveRelic(Player player, RelicModel relic)
    {
        if (RelicsField?.GetValue(player) is not List<RelicModel> list)
            return false;

        if (!list.Remove(relic))
            return false;

        // 触发 RelicRemoved 事件
        if (RelicRemovedEvent?.GetValue(player) is Delegate del)
        {
            foreach (var handler in del.GetInvocationList())
                handler.Method.Invoke(handler.Target, [relic]);
        }

        return true;
    }
}
