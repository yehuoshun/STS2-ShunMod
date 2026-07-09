using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShunMod.Core;

namespace ShunMod.Shun.Events;

/// <summary>
///     遗物交易所 — 自选模式。
///     先选要放弃的遗物，再从随机列出的遗物/附魔中自选想要的。
///     可反复交易直到退出。
/// </summary>
[EventPool]
public class ShunModRelicExchange : ShunEventModel
{
    // ═══════════════════════════════════════════════════════════
    //  静态数据
    // ═══════════════════════════════════════════════════════════

    private static readonly HashSet<RelicRarity> TradeableRarities =
        [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop, RelicRarity.None];

    /// <summary>附魔黑名单 — 不会在交易所随机出现。</summary>
    private static readonly HashSet<string> EnchantBlacklist = new()
    {
        "Adroit", "PerfectFit", "RoyallyApproved", "SlumberingEssence",
        "Sown", "Spiral", "Steady", "TezcatarasEmber", "Vigorous",
        "Swift", "Glam", "Clone", "Goopy", "Momentum", "Inky",
    };

    private static readonly Lazy<List<EnchantmentModel>> EnchantPoolCache =
        new(InitEnchantPool);

    // ═══════════════════════════════════════════════════════════
    //  状态
    // ═══════════════════════════════════════════════════════════

    private enum TradeState { Menu, SelectLose, SelectGain, SelectEnchant }
    private enum TradeMode { Relic, Enchant }

    private TradeState _state = TradeState.Menu;
    private TradeMode _mode = TradeMode.Relic;
    private RelicModel? _selectedLoseRelic;

    /// <summary>候选遗物交易池（3 个随机遗物选项）。</summary>
    private readonly List<RelicModel> _gainOptions = [];

    /// <summary>候选附魔交易池（2 个随机附魔选项）。</summary>
    private readonly List<EnchantmentModel> _enchantOptions = [];

    // ═══════════════════════════════════════════════════════════
    //  DynamicVars — 固定 10 个槽位，按需使用
    //  选遗物用 LOSE_1~LOSE_10，选增益用 GAIN_1~GAIN_5，选附魔用 ENCH_1~ENCH_5
    // ═══════════════════════════════════════════════════════════

    protected override IEnumerable<DynamicVar> CanonicalVars => BuildCanonicalVars();

    private static IEnumerable<DynamicVar> BuildCanonicalVars()
    {
        // 放弃遗物槽位 (10)
        for (var i = 1; i <= 10; i++) yield return new StringVar($"LOSE_{i}");
        // 增益遗物槽位 (5)
        for (var i = 1; i <= 5; i++) yield return new StringVar($"GAIN_{i}");
        // 附魔槽位 (5)
        for (var i = 1; i <= 5; i++) yield return new StringVar($"ENCH_{i}");
        // 放弃遗物名（附魔时的描述）
        yield return new StringVar("LOSE_RELIC");
    }

    // ═══════════════════════════════════════════════════════════
    //  事件生命周期
    // ═══════════════════════════════════════════════════════════

    public override void CalculateVars()
    {
        if (Owner == null) return;
        _state = TradeState.Menu;
        _selectedLoseRelic = null;
        RollGainOptions();
        ClearVars();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return BuildMenuOptions();
    }

    // ═══════════════════════════════════════════════════════════
    //  Roll 逻辑
    // ═══════════════════════════════════════════════════════════

    private void RollGainOptions()
    {
        _gainOptions.Clear();
        _gainOptions.AddRange(Enumerable.Range(0, 3)
            .Select(_ => RollRandomRelic())
            .OfType<RelicModel>()
            .Distinct()
            .Take(3));

        _enchantOptions.Clear();
        _enchantOptions.AddRange(RollRandomEnchants(2));
    }

    private static List<EnchantmentModel> RollRandomEnchants(int count)
    {
        var pool = GetEnchantPool();
        if (pool.Count == 0) return [];

        var rolled = new HashSet<string>();
        var result = new List<EnchantmentModel>();
        for (var i = 0; i < count; i++)
        {
            var ench = PickUnique(pool, rolled);
            if (ench != null) result.Add(ench);
        }
        return result;
    }

    private static EnchantmentModel? PickUnique(List<EnchantmentModel> pool, HashSet<string> rolled)
    {
        var chosen = default(EnchantmentModel);
        var count = 0;
        foreach (var e in pool)
        {
            if (rolled.Contains(e.GetType().FullName!)) continue;
            count++;
            if (Random.Shared.Next(count) == 0)
                chosen = e;
        }
        if (count == 0) return null;
        rolled.Add(chosen!.GetType().FullName!);
        return chosen;
    }

    private static List<EnchantmentModel> GetEnchantPool() => EnchantPoolCache.Value;

    private static List<EnchantmentModel> InitEnchantPool()
    {
        return typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t)
                                      && t.Name != "EnchantmentModel"
                                      && t.Name != "DeprecatedEnchantment"
                                      && t.Name != "MockFreeEnchantment"
                                      && !EnchantBlacklist.Contains(t.Name))
            .Select(t => ModelDb.GetByIdOrNull<EnchantmentModel>(ModelDb.GetId(t)))
            .OfType<EnchantmentModel>()
            .ToList();
    }

    private static RelicModel? RollRandomRelic()
    {
        IEnumerable<RelicModel> all;
        try { all = ModelDb.AllRelics; }
        catch (InvalidOperationException ex)
        {
            Log.Warn($"[ShunMod_Shun] ModelDb.AllRelics 不可用，回退到反射枚举: {ex.Message}");
            all = typeof(RelicModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(RelicModel).IsAssignableFrom(t))
                .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t)))
                .OfType<RelicModel>();
        }
        var pool = all.Where(IsTradeable).ToList();
        return pool.Count > 0 ? pool[Random.Shared.Next(pool.Count)] : null;
    }

    private static bool IsTradeable(RelicModel r) => TradeableRarities.Contains(r.Rarity);

    // ═══════════════════════════════════════════════════════════
    //  DynamicVar 辅助
    // ═══════════════════════════════════════════════════════════

    private void ClearVars()
    {
        for (var i = 1; i <= 10; i++) SetVar($"LOSE_{i}", "");
        for (var i = 1; i <= 5; i++) SetVar($"GAIN_{i}", "");
        for (var i = 1; i <= 5; i++) SetVar($"ENCH_{i}", "");
        SetVar("LOSE_RELIC", "");
    }

    private void SetVar(string key, string val)
    {
        DynamicVarHelper.SetStrValue(DynamicVars, key, val);
    }

    private static string Str(LocString? loc)
    {
        if (loc == null) return "";
        try
        {
            return loc.GetRawText() ?? loc.GetFormattedText() ?? "";
        }
        catch (LocException)
        {
            return "";
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  主菜单选项
    // ═══════════════════════════════════════════════════════════

    private IReadOnlyList<EventOption> BuildMenuOptions()
    {
        var player = Owner;
        var list = new List<EventOption>();
        var hasTradeable = player != null && player.Relics.Any(IsTradeable);

        if (!hasTradeable)
        {
            list.Add(BuildLeaveOption());
            return list;
        }

        // 遗物换遗物
        if (_gainOptions.Count > 0)
        {
            list.Add(MenuOpt(async () =>
            {
                _state = TradeState.SelectLose;
                _mode = TradeMode.Relic;
                ShowSelectLose();
            }, "OPT_TRADE_RELIC"));
        }

        // 遗物换附魔
        if (_enchantOptions.Count > 0)
        {
            list.Add(MenuOpt(async () =>
            {
                _state = TradeState.SelectLose;
                _mode = TradeMode.Enchant;
                ShowSelectLose();
            }, "OPT_TRADE_ENCHANT"));
        }

        // 扣血刷新
        if (player != null)
        {
            list.Add(MenuOpt(async () =>
            {
                player.Creature.LoseHpInternal(5, 0);
                RollGainOptions();
                ShowMenu();
            }, "OPT_REFRESH"));
        }

        // 离开
        list.Add(BuildLeaveOption());

        return list;
    }

    // ═══════════════════════════════════════════════════════════
    //  选放弃遗物
    // ═══════════════════════════════════════════════════════════

    private void ShowSelectLose()
    {
        var player = Owner;
        if (player == null) return;

        var available = player.Relics.Where(IsTradeable).ToList();
        if (available.Count == 0) { ShowMenu(); return; }

        // 设置 DynamicVars
        var count = Math.Min(available.Count, 10);
        for (var i = 0; i < count; i++)
            SetVar($"LOSE_{i + 1}", Str(available[i].Title));

        // 可选遗物不够 10 个时，空槽位显示空
        for (var i = count + 1; i <= 10; i++)
            SetVar($"LOSE_{i}", "");

        var options = new List<EventOption>();
        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var relic = available[i];
            var tips = ShunModHelper.SafeRelicHoverTips(relic);

            options.Add(new EventOption(this, async () =>
            {
                _selectedLoseRelic = relic;
                if (_mode == TradeMode.Relic) ShowSelectGain();
                else ShowSelectEnchant();
            }, $"{Id.Entry}.pages.SELECT_LOSE.options.OPT_LOSE_{idx + 1}", tips));
        }

        SetEventState(L10NLookup("pages.SELECT_LOSE.description"), options);
    }

    // ═══════════════════════════════════════════════════════════
    //  选获得的遗物
    // ═══════════════════════════════════════════════════════════

    private void ShowSelectGain()
    {
        var player = Owner;
        if (player == null || _gainOptions.Count == 0 || _selectedLoseRelic == null)
        { ShowMenu(); return; }

        var count = Math.Min(_gainOptions.Count, 5);
        for (var i = 0; i < count; i++)
            SetVar($"GAIN_{i + 1}", Str(_gainOptions[i].Title));
        for (var i = count + 1; i <= 5; i++)
            SetVar($"GAIN_{i}", "");

        var options = new List<EventOption>();
        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var gain = _gainOptions[i];
            var lose = _selectedLoseRelic;
            var mutableGain = (RelicModel)gain.MutableClone();
            var tips = new List<IHoverTip>();
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(lose));
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(mutableGain));

            options.Add(new EventOption(this, async () =>
            {
                await RelicCmd.Remove(lose);
                await RelicCmd.Obtain(mutableGain, player);
                _state = TradeState.Menu;
                _selectedLoseRelic = null;
                RollGainOptions();
                ShowMenu();
            }, $"{Id.Entry}.pages.SELECT_GAIN.options.OPT_GAIN_{idx + 1}", tips));
        }

        // 返回按钮
        options.Add(MenuOpt(() =>
        {
            _state = TradeState.Menu;
            _selectedLoseRelic = null;
            ShowMenu();
        }, "OPT_BACK"));

        SetEventState(L10NLookup("pages.SELECT_GAIN.description"), options);
    }

    // ═══════════════════════════════════════════════════════════
    //  选附魔
    // ═══════════════════════════════════════════════════════════

    private void ShowSelectEnchant()
    {
        var player = Owner;
        if (player == null || _enchantOptions.Count == 0 || _selectedLoseRelic == null)
        { ShowMenu(); return; }

        var count = Math.Min(_enchantOptions.Count, 5);
        for (var i = 0; i < count; i++)
            SetVar($"ENCH_{i + 1}", Str(_enchantOptions[i].Title));
        for (var i = count + 1; i <= 5; i++)
            SetVar($"ENCH_{i}", "");

        SetVar("LOSE_RELIC", Str(_selectedLoseRelic.Title));

        var options = new List<EventOption>();
        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var ench = _enchantOptions[i];
            var lose = _selectedLoseRelic;
            var mutableEnch = (EnchantmentModel)ench.MutableClone();
            mutableEnch.Amount = 5;
            var tips = new List<IHoverTip>();
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(lose));
            tips.AddRange(mutableEnch.HoverTips);

            options.Add(new EventOption(this, async () =>
            {
                var deck = player!.Deck.Cards;
                if (!deck.Any(c => mutableEnch.CanEnchant(c)))
                {
                    ShowSelectEnchant();
                    return;
                }

                var picked = await CardSelectCmd.FromDeckGeneric(player,
                    new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1, 1),
                    c => mutableEnch.CanEnchant(c));
                if (!picked.Any())
                {
                    ShowSelectEnchant();
                    return;
                }

                var card = picked.First();
                CardCmd.Enchant(mutableEnch, card, 5);
                await RelicCmd.Remove(lose);
                _state = TradeState.Menu;
                _selectedLoseRelic = null;
                RollGainOptions();
                ShowMenu();
            }, $"{Id.Entry}.pages.SELECT_ENCHANT.options.OPT_ENCH_{idx + 1}", tips));
        }

        // 返回按钮
        options.Add(MenuOpt(() =>
        {
            _state = TradeState.Menu;
            _selectedLoseRelic = null;
            ShowMenu();
        }, "OPT_BACK"));

        SetEventState(L10NLookup("pages.SELECT_ENCHANT.description"), options);
    }

    // ═══════════════════════════════════════════════════════════
    //  辅助
    // ═══════════════════════════════════════════════════════════

    private void ShowMenu()
    {
        SetEventState(L10NLookup("pages.MENU.description"), BuildMenuOptions());
    }

    private EventOption MenuOpt(Func<Task> cb, string key)
    {
        return new EventOption(this, cb, $"{Id.Entry}.pages.MENU.options.{key}");
    }

    private EventOption BuildLeaveOption()
    {
        return new EventOption(this, async () =>
                SetEventFinished(L10NLookup("pages.CLOSE.description")),
            $"{Id.Entry}.pages.MENU.options.OPT_LEAVE");
    }
}