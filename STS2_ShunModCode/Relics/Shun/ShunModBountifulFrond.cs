using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Relics.Shun;

/// <summary>
///     丰饶叶 — 每个回合开始时，用随机药水填满所有空药水栏位。
/// </summary>
[RelicPool(typeof(SharedRelicPool))]
public sealed class ShunModBountifulFrond : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override string PackedIconPath => ShunModHelper.RelicIconPath(GetType());
    protected override string PackedIconOutlinePath => ShunModHelper.RelicOutlinePath(GetType());
    protected override string BigIconPath => ShunModHelper.RelicIconPath(GetType());

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        Flash();
        while (player.HasOpenPotionSlots)
        {
            var potion = PotionFactory
                .CreateRandomPotionOutOfCombat(player, player.RunState.Rng.CombatPotionGeneration)
                .ToMutable();
            if (!(await PotionCmd.TryToProcure(potion, player)).success)
                break;
        }
    }
}