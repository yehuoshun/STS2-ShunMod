using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Powers;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Relics.Shun;

/// <summary>
///     无限壶铃 — 在休息处获得力量，无使用次数限制。
/// </summary>
[RelicPool(typeof(SharedRelicPool))]
public sealed class ShunModInfiniteGirya : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string PackedIconPath => ShunModHelper.RelicIconPath(GetType());
    protected override string PackedIconOutlinePath => ShunModHelper.RelicOutlinePath(GetType());
    protected override string BigIconPath => ShunModHelper.RelicIconPath(GetType());

    public override void OnPlayerRest(Player player)
    {
        if (player != Owner) return;

        Flash();
        TaskHelper.RunSafely(CreatureCmd.ApplyPower(Owner!.Creature, new StrengthPower(1)));
    }
}
