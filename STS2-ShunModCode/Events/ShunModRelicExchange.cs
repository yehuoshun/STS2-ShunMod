using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
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
    private static readonly HashSet<RelicRarity> TradeableRarities =
        [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop];

    // ════════════════════════════════════ 状态 ════════════════════════════════════

    private RelicModel? _loseRelic1;
    private RelicModel? _gainRelic;
    private RelicModel? _loseRelic2;
    private EnchantmentModel? _enchant;

    // ════════════════════════════════════ 背景图 ════════════════════════════════════

    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        var paths = base.GetAssetPaths(runState).ToList();
        var modPath = $"res://STS2-ShunMod/images/events/{Id.Entry.ToLowerInvariant()}.png";
        var defaultPath = ImageHelper.GetImagePath($"events/{Id.Entry.ToLowerInvariant()}.png");
        var i = paths.IndexOf(defaultPath);
        if (i >= 0) paths[i] = modPath;
        else paths.Add(modPath);
        return paths;
    }

    // ════════════════════════════════════ DynamicVars ════════════════════════════════════

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
        Roll();
        SyncVars();
    }

    private void SyncVars()
    {
        SetStr("LOSE_RELIC_1", Resolve(_loseRelic1?.Title));
        SetStr("GAIN_RELIC", Resolve(_gainRelic?.Title));
        SetStr("LOSE_RELIC_2", Resolve(_loseRelic2?.Title));
        SetStr("ENCHANT_NAME", Resolve(_enchant?.Title));
    }

    // ════════════════════════════════════ LocString 解析 ════════════════════════════════════

    private static string Resolve(LocString? loc)
    {
        if (loc == null) return "?";
        try { return loc.GetRawText(); }
        catch (LocException) { }
        try { return loc.GetFormattedText(); }
        catch (LocException) { }
        return "?";
    }

    private void SetStr(string key, string val)
    {
        if (!DynamicVars.TryGetValue(key, out var dv) || dv is not StringVar sv) return;
        foreach (var name in new[] { "String", "StringValue", "BaseValue", "Value" })
        {
            var prop = sv.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.CanWrite == true && prop.PropertyType == typeof(string))
            {
                prop.SetValue(sv, val);
                return;
            }
        }
    }

    // ════════════════════════════════════ 随机 ════════════════════════════════════

    private void Roll()
    {
        _loseRelic1 = _loseRelic2 = _gainRelic = null;
        _enchant = null;

        var player = Owner!;
        var available = player.Relics.Where(IsTradeable).ToList();
        if (available.Count == 0) return;

        _loseRelic1 = available[Rnd.Next(available.Count)];
        _gainRelic = RollRandomRelic();
        _enchant = RollRandomEnchant();

        if (available.Count >= 2)
            do { _loseRelic2 = available[Rnd.Next(available.Count)]; }
            while (_loseRelic2 == _loseRelic1);
    }

    private static RelicModel? RollRandomRelic()
    {
        IEnumerable<RelicModel> all;
        try { all = ModelDb.AllRelics; }
        catch
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
        var pool = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t) && t.Name != "EnchantmentModel")
            .Select(t => ModelDb.GetByIdOrNull<EnchantmentModel>(ModelDb.GetId(t)))
            .OfType<EnchantmentModel>().ToList();
        return pool.Count > 0 ? pool[Rnd.Next(pool.Count)] : null;
    }

    private static bool IsTradeable(RelicModel r) =>
        TradeableRarities.Contains(r.Rarity) && r.GetType().Name != "Circlet";

    // ════════════════════════════════════ 选项 ════════════════════════════════════

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => BuildOptions();

    private IReadOnlyList<EventOption> BuildOptions()
    {
        var player = Owner;
        var list = new List<EventOption>();

        // OPT_1: 遗物 → 遗物
        if (_loseRelic1 != null && _gainRelic != null)
        {
            var lose = _loseRelic1; var gain = _gainRelic;
            list.Add(Opt(async () =>
            {
                await RelicCmd.Remove(lose);
                await RelicCmd.Obtain((RelicModel)gain.MutableClone(), player!);
                Refresh();
            }, "OPT_1"));
        }

        // OPT_2: 遗物 → 附魔（玩家自选卡牌）
        if (_loseRelic2 != null && _enchant != null)
        {
            var lose = _loseRelic2; var ench = _enchant;
            list.Add(Opt(async () =>
            {
                var picked = await CardSelectCmd.FromDeckGeneric(player!,
                    new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1, 1),
                    filter: c => ench.CanEnchant(c));
                if (!picked.Any()) return; // 取消不扣遗物
                var mutableEnch = (EnchantmentModel)ench.MutableClone();
                var card = picked.First();
                // 走 CardCmd.Enchant → InfiniteEnchant patch → AllEnchantments 追踪
                CardCmd.Enchant(mutableEnch, card, 1);
                await RelicCmd.Remove(lose);
                Refresh();
            }, "OPT_2"));
        }

        // OPT_3: 扣血刷新
        if (player != null && player.Relics.Any(IsTradeable))
        {
            list.Add(Opt(async () =>
            {
                player.Creature.LoseHpInternal(5, 0);
                Refresh();
            }, "OPT_3"));
        }

        // OPT_4: 离开
        list.Add(new EventOption(this, async () =>
            SetEventFinished(L10NLookup("pages.CLOSE.description")),
            $"{Id.Entry}.pages.INITIAL.options.OPT_4"));

        return list;
    }

    private EventOption Opt(Func<Task> cb, string key) =>
        new(this, cb, $"{Id.Entry}.pages.INITIAL.options.{key}");

    /// <summary>交易/刷新后：重新 Roll → 写 DynamicVars → SetEventState</summary>
    private void Refresh()
    {
        Roll();
        SyncVars();
        SetEventState(L10NLookup("pages.INITIAL.description"), BuildOptions());
    }
}
