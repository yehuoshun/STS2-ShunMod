using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Cards.Shun;

/// <summary>
///     添柴·改 — 消耗任意手牌，每消耗一张将 1 张随机牌加入手牌。
///     升级后生成的牌自动升级。
///     与原版 Stoke 的区别：原版消耗所有手牌，本卡让玩家自由选择。
/// </summary>
[CardPool(typeof(ColorlessCardPool))]
public class ShunModStokeModified : CardModel
{
    public ShunModStokeModified()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    public override string PortraitPath => ShunCard.PortraitPath<ShunModStokeModified>();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner;
        await CreatureCmd.TriggerAnim(owner.Creature, "Cast", owner.Character.CastAnimDelay);

        // 玩家自由选择要消耗的手牌（最少 0，最多不限）
        var prefs = new CardSelectorPrefs(
            CardSelectorPrefs.ExhaustSelectionPrompt, 0, int.MaxValue)
        {
            Cancelable = true
        };
        var selected = (await CardSelectCmd.FromHand(
            choiceContext, owner, prefs, null, this)).ToList();

        var exhaustCount = selected.Count;
        if (exhaustCount == 0) return;

        // 消耗选中的牌
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        // 生成等量的随机牌（全卡池，不限定角色）
        var cards = CardFactory.GetForCombat(
            owner, ModelDb.AllCards, exhaustCount,
            owner.RunState.Rng.CombatCardGeneration).ToList();

        // 升级后：生成的牌自动升级
        if (IsUpgraded)
            CardCmd.Upgrade(cards, CardPreviewStyle.None);

        await CardPileCmd.Add(
            cards, PileType.Hand);
    }
}