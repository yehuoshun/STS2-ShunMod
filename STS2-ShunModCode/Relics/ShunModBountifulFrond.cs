using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Relics;

/// <summary>
///     丰饶叶 — 每个回合开始时，用随机药水填满所有空药水栏位。
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ShunModBountifulFrond : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override string BigIconPath =>
        ShunImageHelper.RelicBigIcon(IconBaseName);

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