using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Relics;

/// <summary>
///     首领奖杯 — Boss 遗物。
///     击杀 Boss 后，最大生命值 +25%。
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ShunModBossTrophy : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Boss;

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || room == null || room.RoomType != RoomType.Boss)
            return false;

        Flash();

        var gain = (int)(Owner!.Creature.MaxHp * 0.25m);
        if (gain > 0)
            TaskHelper.RunSafely(CreatureCmd.GainMaxHp(Owner.Creature, gain));

        // 不改奖励，只触发 HP 增长
        return false;
    }
}