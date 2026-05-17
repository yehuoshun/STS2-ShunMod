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
/// </summary>
public class ShunModRelicExchange : EventModel
{
    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        var paths = base.GetAssetPaths(runState).ToList();
        var def = ImageHelper.GetImagePath($"events/{Id.Entry.ToLowerInvariant()}.png");
        var mod = $"res://STS2-ShunMod/images/events/{Id.Entry.ToLowerInvariant()}.png";
        var i = paths.IndexOf(def);
        if (i >= 0) paths[i] = mod;
        return paths;
    }

    // ════════════════════════════════════════════════════
    // 状态 & DynamicVars
    // ════════════════════════════════════════════════════

    private static readonly Random Rnd = new();
    private RelicModel? _loseRelic1;
    private RelicModel? _gainRelic;
    private RelicModel? _loseRelic2;
    private EnchantmentModel? _enchantment;
    private CardModel? _enchantTarget;

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
        var v = DynamicVars;
        SetVarString(v, "LOSE_RELIC_1", _loseRelic1?.Title);
        SetVarString(v, "GAIN_RELIC", _gainRelic?.Title);
        SetVarString(v, "LOSE_RELIC_2", _loseRelic2?.Title);
        SetVarString(v, "ENCHANT_NAME", _enchantment?.Title);
        // CardModel.Title 可能返回 string（非 LocString），统一转
        SetVarString(v, "CARD_NAME", AsLoc(_enchantTarget?.Title));
    }

    private static readonly LocString NilLoc = new("", "");
    private static LocString? AsLoc(LocString? loc) => loc;
    private static LocString? AsLoc(string? s) => s != null ? new LocString("", s) : null;

    private static void SetVarString(IReadOnlyDictionary<string, DynamicVar> vars, string key, LocString? value)
    {
        if (!vars.TryGetValue(key, out var dv) || dv is not StringVar sv) return;

        // 优先: .String (LocString 型)
        var stringProp = typeof(StringVar).GetProperty("String", BindingFlags.Public | BindingFlags.Instance);
        if (stringProp?.CanWrite == true && stringProp.PropertyType == typeof(LocString))
        {
            stringProp.SetValue(sv, value ?? NilLoc);
            return;
        }

        // 其次: .BaseValue (string 型)
        var baseProp = typeof(StringVar).GetProperty("BaseValue", BindingFlags.Public | BindingFlags.Instance);
        if (baseProp?.CanWrite == true)
        {
            baseProp.SetValue(sv, (value ?? NilLoc).ToString());
            return;
        }

        // 回退: 反射 _value
        var field = typeof(StringVar).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(sv, value ?? NilLoc);
    }

    // ════════════════════════════════════════════════════
    // 随机
    // ════════════════════════════════════════════════════

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

        if (relics.Count >= 2)
        {
            do { _loseRelic2 = relics[Rnd.Next(relics.Count)]; }
            while (_loseRelic2 == _loseRelic1);
        }

        _enchantment = RollRandomEnchant();
        _enchantTarget = RollRandomCard(player);
    }

    // ════════════════════════════════════════════════════
    // 选项
    // ════════════════════════════════════════════════════

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return BuildOptionList();
    }

    private string OptKey(string key) => $"{Id.Entry}.pages.INITIAL.options.{key}";

    private IReadOnlyList<EventOption> BuildOptionList()
    {
        var player = Owner;
        if (player == null) return [MakeExitOption()];

        var relics = GetTradeableRelics(player);
        var list = new List<EventOption>();

        if (_loseRelic1 != null && _gainRelic != null)
        {
            var lose = _loseRelic1;
            var gain = _gainRelic;
            list.Add(new EventOption(this, async () =>
            {
                TryRemoveRelic(Owner!, lose);
                TryGiveRelic(Owner!, gain);
                AfterTrade();
            }, OptKey("OPT_1")));
        }

        if (relics.Count >= 2 && _loseRelic2 != null && _enchantment != null && _enchantTarget != null)
        {
            var lose = _loseRelic2;
            var ench = _enchantment;
            var card = _enchantTarget;
            list.Add(new EventOption(this, async () =>
            {
                TryRemoveRelic(Owner!, lose);
                CardCmd.Enchant(ench, card, 1);
                AfterTrade();
            }, OptKey("OPT_2")));
        }

        if (relics.Count > 0)
        {
            list.Add(new EventOption(this, async () =>
            {
                await DamagePlayer(Owner!, 5);
                AfterTrade();
            }, OptKey("OPT_3")));
        }

        list.Add(MakeExitOption());
        return list;
    }

    private void AfterTrade()
    {
        var player = Owner!;
        Roll(player);
        ApplyVars();
        SetEventState(L10NLookup("pages.INITIAL.description"), BuildOptionList());
    }

    private EventOption MakeExitOption()
    {
        return new EventOption(this, async () =>
            { SetEventFinished(L10NLookup("pages.CLOSE.description")); },
            OptKey("OPT_4"));
    }

    // ════════════════════════════════════════════════════
    // 遗物操作
    // ════════════════════════════════════════════════════

    private static void TryRemoveRelic(Player player, RelicModel relic)
    {
        var field = typeof(Player).GetField("_relics",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(player) is List<RelicModel> list)
        {
            list.Remove(relic);
            TryInvokeEvent(player, "RelicRemoved", relic);
        }
    }

    private static void TryGiveRelic(Player player, RelicModel relic)
    {
        typeof(Player).GetMethod("AddRelicInternal",
            BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(player, [relic]);
    }

    private static void TryInvokeEvent(object target, string eventName, params object[] args)
    {
        var field = target.GetType().GetField(eventName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(target) is Delegate del)
            foreach (var h in del.GetInvocationList())
                try { h.DynamicInvoke(args); } catch { }
    }

    // ════════════════════════════════════════════════════
    // 伤害
    // ════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════
    // 随机池
    // ════════════════════════════════════════════════════

    private static RelicModel? RollRandomRelic()
    {
        var p = typeof(ModelDb).GetProperty("AllRelics",
            BindingFlags.Public | BindingFlags.Static);

        IEnumerable<RelicModel>? all = null;
        if (p?.GetValue(null) is IEnumerable<RelicModel> relics) all = relics;
        else
        {
            var types = typeof(RelicModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(RelicModel).IsAssignableFrom(t));
            all = types
                .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t)))
                .OfType<RelicModel>();
        }

        var pool = all.Where(IsTradeable).ToList();
        return pool.Count > 0 ? pool[Rnd.Next(pool.Count)] : null;
    }

    private static EnchantmentModel? RollRandomEnchant()
    {
        var types = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t)
                && t.Name != "EnchantmentModel");
        var valid = types
            .Select(t => ModelDb.GetByIdOrNull<EnchantmentModel>(ModelDb.GetId(t)))
            .OfType<EnchantmentModel>()
            .ToList();
        return valid.Count > 0 ? valid[Rnd.Next(valid.Count)] : null;
    }

    private static CardModel? RollRandomCard(Player player)
    {
        var deck = player.Deck.Cards;
        return deck.Count > 0 ? deck[Rnd.Next(deck.Count)] : null;
    }

    // ════════════════════════════════════════════════════
    // 过滤
    // ════════════════════════════════════════════════════

    private static bool IsTradeable(RelicModel relic)
    {
        var rp = typeof(RelicModel).GetProperty("Rarity");
        var rarity = rp?.GetValue(relic);
        if (rarity != null && rarity.ToString() == "Starter") return false;
        if (relic.GetType().Name == "Circlet") return false;
        return true;
    }

    private static List<RelicModel> GetTradeableRelics(Player? player)
    {
        if (player == null) return [];
        return player.Relics.Where(IsTradeable).ToList();
    }
}
