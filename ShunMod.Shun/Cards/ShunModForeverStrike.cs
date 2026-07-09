using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using ShunMod.Core;

namespace ShunMod.Shun.Cards;

/// <summary>
///     永远打击 — 造成 6 点伤害。
///     拥有【永远】词条：打出后自动回到手牌，回合结束时也会回到手牌。
///     0 费，无色，普通。
/// </summary>
[CardPool(typeof(ColorlessCardPool))]
public class ShunModForeverStrike : CardModel
{
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;

    public ShunModForeverStrike()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    public override string PortraitPath => ShunCard.PortraitPath<ShunModForeverStrike>();

    public override IEnumerable<CardTag> Tags => new CardTag[] { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move)
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner;
        if (cardPlay.Target == null) return;

        await CreatureCmd.TriggerAnim(owner.Creature, "Cast", owner.Character.CastAnimDelay);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.OnUpgrade();
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}