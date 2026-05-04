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
/// 超级神化 — 升级战斗中所有卡牌，同时升级牌组中所有可升级卡牌。
/// </summary>
/// <remarks>
/// 卡牌属性：
/// <list type="bullet">
/// <item>费用：2（升级后 1）</item>
/// <item>类型：技能 / 自身目标</item>
/// <item>稀有度：稀有 / 无色</item>
/// <item>关键词：消耗</item>
/// </list>
/// </remarks>
[Pool(typeof(ColorlessCardPool))]
public class SuperApotheosis : ShunCard
{
    /// <summary>
    /// 配置卡牌基础属性与关键词。
    /// </summary>
    public SuperApotheosis()
        : base(baseCost: 2, type: CardType.Skill, rarity: CardRarity.Rare, target: TargetType.Self)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithTip(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    /// <summary>
    /// 卡牌肖像资源路径（Godot res:// 协议）。
    /// </summary>
    public override string PortraitPath => "res://STS2_ShunMod/cards/apotheosis.png";

    /// <summary>
    /// 打出效果：升级当前战斗中所有卡牌 + 牌组中所有可升级卡牌。
    /// </summary>
    /// <param name="choiceContext">玩家选择上下文</param>
    /// <param name="cardPlay">卡牌打出信息</param>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 升级战斗中所有卡牌（排除自身）
        foreach (CardModel allCard in base.Owner!.PlayerCombatState.AllCards)
        {
            if (allCard != this && allCard.IsUpgradable)
            {
                CardCmd.Upgrade(allCard);
            }
        }

        // 升级牌组中所有可升级卡牌（无预览动画）
        var deckCards = PileType.Deck.GetPile(Owner!).Cards
            .Where(c => c.IsUpgradable)
            .ToList();
        foreach (var card in deckCards)
            CardCmd.Upgrade(card, CardPreviewStyle.None);
    }
}
