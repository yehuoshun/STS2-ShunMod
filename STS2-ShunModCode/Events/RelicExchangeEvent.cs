using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Runs;
using STS2_ShunMod.Abilities;
using STS2_ShunMod.Cards;
using STS2_ShunMod.Utils;

namespace STS2_ShunMod.Events;

/// <summary>
/// 遗物交易所 — 遗物 > 3 时触发，用遗物换能力卡。
/// 若拥有空白能力卡（FlexiblePower），各选项有概率额外附加能力。
/// </summary>
public sealed class RelicExchangeEvent : EventModel
{
    // 能力附加概率
    private const double AbilityChance = 0.5;

    public override bool IsAllowed(IRunState runState)
    {
        return runState.Players.Any(p => p.Relics.Count > 3);
    }

    // 是否拥有空白能力卡
    private bool HasFlexiblePower =>
        Owner!.PlayerCombatState.AllCards.OfType<FlexiblePower>().Any()
        || PileType.Deck.GetPile(Owner!).Cards.OfType<FlexiblePower>().Any();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var suffix = HasFlexiblePower ? "（概率附加能力）" : "";
        return
        [
            new(this, Option1, LocKey("OPT1") + suffix),
            new(this, Option2, LocKey("OPT2") + suffix),
            new(this, Option3, LocKey("OPT3") + suffix),
        ];
    }

    // ═══════════════════════════════════════════
    //  选项1：随机1遗物 → 随机能力卡 + 概率附加能力
    // ═══════════════════════════════════════════
    private async Task Option1()
    {
        var relics = Owner!.Relics.ToList();
        if (relics.Count == 0) { Finish("NO_RELICS"); return; }

        RelicHelper.RemoveRelic(Owner, relics[Rng.NextInt(relics.Count)]);

        var card = GetRandomPowerCard();
        if (card == null) { Finish("NO_CARD"); return; }

        var newCard = Owner.RunState.CreateCard(card, Owner);
        await CardPileCmd.Add(newCard, PileType.Deck);

        // 概率附加能力
        var abilityMsg = TryGrantAbility();
        Finish(string.IsNullOrEmpty(abilityMsg) ? "OPT1_DONE" : "OPT1_DONE_ABILITY", abilityMsg);
    }

    // ═══════════════════════════════════════════
    //  选项2：随机2遗物 → 注能能力卡 + 概率附加能力
    // ═══════════════════════════════════════════
    private async Task Option2()
    {
        var relics = Owner!.Relics.ToList();
        if (relics.Count < 2) { Finish("NO_RELICS"); return; }

        var r1 = relics[Rng.NextInt(relics.Count)];
        relics.Remove(r1);
        var r2 = relics[Rng.NextInt(relics.Count)];
        RelicHelper.RemoveRelic(Owner, r1);
        RelicHelper.RemoveRelic(Owner, r2);

        var card = GetRandomPowerCard();
        if (card == null) { Finish("NO_CARD"); return; }

        var newCard = Owner.RunState.CreateCard(card, Owner);
        var imbued = ModelDb.Enchantment<Imbued>();
        CardCmd.Enchant(imbued.ToMutable(), newCard, 1);

        await CardPileCmd.Add(newCard, PileType.Deck);

        var abilityMsg = TryGrantAbility();
        Finish(string.IsNullOrEmpty(abilityMsg) ? "OPT2_DONE" : "OPT2_DONE_ABILITY", abilityMsg);
    }

    // ═══════════════════════════════════════════
    //  选项3：获取本 Mod 卡牌 + 概率附加能力
    // ═══════════════════════════════════════════
    private async Task Option3()
    {
        var pool = Owner!.Character.CardPool;
        var modCards = pool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.GetType().Namespace?.StartsWith("STS2_ShunMod") == true)
            .ToList();

        if (modCards.Count == 0) { Finish("NO_MOD_CARD"); return; }

        var selected = modCards[Rng.NextInt(modCards.Count)];
        var newCard = Owner.RunState.CreateCard(selected, Owner);
        await CardPileCmd.Add(newCard, PileType.Deck);

        var abilityMsg = TryGrantAbility();
        Finish(string.IsNullOrEmpty(abilityMsg) ? "OPT3_DONE" : "OPT3_DONE_ABILITY", abilityMsg);
    }

    // ── 能力附加 ──

    /// <summary>
    /// 有概率给空白能力卡附加一个随机能力。
    /// 返回能力描述文本，未触发返回空字符串。
    /// </summary>
    private string TryGrantAbility()
    {
        if (!HasFlexiblePower) return "";
        if (Rng.NextDouble() >= AbilityChance) return "";

        var config = AbilityConfig.Load();
        var keys = config.Keys.ToList();
        if (keys.Count == 0) return "";

        var key = keys[Rng.NextInt(keys.Count)];
        var def = config[key];
        AbilityStore.Add(key, 1);

        return $"你的空白能力卡获得了「{def.name} +{def.per_stack}」！（当前层数：{AbilityStore.Get(key)}）";
    }

    // ── 辅助 ──

    private CardModel? GetRandomPowerCard()
    {
        var all = new List<CardModel>();
        foreach (var ch in ModelDb.AllCharacters)
        {
            var powers = ch.CardPool.GetUnlockedCards(Owner!.UnlockState, Owner.RunState.CardMultiplayerConstraint)
                .Where(c => c.Type == CardType.Power);
            all.AddRange(powers);
        }
        return all.Count == 0 ? null : all[Rng.NextInt(all.Count)];
    }

    private string LocKey(string name) => $"{Id.Entry}.pages.INITIAL.options.{name}";

    private void Finish(string pageKey, string? extra = null)
    {
        var text = L10NLookup($"SHUNMOD_RELIC_EXCHANGE.pages.{pageKey}.description");
        if (!string.IsNullOrEmpty(extra))
            text += "\n\n" + extra;
        SetEventFinished(text);
    }
}
