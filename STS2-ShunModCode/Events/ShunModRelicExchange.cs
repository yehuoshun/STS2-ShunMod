using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_ShunMod.Events;

/// <summary>
///     遗物交易所 — 随机遗物换遗物/附魔，可反复交易直到退出。
///     严格参照 skill 标准 EventModel 模式。
/// </summary>
public class ShunModRelicExchange : EventModel
{
    private static readonly Random Rnd = new();

    // ════════════════════════════════════════════════
    // 背景图
    // ════════════════════════════════════════════════

    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        var paths = base.GetAssetPaths(runState).ToList();
        var def = ImageHelper.GetImagePath($"events/{Id.Entry.ToLowerInvariant()}.png");
        var mod = $"res://STS2-ShunMod/images/events/{Id.Entry.ToLowerInvariant()}.png";
        var i = paths.IndexOf(def);
        if (i >= 0) paths[i] = mod;
        return paths;
    }

    // ════════════════════════════════════════════════
    // 状态
    // ════════════════════════════════════════════════

    private RelicModel? _loseRelic1;
    private RelicModel? _gainRelic;
    private RelicModel? _loseRelic2;
    private EnchantmentModel? _enchantment;
    private CardModel? _enchantTarget;
    private PlayerChoiceContext? _ctx;

    // ════════════════════════════════════════════════
    // DynamicVars — 反射写入（Value→String API 变动兼容）
    // ════════════════════════════════════════════════

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("LOSE_RELIC_1", ""),
        new StringVar("GAIN_RELIC", ""),
        new StringVar("LOSE_RELIC_2", ""),
        new StringVar("ENCHANT_NAME", ""),
        new StringVar("CARD_NAME", "")
    ];

    public override void CalculateVars()
    {
        var player = Owner;
        if (player == null) return;
        Roll(player);
        ApplyVars();
    }

    private void ApplyVars()
    {
        SetVarStr("LOSE_RELIC_1", _loseRelic1?.Title);
        SetVarStr("GAIN_RELIC", _gainRelic?.Title);
        SetVarStr("LOSE_RELIC_2", _loseRelic2?.Title);
        SetVarStr("ENCHANT_NAME", _enchantment?.Title);
        SetVarStr("CARD_NAME", AsLoc(_enchantTarget?.Title));
    }

    private void SetVarStr(string key, dynamic? val)
    {
        if (val == null || !DynamicVars.TryGetValue(key, out var dv) || dv is not StringVar sv) return;
        try
        {
            sv.GetType().GetProperty("String",
                BindingFlags.Public | BindingFlags.Instance)?.SetValue(sv, val);
        }
        catch { }
    }

    private static LocString? AsLoc(LocString? loc) => loc;
    private static LocString? AsLoc(string? s) => s != null ? new LocString("", s) : null;

    // ════════════════════════════════════════════════
    // 随机
    // ════════════════════════════════════════════════

    private void Roll(Player player)
    {
        _loseRelic1 = _loseRelic2 = null;
        _gainRelic = null;
        _enchantment = null;
        _enchantTarget = null;

        var relics = GetTradeableRelics(player);
        if (relics.Count == 0) return;

        _loseRelic1 = relics[Rnd.Next(relics.Count)];
        _gainRelic = RollRandomRelic();
        _enchantment = RollRandomEnchant();
        _enchantTarget = RollRandomCard(player);

        if (relics.Count >= 2)
            do { _loseRelic2 = relics[Rnd.Next(relics.Count)]; }
            while (_loseRelic2 == _loseRelic1);
    }

    // ════════════════════════════════════════════════
    // 选项 — skill 标准 EventOption(ctx, key, callback)
    // ════════════════════════════════════════════════

    protected override IReadOnlyList<EventOption> GenerateInitialOptions(PlayerChoiceContext ctx)
    {
        _ctx = ctx;
        return BuildList();
    }

    private IReadOnlyList<EventOption> BuildList()
    {
        var p = Owner;
        var ctx = _ctx;
        if (p == null || ctx == null) return [];

        var relics = GetTradeableRelics(p);
        var list = new List<EventOption>();

        if (_loseRelic1 != null && _gainRelic != null)
        {
            var l = _loseRelic1;
            var g = _gainRelic;
            list.Add(new EventOption(ctx, InitialOptionKey("OPT_1"), async () =>
            {
                TryRemoveRelic(Owner!, l);
                TryGiveRelic(Owner!, g);
                AfterTrade();
            }));
        }

        if (relics.Count >= 2 && _loseRelic2 != null && _enchantment != null && _enchantTarget != null)
        {
            var l = _loseRelic2;
            var e = _enchantment;
            var c = _enchantTarget;
            list.Add(new EventOption(ctx, InitialOptionKey("OPT_2"), async () =>
            {
                TryRemoveRelic(Owner!, l);
                CardCmd.Enchant(e, c, 1);
                AfterTrade();
            }));
        }

        if (relics.Count > 0)
        {
            list.Add(new EventOption(ctx, InitialOptionKey("OPT_3"), async () =>
            {
                await DamagePlayer(Owner!, 5);
                AfterTrade();
            }));
        }

        list.Add(new EventOption(ctx, InitialOptionKey("OPT_4"), async () =>
            { SetEventFinished(L10NLookup("pages.CLOSE.description")); }));

        return list;
    }

    private void AfterTrade()
    {
        var p = Owner!;
        Roll(p);
        ApplyVars();
        var opts = BuildList();
        // SetEventState — 优先单参（skill 标准），编译不通时双参
        try
        {
            typeof(EventModel).GetMethod("SetEventState",
                [typeof(IReadOnlyList<EventOption>)])?.Invoke(this, [opts]);
        }
        catch
        {
            typeof(EventModel).GetMethod("SetEventState",
                [typeof(LocString), typeof(IReadOnlyList<EventOption>)])
                ?.Invoke(this, [L10NLookup("pages.INITIAL"), opts]);
        }
    }

    // ════════════════════════════════════════════════
    // 遗物操作
    // ════════════════════════════════════════════════

    private static void TryRemoveRelic(Player player, RelicModel relic)
    {
        var field = typeof(Player).GetField("_relics",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(player) is List<RelicModel> list)
        {
            list.Remove(relic);
            var evtField = typeof(Player).GetField("RelicRemoved",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (evtField?.GetValue(player) is Delegate del)
                foreach (var h in del.GetInvocationList())
                    try { h.DynamicInvoke(relic); } catch { }
        }
    }

    private static void TryGiveRelic(Player player, RelicModel relic)
    {
        typeof(Player).GetMethod("AddRelicInternal",
            BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(player, [relic]);
    }

    // ════════════════════════════════════════════════
    // 伤害
    // ════════════════════════════════════════════════

    private static async Task DamagePlayer(Player player, int amount)
    {
        var c = player.Creature;
        var m = typeof(Creature).GetMethod("ChangeHp",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(decimal)]);
        if (m != null) { m.Invoke(c, [(decimal)-amount]); await Task.CompletedTask; return; }

        m = typeof(Creature).GetMethod("TakeDamage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (m != null) { m.Invoke(c, [(decimal)amount]); await Task.CompletedTask; return; }

        var hp = typeof(Creature).GetProperty("CurrentHp");
        if (hp != null)
        {
            var cur = (decimal)(hp.GetValue(c) ?? 0m);
            hp.SetValue(c, Math.Max(0, cur - amount));
        }
        await Task.CompletedTask;
    }

    // ════════════════════════════════════════════════
    // 随机池 & 过滤
    // ════════════════════════════════════════════════

    private static RelicModel? RollRandomRelic()
    {
        var p = typeof(ModelDb).GetProperty("AllRelics",
            BindingFlags.Public | BindingFlags.Static);
        IEnumerable<RelicModel>? all = null;
        if (p?.GetValue(null) is IEnumerable<RelicModel> relics) all = relics;
        else
        {
            all = typeof(RelicModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(RelicModel).IsAssignableFrom(t))
                .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t)))
                .OfType<RelicModel>();
        }
        var pool = all.Where(IsTradeable).ToList();
        return pool.Count > 0 ? pool[Rnd.Next(pool.Count)] : null;
    }

    private static EnchantmentModel? RollRandomEnchant()
    {
        var valid = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t)
                && t.Name != "EnchantmentModel")
            .Select(t => ModelDb.GetByIdOrNull<EnchantmentModel>(ModelDb.GetId(t)))
            .OfType<EnchantmentModel>()
            .ToList();
        return valid.Count > 0 ? valid[Rnd.Next(valid.Count)] : null;
    }

    private static CardModel? RollRandomCard(Player player)
        => player.Deck.Cards is { Count: > 0 } deck
            ? deck[Rnd.Next(deck.Count)] : null;

    private static bool IsTradeable(RelicModel relic)
    {
        var rp = typeof(RelicModel).GetProperty("Rarity");
        if (rp?.GetValue(relic)?.ToString() == "Starter") return false;
        return relic.GetType().Name != "Circlet";
    }

    private static List<RelicModel> GetTradeableRelics(Player? player)
        => player?.Relics.Where(IsTradeable).ToList() ?? [];
}
