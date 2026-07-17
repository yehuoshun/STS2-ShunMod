using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using ShunMod.Core.Core.Registry;
using ShunMod.Shun.Base;

namespace ShunMod.Shun.Relics;

// ReSharper disable UnusedType.Global — 游戏框架反射使用
// ReSharper disable UnusedType.Instantiation — 游戏框架反射实例化
/// <summary>
///     生生不息 — 右键点击遗物触发。
///     消耗任意数量的手牌，每消耗一张生成随机已升级的卡牌（不分角色）加入手卡，
///     并且首次打出免费，获得消耗数量的能量。
/// </summary>
[RelicPool(typeof(SharedRelicPool))]
public sealed class ShunModEndlessLife : ShunRelicModel<ShunModEndlessLife>
{
    private static readonly LocString SelectionPrompt = new("card_selection", "TO_EXHAUST");

    public override RelicRarity Rarity => RelicRarity.Rare;

    /// <summary>
    ///     执行右键点击动作：选牌 → 消耗 → 生成升级卡 → 加手 → 首次免费 → 回能。
    /// </summary>
    public async Task ExecuteRightClick(PlayerChoiceContext choiceContext)
    {
        // 从手牌选任意张（0 到手牌上限）
        var hand = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (hand.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionPrompt, 0, hand.Count)
        {
            Cancelable = true
        };

        // 立即视觉反馈
        Flash();

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            prefs,
            _ => true,
            this)).ToList();

        if (selected.Count == 0)
            return;

        var count = selected.Count;

        // 获取所有可生成的卡牌池
        var allCards = ModelDb.AllCards
            .Where(c => c.CanBeGeneratedInCombat
                        && c.Rarity != CardRarity.Basic
                        && c.Rarity != CardRarity.Ancient
                        && c.Rarity != CardRarity.Event
                        && c.Rarity != CardRarity.Token
                        && c.Rarity != CardRarity.Status
                        && c.Rarity != CardRarity.Curse
                        && c.Rarity != CardRarity.Quest)
            .Distinct()
            .ToList();

        if (allCards.Count == 0)
            return;

        // 先逐张消耗（消耗动画快，不影响体验）
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        // 批量生成卡牌，一步到位（单次动画）
        var newCards = CardFactory.GetForCombat(
            Owner, allCards, count,
            Owner.RunState.Rng.CombatCardGeneration).ToList();

        // 批量升级（无动画）
        CardCmd.Upgrade(newCards, CardPreviewStyle.None);

        // 首次打出免费
        foreach (var card in newCards)
        {
            card.EnergyCost.SetUntilPlayed(0);
            card.SetStarCostUntilPlayed(0);
        }

        // 批量加入手牌——单次飞入动画代替逐张
        await CardPileCmd.Add(newCards, PileType.Hand);

        // 获得消耗数量的能量
        await PlayerCmd.GainEnergy(count, Owner);
    }

    public override bool IsAllowed(IRunState runState)
    {
        return IsBeforeAct3TreasureChest(runState);
    }
}