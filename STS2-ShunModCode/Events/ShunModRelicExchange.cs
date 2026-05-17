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
///     参照 skill 标准 EventModel 模式。
/// </summary>
public class ShunModRelicExchange : EventModel
{
    // ════════════════════════════════════════════════════
    // 背景图
    // ════════════════════════════════════════════════════

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
    // 状态
    // ════════════════════════════════════════════════════

    private static readonly Random Rnd = new();
    private RelicModel? _loseRelic1;
    private RelicModel? _gainRelic;
    private RelicModel? _loseRelic2;
    private EnchantmentModel? _enchantment;
    private CardModel? _enchantTarget;

    // ════════════════════════════════════════════════════
    // DynamicVars（变量名与本地化文件对齐）
    // ════════════════════════════════════════════════════

    private static readonly LocString EmptyLoc = new("");

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new StringVar("LOSE_RELIC_1", EmptyLoc),
        new StringVar("GAIN_RELIC", EmptyLoc),
        new StringVar("LOSE_RELIC_2", EmptyLoc),
        new StringVar("ENCHANT_NAME", EmptyLoc),
        new StringVar("CARD_NAME", EmptyLoc)
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
        ((StringVar)DynamicVars["LOSE_RELIC_1"]).Value = _loseRelic1?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["GAIN_RELIC"]).Value = _gainRelic?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["LOSE_RELIC_2"]).Value = _loseRelic2?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["ENCHANT_NAME"]).Value = _enchantment?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["CARD_NAME"]).Value = _enchantTarget?.Title ?? EmptyLoc;
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

        // OPT_2 用另一个遗物
        if (relics.Count >= 2)
        {
            do { _loseRelic2 = relics[Rnd.Next(relics.Count)]; }
            while (_loseRelic2 == _loseRelic1);
        }

        _enchantment = RollRandomEnchant();
        _enchantTarget = RollRandomCard(player);
    }

    // ════════════════════════════════════════════════════
    // 选项生成
    // ════════════════════════════════════════════════════

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return BuildOptionList();
    }

    private string OptKey(string key) => $"{Id.Entry}.pages.INITIAL.options.{key}";

    /// <summary>
    ///     构建当前状态下的选项列表。
    ///     被 GenerateInitialOptions 和 AfterTrade → SetEventState 共用。
    /// </summary>
    private IReadOnlyList<EventOption> BuildOptionList()
    {
        var player = Owner;
        if (player == null) return [MakeExitOption()];

        var relics = GetTradeableRelics(player);
        var list = new List<EventOption>();

        // OPT_1: 遗物 → 遗物
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

        // OPT_2: 遗物 → 附魔（至少 2 遗物才显示，不和 OPT_1 抢）
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

        // OPT_3: 扣血刷新
        if (relics.Count > 0)
        {
            list.Add(new EventOption(this, async () =>
            {
                await DamagePlayer(Owner!, 5);
                AfterTrade();
            }, OptKey("OPT_3")));
        }

        // OPT_4: 退出
        list.Add(MakeExitOption());
        return list;
    }

    /// <summary>
    ///     交易/刷新后：重新 Roll → 更新 DynamicVars → SetEventState 刷新页面。
    ///     参照 skill 多页选项模式。
    /// </summary>
    private void AfterTrade()
    {
        var player = Owner!;
        Roll(player);
        ApplyVars();
        SetEventState(BuildOptionList());
    }

    private EventOption MakeExitOption()
    {
        return new EventOption(this, () => SetEventFinished(L10NLookup("pages.CLOSE.description")),
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
            // 触发 RelicRemoved 事件
            TryInvokeEvent(player, "RelicRemoved", relic);
        }
    }

    private static void TryGiveRelic(Player player, RelicModel relic)
    {
        var method = typeof(Player).GetMethod("AddRelicInternal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(player, [relic]);
    }

    private static void TryInvokeEvent(object target, string eventName, params object[] args)
    {
        var evt = target.GetType().GetEvent(eventName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (evt == null) return;

        // 获取 backing field
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
        // 方案 1: ChangeHp（负数 = 扣血）
        var m = typeof(Creature).GetMethod("ChangeHp",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(decimal)]);
        if (m != null) { m.Invoke(c, [(decimal)-amount]); await Task.CompletedTask; return; }

        // 方案 2: TakeDamage 反射
        m = typeof(Creature).GetMethod("TakeDamage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (m != null) { m.Invoke(c, [(decimal)amount]); await Task.CompletedTask; return; }

        // 方案 3: 直接扣血
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
        if (p?.GetValue(null) is IEnumerable<RelicModel> relics)
        {
            var pool = relics.Where(IsTradeable).ToList();
            return pool.Count > 0 ? pool[Rnd.Next(pool.Count)] : null;
        }

        // 回退：反射扫描
        var types = typeof(RelicModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(RelicModel).IsAssignableFrom(t));
        var valid = types
            .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t)))
            .OfType<RelicModel>()
            .Where(IsTradeable)
            .ToList();
        return valid.Count > 0 ? valid[Rnd.Next(valid.Count)] : null;
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
        // 反射读 Rarity，避免 RelicRarity 枚举的编译时依赖
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
