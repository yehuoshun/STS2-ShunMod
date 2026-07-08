using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using ShunMod.Core;

namespace ShunMod.Shun.Relics;

/// <summary>
///     首领奖杯 — 击杀 Boss 后最大生命值 +25%。
/// </summary>
[RelicPool(typeof(SharedRelicPool))]
public sealed class ShunModBossTrophy : ShunRelicModel<ShunModBossTrophy>
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || room == null || room.RoomType != RoomType.Boss)
            return false;

        Flash();

        var gain = (int)(Owner!.Creature.MaxHp * 0.25m);
        if (gain > 0)
            TaskHelper.RunSafely(CreatureCmd.GainMaxHp(Owner.Creature, gain));

        return false;
    }
}