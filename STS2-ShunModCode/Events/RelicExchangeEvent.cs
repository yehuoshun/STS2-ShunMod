using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2_ShunMod.Utils;

namespace STS2_ShunMod.Events;

/// <summary>
/// 遗物交易所 — 遗物 > 3 时触发，用遗物换能力卡。
/// </summary>
public sealed class RelicExchangeEvent : EventModel
{
    public override bool IsAllowed(IRunState runState)
    {
        return runState.Players.Any(p => p.Relics.Count > 3);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return
        [
            new(this, Option1, LocKey("OPT1")),
            new(this, Option2, LocKey("OPT2")),
            new(this, Option3, LocKey("OPT3")),
        ];
    }

    // ── 选项1：随机1遗物 → 随机能力卡（所有颜色） ──
    private async Task Option1()
    {
        var relics = Owner!.Relics.ToList();
        if (relics.Count == 0) { Finish("NO_RELICS"); return; }

        RelicHelper.RemoveRelic(Owner, relics[Rng.NextInt(relics.Count)]);

        var card = GetRandomPowerCard();
        if (card == null) { Finish("NO_CARD"); return; }

        var newCard = Owner.RunState.CreateCard(card, Owner);
        await CardPileCmd.Add(newCard, PileType.Deck);
        Finish("OPT1_DONE");
    }

    // ── 选项2：随机2遗物 → 有注能附魔的能力卡 ──
    private async Task Option2()
    {
        var relics = Owner!.Relics.ToList();
        if (relics.Count < 2) { Finish("NO_RELICS"); return; }

        var r1 = relics[Rng.NextInt(relics.Count)];
        relics.Remove(r1);
        var r2 = relics[Rng.NextInt(relics.Count)];
        RelicHelper.RemoveRelic(Owner, r1);
        RelicHelper.RemoveRelic(Owner, r2);

        var newCard = Owner!.RunState.CreateCard(card, Owner);
        var infuse = ModelDb.GetById<EnchantmentModel>("Infuse");
        if (infuse != null) newCard.Enchantments.Add(infuse);

        await CardPileCmd.Add(newCard, PileType.Deck);
        Finish("OPT2_DONE");
    }

    // ── 选项3：获取本 Mod 卡牌 ──
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
        Finish("OPT3_DONE");
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

    private void Finish(string pageKey)
    {
        SetEventFinished(L10NLookup($"SHUNMOD_RELIC_EXCHANGE.pages.{pageKey}.description"));
    }
}
