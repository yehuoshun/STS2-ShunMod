using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Relics;

/// <summary>
///     首领奖杯 — 击杀 Boss 后最大生命值 +25%。
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ShunModBossTrophy : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string PackedIconPath =>
        ShunImageHelper.RelicPackedIcon(IconBaseName);

    protected override string PackedIconOutlinePath =>
        ShunImageHelper.RelicOutlineIcon(IconBaseName);

    protected override string BigIconPath =>
        ShunImageHelper.RelicBigIcon(IconBaseName);

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