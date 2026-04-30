using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_ShunMod.Events;

/// <summary>
/// 遗物交换事件 — 遗物 > 3 时触发，用遗物换能力卡。
/// </summary>
public sealed class RelicExchangeEvent : EventModel
{
    public override ActModel[] Acts => [];

    public override bool IsAllowed(IRunState runState)
    {
        // 玩家遗物数量 > 3 时触发
        return runState.Players.Any(p => p.Relics.Count > 3);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            // 选项1：随机1遗物 → 随机能力卡（所有颜色）
            new(this, Option1_RandomRelicForPower, LocKey("INITIAL.options.OPT1")),

            // 选项2：随机2遗物 → 有注能附魔的能力卡
            new(this, Option2_TwoRelicsForInfusedPower, LocKey("INITIAL.options.OPT2")),

            // 选项3：获取本 Mod 卡牌
            new(this, Option3_GetShunModCard, LocKey("INITIAL.options.OPT3")),
        };

        return options;
    }

    // ── 选项1：随机1遗物 → 随机能力卡 ──
    private async Task Option1_RandomRelicForPower()
    {
        var relics = Owner!.Relics.ToList();
        if (relics.Count == 0)
        {
            SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.NO_RELICS.description"));
            return;
        }

        // 随机移除1个遗物
        var relic = relics[Owner.RunState.Rng.Niche.NextInt(relics.Count)];
        Owner.Relics.Remove(relic);
        await RelicVfxCmd.Obtained(relic);

        // 从所有角色卡池随机一张能力卡
        var powerCard = GetRandomPowerCard(Owner);
        if (powerCard == null)
        {
            SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.NO_CARD.description"));
            return;
        }

        var newCard = Owner.RunState.CreateCard(powerCard, Owner);
        await CardPileCmd.Add(newCard, PileType.Deck);

        SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.OPT1_DONE.description"));
    }

    // ── 选项2：随机2遗物 → 有注能附魔的能力卡 ──
    private async Task Option2_TwoRelicsForInfusedPower()
    {
        var relics = Owner!.Relics.ToList();
        if (relics.Count < 2)
        {
            SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.NO_RELICS.description"));
            return;
        }

        // 随机移除2个遗物
        for (int i = 0; i < 2; i++)
        {
            var r = relics[Owner.RunState.Rng.Niche.NextInt(relics.Count)];
            Owner.Relics.Remove(r);
            relics.Remove(r);
        }

        // 随机一张能力卡
        var powerCard = GetRandomPowerCard(Owner);
        if (powerCard == null)
        {
            SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.NO_CARD.description"));
            return;
        }

        var newCard = Owner.RunState.CreateCard(powerCard, Owner);

        // 附魔：注能（Infuse）
        var infuse = ModelDb.GetById<EnchantmentModel>("Infuse");
        if (infuse != null)
            newCard.Enchantments.Add(infuse);

        await CardPileCmd.Add(newCard, PileType.Deck);

        SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.OPT2_DONE.description"));
    }

    // ── 选项3：获取本 Mod 卡牌 ──
    private async Task Option3_GetShunModCard()
    {
        // 从本 Mod 卡池随机获取一张卡
        var pool = Owner!.Character.CardPool;
        var modCards = pool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.GetType().Namespace?.StartsWith("STS2_ShunMod") == true)
            .ToList();

        if (modCards.Count == 0)
        {
            SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.NO_MOD_CARD.description"));
            return;
        }

        var selected = modCards[Owner.RunState.Rng.Niche.NextInt(modCards.Count)];
        var newCard = Owner.RunState.CreateCard(selected, Owner);
        await CardPileCmd.Add(newCard, PileType.Deck);

        SetEventFinished(L10NLookup("SHUNMOD_RELIC_EXCHANGE.pages.OPT3_DONE.description"));
    }

    // ── 辅助：随机能力卡（所有颜色） ──
    private CardModel? GetRandomPowerCard(PlayerModel player)
    {
        var allPowers = new List<CardModel>();

        foreach (var charModel in ModelDb.AllCharacters)
        {
            var pool = charModel.CardPool;
            var powers = pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                .Where(c => c.Type == CardType.Power)
                .ToList();
            allPowers.AddRange(powers);
        }

        if (allPowers.Count == 0) return null;
        return allPowers[player.RunState.Rng.Niche.NextInt(allPowers.Count)];
    }

    // ── 辅助：本地化键 ──
    private string LocKey(string path) => $"{Id.Entry}.pages.{path}";
}
