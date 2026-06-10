using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace STS2ShunMod.Cards;

/// <summary>
///     超级神化 — 升级战斗中所有卡牌，同时升级牌组中所有可升级卡牌。
///     2费→1费（升级后），技能，稀有，无色。
/// </summary>
public class ShunModSuperApotheosis : CardModel
{
    private const string Portrait = "res://STS2-ShunMod/images/cards/shunCards/colorless/superapotheosis.png";

    public ShunModSuperApotheosis() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public override string PortraitPath => Portrait;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner;
        if (owner.PlayerCombatState == null)
            return;

        // 升级战斗中所有卡牌（排除自身）
        foreach (var allCard in owner.PlayerCombatState.AllCards)
            if (allCard != this && allCard.IsUpgradable)
                CardCmd.Upgrade(allCard);

        // 升级牌组中所有可升级卡牌（无预览动画）
        var deckCards = PileType.Deck.GetPile(owner).Cards
            .Where(c => c.IsUpgradable)
            .ToList();
        foreach (var card in deckCards)
            CardCmd.Upgrade(card, CardPreviewStyle.None);
    }

    protected override void OnUpgrade()
    {
        base.OnUpgrade();
        EnergyCost.UpgradeBy(-1);
    }
}