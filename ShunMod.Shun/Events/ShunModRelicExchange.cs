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
    //  状态机
    // ═══════════════════════════════════════════════════════════

    private enum TradeState { Menu, SelectLose, SelectGain, SelectEnchant }
    private enum TradeMode { Relic, Enchant }

    private TradeState _state = TradeState.Menu;
    private TradeMode _mode = TradeMode.Relic;
    private RelicModel? _selectedLoseRelic;

    // ═══════════════════════════════════════════════════════════
    //  随机选项池（每次 Menu 刷新时重新生成）
    // ═══════════════════════════════════════════════════════════

    private List<RelicModel> _gainOptions = [];
    private List<EnchantmentModel> _enchantOptions = [];

    // ═══════════════════════════════════════════════════════════
    //  DynamicVars
    // ═══════════════════════════════════════════════════════════

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    // ═══════════════════════════════════════════════════════════
    //  事件生命周期
    // ═══════════════════════════════════════════════════════════

    public override void CalculateVars()
    {
        if (Owner == null) return;
        _state = TradeState.Menu;
        RollGainOptions();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return BuildMenuOptions();
    }

    // ═══════════════════════════════════════════════════════════
    //  Roll 逻辑
    // ═══════════════════════════════════════════════════════════

    /// <summary>刷新候选遗物/附魔池（每次 Menu 刷新时调用）。</summary>
    private void RollGainOptions()
    {
        _gainOptions = Enumerable.Range(0, 3)
            .Select(_ => RollRandomRelic())
            .OfType<RelicModel>()
            .Distinct()
            .Take(3)
            .ToList();

        _enchantOptions = RollRandomEnchants(2);
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
    //  选项构建
    // ═══════════════════════════════════════════════════════════

    /// <summary>主菜单 — 可选：遗物换遗物 / 遗物换附魔 / 扣血刷新 / 离开</summary>
    private IReadOnlyList<EventOption> BuildMenuOptions()
    {
        var player = Owner;
        var list = new List<EventOption>();
        var hasTradeable = player != null && player.Relics.Any(IsTradeable);

        if (!hasTradeable)
        {
            // 没有可交易的遗物，只能离开
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

    /// <summary>选择要放弃的遗物 — 每个可交易遗物一个选项。</summary>
    private void ShowSelectLose()
    {
        var player = Owner;
        if (player == null) return;

        var available = player.Relics.Where(IsTradeable).ToList();
        if (available.Count == 0)
        {
            ShowMenu();
            return;
        }

        var options = available.Select<RelicModel, EventOption>(relic =>
        {
            var lose = relic;
            var tips = ShunModHelper.SafeRelicHoverTips(lose);
            var title = BuildLocString($"失去 {Resolve(lose.Title)}");

            return new EventOption(this, async () =>
            {
                _selectedLoseRelic = lose;
                if (_mode == TradeMode.Relic)
                {
                    _state = TradeState.SelectGain;
                    ShowSelectGain();
                }
                else
                {
                    _state = TradeState.SelectEnchant;
                    ShowSelectEnchant();
                }
            }, title, tips);
        }).ToList();

        SetEventState(L10NLookup("pages.SELECT_LOSE.description"), options);
    }

    /// <summary>选择要获得的遗物 — 3 个随机遗物选项。</summary>
    private void ShowSelectGain()
    {
        var player = Owner;
        if (player == null || _gainOptions.Count == 0 || _selectedLoseRelic == null)
        {
            ShowMenu();
            return;
        }

        var options = _gainOptions.Select<RelicModel, EventOption>((gain, _) =>
        {
            var lose = _selectedLoseRelic!;
            var mutableGain = (RelicModel)gain.MutableClone();
            var tips = new List<IHoverTip>();
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(lose));
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(mutableGain));
            var title = BuildLocString($"获得 {Resolve(mutableGain.Title)}");

            return new EventOption(this, async () =>
            {
                await RelicCmd.Remove(lose);
                await RelicCmd.Obtain(mutableGain, player);
                _state = TradeState.Menu;
                _selectedLoseRelic = null;
                RollGainOptions();
                ShowMenu();
            }, title, tips);
        }).ToList();

        // 返回按钮
        options.Add(MenuOpt(() =>
        {
            _state = TradeState.Menu;
            _selectedLoseRelic = null;
            ShowMenu();
        }, "OPT_BACK"));

        SetEventState(L10NLookup("pages.SELECT_GAIN.description"), options);
    }

    /// <summary>选择附魔 — 先选附魔，再选卡牌。</summary>
    private void ShowSelectEnchant()
    {
        var player = Owner;
        if (player == null || _enchantOptions.Count == 0 || _selectedLoseRelic == null)
        {
            ShowMenu();
            return;
        }

        var options = _enchantOptions.Select<EnchantmentModel, EventOption>(ench =>
        {
            var lose = _selectedLoseRelic!;
            var mutableEnch = (EnchantmentModel)ench.MutableClone();
            mutableEnch.Amount = 5;
            var tips = new List<IHoverTip>();
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(lose));
            tips.AddRange(mutableEnch.HoverTips);
            var title = BuildLocString($"附魔「{Resolve(mutableEnch.Title)}」");

            return new EventOption(this, async () =>
            {
                var deck = player!.Deck.Cards;
                if (!deck.Any(c => mutableEnch.CanEnchant(c)))
                {
                    // 没有可附魔的卡牌，回到选择附魔
                    ShowSelectEnchant();
                    return;
                }

                var picked = await CardSelectCmd.FromDeckGeneric(player,
                    new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1, 1),
                    c => mutableEnch.CanEnchant(c));
                if (!picked.Any())
                {
                    // 取消选择，回到选择附魔
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
            }, title, tips);
        }).ToList();

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
    //  辅助方法
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

    /// <summary>从原始字符串构建 LocString（用于动态显示遗物/附魔名）。</summary>
    private static LocString BuildLocString(string rawText)
    {
        // LocString 具有 FromRaw 工厂方法，从原始字符串创建
        return LocString.FromRaw(rawText);
    }

    private static string Resolve(LocString? loc)
    {
        if (loc == null) return "?";
        try
        {
            return loc.GetRawText() ?? loc.GetFormattedText() ?? "?";
        }
        catch (LocException)
        {
            return "?";
        }
    }
}