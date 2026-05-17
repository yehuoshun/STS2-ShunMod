using System.Reflection;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.CardSelection;
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

    // ════════════════════════════════════════════════
    // DynamicVars — 反射写入 String 属性
    // ════════════════════════════════════════════════

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("LOSE_RELIC_1", ""),
        new StringVar("GAIN_RELIC", ""),
        new StringVar("LOSE_RELIC_2", ""),
        new StringVar("ENCHANT_NAME", "")
    ];

    public override void CalculateVars()
    {
        if (Owner == null) return;
        Roll(Owner);
        SetStr("LOSE_RELIC_1", ToText(_loseRelic1?.Title));
        SetStr("GAIN_RELIC", ToText(_gainRelic?.Title));
        SetStr("LOSE_RELIC_2", ToText(_loseRelic2?.Title));
        SetStr("ENCHANT_NAME", ToText(_enchant?.Title));
    }

    /// <summary>解析 LocString 为显示文本</summary>
    /// <remarks>
    ///     优先用游戏原生 API GetRawText/GetFormattedText（能正确走 LocManager 解析），
    ///     解析失败时退到 LocEntryKey 提取可读名称。
    ///     所有路径最终经 SanitizeForBbcode 清洗，防止 ASCII 方括号被 MegaRichTextLabel 当 BBCode 解析。
    /// </remarks>
    private static string ToText(LocString? loc)
    {
        if (loc == null) return "?";

        // 1. 游戏原生解析
        try { return SanitizeForBbcode(loc.GetRawText()); }
        catch (LocException) { }
        try { return SanitizeForBbcode(loc.GetFormattedText()); }
        catch (LocException) { }

        // 2. 回退：从 LocEntryKey 提取可读名称
        var key = loc.LocEntryKey ?? "";
        // 去掉 .title / .description / .flavor 等后缀
        key = System.Text.RegularExpressions.Regex.Replace(key, @"\.(title|description|flavor|name|additionalRestSiteHealText)(\..*)?$", "");
        // 取最后一段（去掉 MOD_ID 前缀）
        var parts = key.Split('.');
        key = parts.Length > 0 ? parts[^1] : key;
        // SNAKE_CASE → Title Case
        key = System.Text.RegularExpressions.Regex.Replace(key, @"_+", " ").Trim();
        if (key.Length > 0)
        {
            var words = key.Split(' ');
            key = string.Join(" ", System.Array.ConvertAll(words, w =>
                w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant() : w));
        }
        return string.IsNullOrWhiteSpace(key) ? "?" : SanitizeForBbcode(key);
    }

    /// <summary>清洗可能被误认为 BBCode 的方括号文本，防止 MegalLabel 解析崩溃</summary>
    /// <remarks>
    ///     把 ASCII 方括号 [ ] 替换为全角 ［ ］，外观近似但不会被 BBCode 解析器当成标签。
    ///     典型场景：LocString 解析失败时，回退文本自带 [LocString table ...] 格式，
    ///     当它嵌入 [gold][b]...[/b][/gold] 上下文中时，[LocString 会被当成 BBCode 标签导致崩溃。
    /// </remarks>
    private static string SanitizeForBbcode(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "?";
        // 替换 ASCII 方括号为全角，保留可读性同时避免 BBCode 冲突
        var cleaned = raw
            .Replace('[', '［')
            .Replace(']', '］');
        return string.IsNullOrWhiteSpace(cleaned) ? "?" : cleaned.Trim();
    }

    private void SetStr(string key, string? val)
    {
        if (val == null || !DynamicVars.TryGetValue(key, out var dv) || dv is not StringVar sv) return;
        var t = sv.GetType();

        foreach (var name in new[] { "String", "StringValue", "BaseValue", "Value" })
        {
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.CanWrite != true) continue;
            if (prop.PropertyType == typeof(string))
            {
                prop.SetValue(sv, val);
                return;
            }
            if (prop.PropertyType == typeof(LocString))
            {
                prop.SetValue(sv, new LocString("", val));
                return;
            }
        }

        foreach (var name in new[] { "_value", "_string", "_locString", "value" })
        {
            var field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null) { field.SetValue(sv, val); return; }
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

        var relics = GetTradeableRelics(player);
        if (relics.Count == 0) return;

        _loseRelic1 = relics[Rnd.Next(relics.Count)];
        _gainRelic = RollRandomRelic();
        _enchant = RollRandomEnchant();

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
            list.Add(Opt(async () =>
            {
                await RelicCmd.Remove(l);
                var mutable = (RelicModel)g.MutableClone();
                await RelicCmd.Obtain(mutable, Owner!);
                AfterTrade();
            }, "OPT_1"));
        }

        // OPT_2: 遗物 → 附魔（≥2 遗物，玩家自选卡牌）
        if (relics.Count >= 2 && _loseRelic2 != null && _enchant != null)
        {
            var l = _loseRelic2; var e = _enchant;
            list.Add(Opt(async () =>
            {
                var mutableEnchant = (EnchantmentModel)e.MutableClone();
                var selected = await CardSelectCmd.FromDeckForEnchantment(Owner!, mutableEnchant, 1,
                    new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1));
                if (!selected.Any()) return; // 取消选择则不消耗遗物
                await RelicCmd.Remove(l);
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
        SetStr("LOSE_RELIC_1", ToText(_loseRelic1?.Title));
        SetStr("GAIN_RELIC", ToText(_gainRelic?.Title));
        SetStr("LOSE_RELIC_2", ToText(_loseRelic2?.Title));
        SetStr("ENCHANT_NAME", ToText(_enchant?.Title));

        var opts = BuildList();
        SetEventState(L10NLookup("pages.INITIAL.description"), opts);
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

    private static bool IsTradeable(RelicModel relic) =>
        typeof(RelicModel).GetProperty("Rarity")?.GetValue(relic)?.ToString() != "Starter"
        && relic.GetType().Name != "Circlet";

    private static List<RelicModel> GetTradeableRelics(Player? player) =>
        player?.Relics.Where(IsTradeable).ToList() ?? [];
}
