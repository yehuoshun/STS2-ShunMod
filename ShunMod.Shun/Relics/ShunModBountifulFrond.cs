using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using ShunMod.Core.Core.Registry;
using ShunMod.Shun.Base;

// ReSharper disable UnusedType.Global — 游戏框架通过反射/泛型基类实例化
namespace ShunMod.Shun.Relics;

/// <summary>
///     丰饶叶 — 每个回合开始时，用随机药水填满所有空药水栏位。
/// </summary>
[RelicPool(typeof(SharedRelicPool))]
public sealed class ShunModBountifulFrond : ShunRelicModel<ShunModBountifulFrond>
{
    private const int MaxPotionFillAttempts = 20;
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        Flash();
        var attempts = 0;
        while (player.HasOpenPotionSlots && attempts++ < MaxPotionFillAttempts)
        {
            var potion = PotionFactory
                .CreateRandomPotionOutOfCombat(player, player.RunState.Rng.CombatPotionGeneration)
                .ToMutable();
            if (!(await PotionCmd.TryToProcure(potion, player)).success)
                break;
        }
    }
}