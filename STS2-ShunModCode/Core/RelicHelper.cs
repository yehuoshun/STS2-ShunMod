using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Core;

/// <summary>
///     遗物操作工具类 — 通过反射移除玩家遗物。
/// </summary>
/// <remarks>
///     使用反射访问 Player 的私有字段 _relics 并触发移除事件。
///     TODO: 待游戏提供公共 API 后替换反射实现。
/// </remarks>
public static class RelicHelper
{
    /// <summary>
    ///     Player._relics 私有字段反射缓存。
    /// </summary>
    private static readonly FieldInfo? RelicsField =
        typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Player.RelicRemoved 事件反射缓存。优先尝试 EventInfo，
    ///     回退到 field-like event 的 backing field。
    /// </summary>
    private static readonly EventInfo? RelicRemovedEventInfo =
        typeof(Player).GetEvent("RelicRemoved", BindingFlags.Public | BindingFlags.Instance);

    private static readonly FieldInfo? RelicRemovedBackingField =
        typeof(Player).GetField("RelicRemoved", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    ///     从玩家身上移除指定遗物并触发 RelicRemoved 事件。
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

        // 触发 RelicRemoved 事件
        // 优先通过 EventInfo 获取 raise 方法，回退到 backing field 手动调用
        var raiseMethod = RelicRemovedEventInfo?.GetRaiseMethod(true);
        if (raiseMethod != null)
            raiseMethod.Invoke(player, [relic]);
        else if (RelicRemovedBackingField?.GetValue(player) is Delegate del)
            foreach (var handler in del.GetInvocationList())
                try
                {
                    handler.DynamicInvoke(relic);
                }
                catch (TargetParameterCountException)
                {
                    // 事件签名不匹配，尝试空参数调用
                    try
                    {
                        handler.DynamicInvoke();
                    }
                    catch
                    {
                        /* 静默跳过 */
                    }
                }

        return true;
    }
}