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
using STS2ShunMod.STS2_ShunModCode.Core;

namespace STS2ShunMod.STS2_ShunModCode.Events.Shun;

/// <summary>
///     遗物交易所 — 随机遗物换遗物/附魔，可反复交易直到退出。
///     ①随机遗物换随机遗物 ②随机遗物换卡牌附魔 ③扣5HP刷新 ④退出
/// </summary>
[EventPool]
public class ShunModRelicExchange : ShunEventModel
{
    // ═══════════════════════════════════════════════════════════
    //  静态数据
    // ═══════════════════════════════════════════════════════════

    // 使用 Random.Shared 而非 new Random() 实例。
    // .NET 9 的 Random.Shared 是线程安全的共享 RNG 实例，
    // 避免每个类/模块创建独立的 Random 对象。
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
    //  实例状态
    // ═══════════════════════════════════════════════════════════

    private RelicModel? _loseRelic1;
    private RelicModel? _loseRelic2;
    private RelicModel? _gainRelic;
    private EnchantmentModel? _enchantA;
    private EnchantmentModel? _enchantB;

    // ═══════════════════════════════════════════════════════════
    //  DynamicVars
    // ═══════════════════════════════════════════════════════════

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new StringVar("LOSE_RELIC_1"),
        new StringVar("GAIN_RELIC"),
        new StringVar("LOSE_RELIC_2"),
        new StringVar("ENCHANT_NAME_A"),
        new StringVar("ENCHANT_NAME_B"),
    ];

    // ═══════════════════════════════════════════════════════════
    //  事件生命周期
    // ═══════════════════════════════════════════════════════════

    public override void CalculateVars()
    {
        if (Owner == null) return;
        Roll();
        SyncVars();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return BuildOptions();
    }

    // ═══════════════════════════════════════════════════════════
    //  Sync & Resolve
    // ═══════════════════════════════════════════════════════════

    private void SyncVars()
    {
        DynamicVarHelper.SetStrValue(DynamicVars, "LOSE_RELIC_1", Resolve(_loseRelic1?.Title));
        DynamicVarHelper.SetStrValue(DynamicVars, "GAIN_RELIC", Resolve(_gainRelic?.Title));
        DynamicVarHelper.SetStrValue(DynamicVars, "LOSE_RELIC_2", Resolve(_loseRelic2?.Title));
        DynamicVarHelper.SetStrValue(DynamicVars, "ENCHANT_NAME_A", Resolve(_enchantA?.Title));
        DynamicVarHelper.SetStrValue(DynamicVars, "ENCHANT_NAME_B", Resolve(_enchantB?.Title));
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

    // ═══════════════════════════════════════════════════════════
    //  Roll 逻辑
    // ═══════════════════════════════════════════════════════════

    private void Roll()
    {
        _loseRelic1 = _loseRelic2 = _gainRelic = null;
        _enchantA = _enchantB = null;

        var player = Owner!;
        var available = player.Relics.Where(IsTradeable).ToList();
        if (available.Count == 0) return;

        _loseRelic1 = PreferCirclet(available);
        _gainRelic = RollRandomRelic();

        if (available.Count >= 2)
        {
            var remaining = new List<RelicModel>(available);
            remaining.Remove(_loseRelic1);
            _loseRelic2 = PreferCirclet(remaining);
        }

        RollThreeEnchants();
    }

    private void RollThreeEnchants()
    {
        var pool = GetEnchantPool();
        if (pool.Count == 0) return;

        var rolled = new HashSet<string>();
        _enchantA = PickUnique(pool, rolled);
        _enchantB = PickUnique(pool, rolled);
    }

    /// <summary>
    ///     从附魔池中随机选一个未被 roll 过的附魔。
    ///     先过滤出所有候选，再随机选一个，选中后标记已选 — 过滤和副作用互不干扰。
    /// </summary>
    private static EnchantmentModel? PickUnique(List<EnchantmentModel> pool, HashSet<string> rolled)
    {
        var available = pool.Where(e => !rolled.Contains(e.GetType().FullName!)).ToArray();
        if (available.Length == 0) return null;
        var chosen = available[Random.Shared.Next(available.Length)];
        rolled.Add(chosen.GetType().FullName!);
        return chosen;
    }

    /// <summary>获取附魔池（首次访问时执行一次全量扫描，之后返回缓存结果）。</summary>
    private static List<EnchantmentModel> GetEnchantPool() => EnchantPoolCache.Value;

    /// <summary>首次初始化附魔池：全量反射扫描过滤 + ModelDb 解析。</summary>
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
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            Log.Warn($"[STS2_ShunMod] ModelDb.AllRelics 不可用，回退到反射枚举: {ex.GetType().Name}: {ex.Message}");
            all = typeof(RelicModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(RelicModel).IsAssignableFrom(t))
                .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t)))
                .OfType<RelicModel>();
        }
        var pool = all.Where(IsTradeable).ToList();
        return pool.Count > 0 ? pool[Random.Shared.Next(pool.Count)] : null;
    }

    private static bool IsTradeable(RelicModel r) => TradeableRarities.Contains(r.Rarity);

    private static RelicModel PreferCirclet(List<RelicModel> available)
    {
        var circlet = available.FirstOrDefault(r => r.GetType().Name == "Circlet");
        return circlet ?? available[Random.Shared.Next(available.Count)];
    }

    // ═══════════════════════════════════════════════════════════
    //  选项构建
    // ═══════════════════════════════════════════════════════════

    private IReadOnlyList<EventOption> BuildOptions()
    {
        var player = Owner;
        var list = new List<EventOption>();

        // OPT_1: 遗物 → 遗物
        if (_loseRelic1 != null && _gainRelic != null)
        {
            var lose = _loseRelic1;
            var gain = _gainRelic;
            var tips = new List<IHoverTip>();
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(lose));
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(gain));
            list.Add(Opt(async () =>
            {
                await RelicCmd.Remove(lose);
                await RelicCmd.Obtain((RelicModel)gain.MutableClone(), player!);
                Refresh();
            }, "OPT_1", tips));
        }

        // OPT_2A/2B: 遗物 → 附魔
        if (_loseRelic2 != null)
        {
            var lose = _loseRelic2;
            var deck = player!.Deck.Cards;
            foreach (var (ench, key) in new[]
                         { (_enchantA, "OPT_2A"), (_enchantB, "OPT_2B") })
            {
                if (ench == null) continue;
                if (!deck.Any(c => ench.CanEnchant(c))) continue;

                var mutableEnch = (EnchantmentModel)ench.MutableClone();
                mutableEnch.Amount = 5;
                var tips = new List<IHoverTip>();
                tips.AddRange(ShunModHelper.SafeRelicHoverTips(lose));
                tips.AddRange(mutableEnch.HoverTips);
                list.Add(Opt(async () =>
                {
                    var picked = await CardSelectCmd.FromDeckGeneric(player!,
                        new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1, 1),
                        c => mutableEnch.CanEnchant(c));
                    if (!picked.Any()) { Refresh(); return; }
                    var card = picked.First();
                    CardCmd.Enchant(mutableEnch, card, 5);
                    await RelicCmd.Remove(lose);
                    Refresh();
                }, key, tips));
            }
        }

        // OPT_3: 扣血刷新
        if (player != null && player.Relics.Any(IsTradeable))
            list.Add(Opt(async () =>
            {
                player.Creature.LoseHpInternal(5, 0);
                Refresh();
            }, "OPT_3"));

        // OPT_4: 离开
        list.Add(new EventOption(this, async () =>
                SetEventFinished(L10NLookup("pages.CLOSE.description")),
            $"{Id.Entry}.pages.INITIAL.options.OPT_4"));

        return list;
    }

    private EventOption Opt(Func<Task> cb, string key, IEnumerable<IHoverTip>? hoverTips = null)
    {
        return new EventOption(this, cb, $"{Id.Entry}.pages.INITIAL.options.{key}",
            hoverTips ?? Array.Empty<IHoverTip>());
    }

    private void Refresh()
    {
        Roll();
        SyncVars();
        SetEventState(L10NLookup("pages.INITIAL.description"), BuildOptions());
    }
}