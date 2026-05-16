using System.Reflection;
using HarmonyLib;

namespace STS2_ShunMod.Core;

/// <summary>
///     Creature 反射工具 — 访问 Creature 类型内部属性（Block / IsPlayer 等）。
///     因为 Publicizer 可能未启用，用反射比直接属性访问更稳健。
///     参考 STS2Plus GameReflection 精简。
/// </summary>
internal static class CreatureReflection
{
    public static readonly Type? CreatureType =
        AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Creatures.Creature");

    public static readonly PropertyInfo? BlockProperty =
        AccessTools.Property(CreatureType, "Block");

    public static readonly PropertyInfo? IsPlayerProperty =
        AccessTools.Property(CreatureType, "IsPlayer");

    /// <summary>
    ///     获取 Creature.Block（int），反射失败返回 0。
    /// </summary>
    public static int GetBlock(object creature)
    {
        return BlockProperty?.GetValue(creature) as int? ?? 0;
    }

    /// <summary>
    ///     设置 Creature.Block。
    /// </summary>
    public static void SetBlock(object creature, int value)
    {
        BlockProperty?.SetValue(creature, value);
    }

    /// <summary>
    ///     判断是否为玩家生物（Creature.IsPlayer）。
    /// </summary>
    public static bool IsPlayer(object? creature)
    {
        return creature != null && IsPlayerProperty?.GetValue(creature) is true;
    }
}