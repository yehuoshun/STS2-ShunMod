using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Cards;

/// <summary>
/// 超级神化 (Super Apotheosis) — 升级手牌和牌组中的所有卡牌。
/// 神化的强化版：不仅升级手牌，还额外升级牌组中全部卡牌。
/// </summary>
public class SuperApotheosis : CardModel
{
    public SuperApotheosis()
        : base(baseCost: 2, type: CardType.Skill, rarity: CardRarity.Rare, target: TargetType.Self)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 升级手牌中所有可升级的卡牌
        var handCards = PileType.Hand.GetPile(Owner).Cards
            .Where(c => c is not null && c.IsUpgradable)
            .ToList();
        foreach (var card in handCards)
            CardCmd.Upgrade(card);

        // 额外：升级牌组中所有可升级的卡牌
        var deckCards = PileType.Deck.GetPile(Owner).Cards
            .Where(c => c is not null && c.IsUpgradable)
            .ToList();
        foreach (var card in deckCards)
            CardCmd.Upgrade(card, CardPreviewStyle.None);
    }
}
