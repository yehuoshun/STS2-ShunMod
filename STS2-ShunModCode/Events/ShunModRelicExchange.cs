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
    private static readonly Random Rnd = new();

    // ════════════════════════════════════════════════
    // 背景图
    // ════════════════════════════════════════════════

    /// <summary>
    ///     重写资源路径：将默认 events/xxx.png 指向 mod 目录下的图片。
    ///     同时把 mod 路径加入列表（防止匹配失败时也触发缓存）。
    /// </summary>
    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        var paths = base.GetAssetPaths(runState).ToList();
        var modPath = $"res://STS2-ShunMod/images/events/{Id.Entry.ToLowerInvariant()}.png";

        // 替换默认路径
        var defaultPath = ImageHelper.GetImagePath($"events/{Id.Entry.ToLowerInvariant()}.png");
        var i = paths.IndexOf(defaultPath);
        if (i >= 0) paths[i] = modPath;
        else paths.Add(modPath); // 兜底：直接追加

        return paths;
    }

    // ════════════════════════════════════════════════
    // 状态
    // ════════════════════════════════════════════════

    private RelicModel? _loseRelic1;
    private RelicModel? _gainRelic;
    private RelicModel? _loseRelic2;
    private EnchantmentModel? _enchant;
    private CardModel? _enchantTarget;

    // ════════════════════════════════════════════════
    // DynamicVars — 反射写入 String 属性
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
        if (Owner == null) return;
        Roll(Owner);
        SetStr("LOSE_RELIC_1", _loseRelic1?.Title);
        SetStr("GAIN_RELIC", _gainRelic?.Title);
        SetStr("LOSE_RELIC_2", _loseRelic2?.Title);
        SetStr("ENCHANT_NAME", _enchant?.Title);
        // CardModel.Title 类型可能是 string（非 LocString）
        SetStr("CARD_NAME", AsLoc(_enchantTarget?.Title));
    }

    private void SetStr(string key, LocString? val)
    {
        if (val == null || !DynamicVars.TryGetValue(key, out var dv) || dv is not StringVar sv) return;
        var t = sv.GetType();

        // 按优先级尝试属性（必须类型兼容才写，避免 LocString→decimal 强转崩溃）
        foreach (var name in new[] { "String", "StringValue", "BaseValue", "Value" })
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.CanWrite != true) continue;

            if (prop.PropertyType == typeof(LocString) || prop.PropertyType == typeof(LocString?))
            {
                prop.SetValue(sv, val);
                return;
            }
            if (prop.PropertyType == typeof(string))
            {
                prop.SetValue(sv, val.ToString());
                return;
            }
        }

        // 回退：反射写私有字段
        foreach (var name in new[] { "_value", "_string", "_locString", "value" })
        {
            var field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(sv, val);
                return;
            }
        }
    }

    private static LocString? AsLoc(LocString? l) => l;
    private static LocString? AsLoc(string? s) => s != null ? new LocString("", s) : null;

    // ════════════════════════════════════════════════
    // 随机
    // ════════════════════════════════════════════════

    private void Roll(Player player)
    {
        _loseRelic1 = null;
        _loseRelic2 = null;
        _gainRelic = null;
        _enchant = null;
        _enchantTarget = null;

        var relics = GetTradeableRelics(player);
        if (relics.Count == 0) return;

        _loseRelic1 = relics[Rnd.Next(relics.Count)];
        _gainRelic = RollRandomRelic();
        _enchant = RollRandomEnchant();
        _enchantTarget = RollRandomCard(player);

        if (relics.Count >= 2)
            do { _loseRelic2 = relics[Rnd.Next(relics.Count)]; }
            while (_loseRelic2 == _loseRelic1);
    }

    // ════════════════════════════════════════════════
    // 选项
    // ════════════════════════════════════════════════

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return BuildList();
    }

    private IReadOnlyList<EventOption> BuildList()
    {
        var player = Owner;
        if (player == null) return [ExitOpt()];

        var relics = GetTradeableRelics(player);
        var list = new List<EventOption>();

        // OPT_1: 遗物 → 遗物
        if (_loseRelic1 != null && _gainRelic != null)
        {
            var l = _loseRelic1; var g = _gainRelic;
            list.Add(Opt(() =>
            {
                TryRemoveRelic(Owner!, l);
                TryGiveRelic(Owner!, g);
                AfterTrade();
                return Task.CompletedTask;
            }, "OPT_1"));
        }

        // OPT_2: 遗物 → 附魔（≥2 遗物）
        if (relics.Count >= 2 && _loseRelic2 != null && _enchant != null && _enchantTarget != null)
        {
            var l = _loseRelic2; var e = _enchant; var c = _enchantTarget;
            list.Add(Opt(async () =>
            {
                TryRemoveRelic(Owner!, l);
                CardCmd.Enchant(e, c, 1);
                AfterTrade();
            }, "OPT_2"));
        }

        // OPT_3: 扣血刷新
        if (relics.Count > 0)
        {
            list.Add(Opt(async () =>
            {
                await DamagePlayer(Owner!, 5);
                AfterTrade();
            }, "OPT_3"));
        }

        // OPT_4: 退出
        list.Add(ExitOpt());
        return list;
    }

    private EventOption Opt(Func<Task> cb, string key)
    {
        return new EventOption(this, cb, OptKey(key));
    }

    private EventOption ExitOpt()
    {
        return new EventOption(this, async () =>
            { SetEventFinished(L10NLookup("pages.CLOSE.description")); },
            OptKey("OPT_4"));
    }

    private string OptKey(string key) => $"{Id.Entry}.pages.INITIAL.options.{key}";

    /// <summary>
    ///     交易/刷新后：重新 Roll → 写 DynamicVars → 刷新选项。
    ///     用反射找属性的方式更新选项列表（兼容 EventOptions / Options 命名差异）。
    /// </summary>
    private void AfterTrade()
    {
        var player = Owner!;
        Roll(player);
        SetStr("LOSE_RELIC_1", _loseRelic1?.Title);
        SetStr("GAIN_RELIC", _gainRelic?.Title);
        SetStr("LOSE_RELIC_2", _loseRelic2?.Title);
        SetStr("ENCHANT_NAME", _enchant?.Title);
        SetStr("CARD_NAME", AsLoc(_enchantTarget?.Title));

        var opts = BuildList();
        // 反射写选项列表属性/字段
        var t = GetType();
        foreach (var name in new[] { "EventOptions", "CurrentOptions", "Options" })
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.CanWrite == true) { prop.SetValue(this, opts); return; }
        }
        foreach (var name in new[] { "_currentOptions", "_eventOptions", "_options" })
        {
            var field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) { field.SetValue(this, opts); return; }
        }

        // 最后回退：SetEventState(页面描述, 选项列表)
        typeof(EventModel).GetMethod("SetEventState",
            [typeof(LocString), typeof(IReadOnlyList<EventOption>)])
            ?.Invoke(this, [L10NLookup("pages.INITIAL.description"), opts]);
    }

    // ════════════════════════════════════════════════
    // 遗物操作
    // ════════════════════════════════════════════════

    private static void TryRemoveRelic(Player player, RelicModel relic)
    {
        if (typeof(Player).GetField("_relics",
            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(player) is List<RelicModel> list)
        {
            list.Remove(relic);
            if (typeof(Player).GetField("RelicRemoved",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(player) is Delegate del)
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
            var cur = Convert.ToDecimal(hp.GetValue(c) ?? 0m);
            hp.SetValue(c, Math.Max(0, cur - amount));
        }
        await Task.CompletedTask;
    }

    // ════════════════════════════════════════════════
    // 随机池 & 过滤
    // ════════════════════════════════════════════════

    private static RelicModel? RollRandomRelic()
    {
        IEnumerable<RelicModel>? all = null;
        var p = typeof(ModelDb).GetProperty("AllRelics",
            BindingFlags.Public | BindingFlags.Static);
        if (p?.GetValue(null) is IEnumerable<RelicModel> r) all = r;
        else all = typeof(RelicModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(RelicModel).IsAssignableFrom(t))
            .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t)))
            .OfType<RelicModel>();
        var pool = all.Where(IsTradeable).ToList();
        return pool.Count > 0 ? pool[Rnd.Next(pool.Count)] : null;
    }

    private static EnchantmentModel? RollRandomEnchant()
    {
        var pool = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t)
                && t.Name != "EnchantmentModel")
            .Select(t => ModelDb.GetByIdOrNull<EnchantmentModel>(ModelDb.GetId(t)))
            .OfType<EnchantmentModel>().ToList();
        return pool.Count > 0 ? pool[Rnd.Next(pool.Count)] : null;
    }

    private static CardModel? RollRandomCard(Player player) =>
        player.Deck.Cards is { Count: > 0 } d ? d[Rnd.Next(d.Count)] : null;

    private static bool IsTradeable(RelicModel relic) =>
        typeof(RelicModel).GetProperty("Rarity")?.GetValue(relic)?.ToString() != "Starter"
        && relic.GetType().Name != "Circlet";

    private static List<RelicModel> GetTradeableRelics(Player? player) =>
        player?.Relics.Where(IsTradeable).ToList() ?? [];
}
