using System.Reflection;
using HarmonyLib;

namespace ShunMod.Core;

/// <summary>
///     Creature 反射工具 — 通过反射访问 Creature 类型内部属性（Block / IsPlayer）。
///     格挡保留、未来可能有的战斗补丁均通过此类操作 Creature 属性，
///     避免直接引用游戏内部 API 导致硬依赖。
/// </summary>
internal static class CreatureReflection
{
    public static readonly Type? CreatureType =
        AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Creatures.Creature");

    public static readonly PropertyInfo? BlockProperty =
        AccessTools.Property(CreatureType, "Block");

    public static readonly PropertyInfo? IsPlayerProperty =
        AccessTools.Property(CreatureType, "IsPlayer");

    public static int GetBlock(object creature) => BlockProperty?.GetValue(creature) as int? ?? 0;

    public static void SetBlock(object creature, int value) => BlockProperty?.SetValue(creature, value);

    public static bool IsPlayer(object? creature) =>
        creature != null && IsPlayerProperty?.GetValue(creature) is true;
}
