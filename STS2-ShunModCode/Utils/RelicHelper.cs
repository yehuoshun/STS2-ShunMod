using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Utils;

/// <summary>
/// 遗物操作工具类 — 通过反射移除玩家遗物。
/// </summary>
/// <remarks>
/// 使用反射访问 Player 的私有字段 _relics 和公共事件 RelicRemoved。
/// TODO: 待游戏提供公共 API 后替换反射实现。
/// </remarks>
public static class RelicHelper
{
    /// <summary>
    /// Player._relics 私有字段反射缓存。
    /// </summary>
    private static readonly FieldInfo? RelicsField =
        typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Player.RelicRemoved 公共事件反射缓存。
    /// </summary>
    private static readonly FieldInfo? RelicRemovedEvent =
        typeof(Player).GetField("RelicRemoved", BindingFlags.Public | BindingFlags.Instance);

    /// <summary>
    /// 从玩家身上移除指定遗物并触发 RelicRemoved 事件。
    /// </summary>
    /// <param name="player">目标玩家</param>
    /// <param name="relic">要移除的遗物</param>
    /// <returns>成功移除返回 true；遗物不存在或反射失败返回 false</returns>
    public static bool RemoveRelic(Player player, RelicModel relic)
    {
        // 反射获取遗物列表
        if (RelicsField?.GetValue(player) is not List<RelicModel> list)
            return false;

        if (!list.Remove(relic))
            return false;

        // 触发 RelicRemoved 事件，通知游戏遗物已移除
        if (RelicRemovedEvent?.GetValue(player) is Delegate del)
        {
            foreach (var handler in del.GetInvocationList())
                handler.Method.Invoke(handler.Target, [relic]);
        }

        return true;
    }
}
