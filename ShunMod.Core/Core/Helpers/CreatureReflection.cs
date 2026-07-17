using System.Reflection;
using HarmonyLib;

namespace ShunMod.Core.Core.Helpers;

/// <summary>
///     Creature 反射工具 — 通过反射访问 Creature 内部属性（Block / IsPlayer）。
/// </summary>
public static class CreatureReflection
{
    public static readonly Type? CreatureType =
        AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Creatures.Creature");

    private static readonly PropertyInfo? BlockProperty =
        AccessTools.Property(CreatureType, "Block");

    private static readonly PropertyInfo? IsPlayerProperty =
        AccessTools.Property(CreatureType, "IsPlayer");

    public static int GetBlock(object creature)
    {
        return BlockProperty?.GetValue(creature) as int? ?? 0;
    }

    public static void SetBlock(object creature, int value)
    {
        BlockProperty?.SetValue(creature, value);
    }

    public static bool IsPlayer(object? creature)
    {
        return creature != null && IsPlayerProperty?.GetValue(creature) is true;
    }
}