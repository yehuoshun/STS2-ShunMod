using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2_ShunMod.Core.Registration;
using STS2_ShunMod.Utils;

namespace STS2_ShunMod.Cards;

/// <summary>
/// 超级神化 (Super Apotheosis) — 升级所有局内卡牌 + 牌组中所有卡牌。
/// </summary>
[Pool(typeof(ColorlessCardPool))]
public class SuperApotheosis : ShunCard
{
    public SuperApotheosis()
        : base(baseCost: 2, type: CardType.Skill, rarity: CardRarity.Rare, target: TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithTip(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    public override string PortraitPath => "res://STS2_ShunMod/cards/apotheosis.png";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (CardModel allCard in base.Owner!.PlayerCombatState.AllCards)
        {
            if (allCard != this && allCard.IsUpgradable)
            {
                CardCmd.Upgrade(allCard);
            }
        }

        // 升级牌组
        var deckCards = PileType.Deck.GetPile(Owner!).Cards
            .Where(c => c.IsUpgradable)
            .ToList();
        foreach (var card in deckCards)
            CardCmd.Upgrade(card, CardPreviewStyle.None);
    }
}
