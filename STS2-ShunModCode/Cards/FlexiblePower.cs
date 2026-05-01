using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Abilities;

namespace STS2_ShunMod.Cards;

/// <summary>
/// 空白能力卡 — 初始无效果，通过事件叠加能力。
/// 打出时读取本地存档的能力层数，逐一执行。
/// </summary>
public class FlexiblePower : ShunCard
{
    public FlexiblePower()
        : base(baseCost: 2, type: CardType.Power, rarity: CardRarity.Rare, target: TargetType.Self)
    {
        WithKeywords(CardKeyword.Innate);
        WithTip(CardKeyword.Innate);
        WithCostUpgradeBy(-1);
    }

    public override string PortraitPath => "res://cards/apotheosis.png";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var config = AbilityConfig.Load();
        var target = choiceContext.CreatureTargets.FirstOrDefault();

        foreach (var (key, def) in config)
        {
            int stacks = AbilityStore.Get(key);
            if (stacks <= 0) continue;

            int amount = stacks * def.per_stack;

            switch (key)
            {
                case "damage":
                    if (target != null)
                        await DamageCmd.Attack(amount).FromCard(this).Targeting(target).Execute();
                    break;
                case "aoe":
                    await DamageCmd.Attack(amount).FromCard(this)
                        .TargetingAllOpponents(null!).Execute();
                    break;
                case "block":
                    await CardCmd.Block(Owner!, amount);
                    break;
                case "draw":
                    await CardPileCmd.Draw(Owner!, amount);
                    break;
                case "heal":
                    await PlayerCmd.Heal(Owner!, amount);
                    break;
            }
        }
    }
}
