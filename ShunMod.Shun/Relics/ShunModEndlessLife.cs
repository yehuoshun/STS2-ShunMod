using System.Linq;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShunMod.Core.Core.Registry;
using ShunMod.Shun.Base;

namespace ShunMod.Shun.Relics;

/// <summary>
///     生生不息 — 右键点击遗物触发。
///     消耗任意数量的手牌，每消耗一张生成随机已升级的卡牌（不分角色）加入手卡，
///     并且首次打出免费，获得消耗数量的能量。
///     无使用次数限制。
/// </summary>
[RelicPool(typeof(SharedRelicPool))]
// ReSharper disable UnusedType.Global — ContentRegistry 反射使用
// ReSharper disable UnusedType.Instantiation — ContentRegistry 反射实例化
public sealed class ShunModEndlessLife : ShunRelicModel<ShunModEndlessLife>
{
    private static readonly LocString SelectionPrompt = new("card_selection", "TO_EXHAUST");

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsAllowed(IRunState runState)
    {
        return IsBeforeAct3TreasureChest(runState);
    }

    /// <summary>
    ///     执行右键点击动作：选牌 → 消耗 → 生成升级卡 → 加手 → 首次免费 → 回能。
    /// </summary>
    public async Task ExecuteRightClick(PlayerChoiceContext choiceContext)
    {
        if (Owner == null)
            return;

        // 右键即闪烁，立即视觉反馈
        Flash();

        // 从手牌选任意张（0 到手牌上限）
        var hand = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (hand.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionPrompt, 0, hand.Count)
        {
            Cancelable = true
        };

        var selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            prefs,
            _ => true,
            this)).ToList();

        // 取消选择，不执行
        if (selected.Count == 0)
            return;

        var count = selected.Count;

        // 获取所有可生成的卡牌池（排除基础/先古/事件/代币/状态/诅咒）
        var allCards = ModelDb.AllCards
            .Where(c => c.CanBeGeneratedInCombat
                        && c.Rarity != CardRarity.Basic
                        && c.Rarity != CardRarity.Ancient
                        && c.Rarity != CardRarity.Event
                        && c.Rarity != CardRarity.Token
                        && c.Rarity != CardRarity.Status
                        && c.Rarity != CardRarity.Curse)
            .Distinct()
            .ToList();

        if (allCards.Count == 0)
            return;

        // 逐张处理：消耗手牌 → 生成升级卡 → 加入手牌 → 首次免费
        for (var i = 0; i < count; i++)
        {
            // 消耗手牌（进消耗牌堆）
            await CardCmd.Exhaust(choiceContext, selected[i]!);

            // 从全卡池随机取一张
            var canonical = Owner.PlayerRng.Transformations.NextItem(allCards);
            if (canonical == null) continue;

            // 创建随机的卡牌实例
            var newCard = Owner.Creature.CombatState!.CreateCard(canonical, Owner);

            // 升级（如果可升级）
            if (newCard.IsUpgradable)
                CardCmd.Upgrade(newCard);

            // 首次打出免费（能量+星辉）
            newCard.EnergyCost.SetUntilPlayed(0);
            newCard.SetStarCostUntilPlayed(0);

            // 加入手牌
            await CardPileCmd.AddGeneratedCardToCombat(newCard, PileType.Hand, Owner);
        }

        // 获得消耗数量的能量
        await PlayerCmd.GainEnergy(count, Owner);
    }
}