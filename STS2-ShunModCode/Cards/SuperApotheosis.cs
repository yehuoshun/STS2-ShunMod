using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Utils;

namespace STS2_ShunMod.Cards;

/// <summary>
/// 超级神化 (Super Apotheosis) — 升级手牌 + 牌组中所有卡牌。
/// </summary>
public class SuperApotheosis : ShunCard
{
    public SuperApotheosis()
        : base(baseCost: 2, type: CardType.Skill, rarity: CardRarity.Rare, target: TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithTip(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    public override string PortraitPath => "res://cards/apotheosis.png";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 升级手牌
        var handCards = PileType.Hand.GetPile(Owner).Cards
            .Where(c => c is not null && c.IsUpgradable)
            .ToList();
        foreach (var card in handCards)
            CardCmd.Upgrade(card);

        // 升级牌组
        var deckCards = PileType.Deck.GetPile(Owner).Cards
            .Where(c => c is not null && c.IsUpgradable)
            .ToList();
        foreach (var card in deckCards)
            CardCmd.Upgrade(card, CardPreviewStyle.None);
    }
}
