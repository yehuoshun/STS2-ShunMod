using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Events;

/// <summary>
/// 遗物交易所 — 用遗物换遗物、换附魔、或消耗生命刷新选项。
/// </summary>
/// <remarks>
/// 三个选项：
/// <list type="number">
/// <item>随机一个玩家遗物 → 随机一个新遗物（从遗物池抽取）</item>
/// <item>随机一个玩家遗物 → 卡牌附魔（随机附魔类型，目标随机牌组中一张）</item>
/// <item>扣除 5 生命 → 刷新以上两个选项的随机结果</item>
/// </list>
/// </remarks>
[Pool(typeof(EventRelicPool))]
public class ShunModRelicExchange : EventModel
{
    private static readonly Random Rng = new();

    // ── 当前随机结果（每次生成选项时更新） ──
    private RelicModel? _playerRelic1;
    private RelicModel? _rewardRelic;
    private RelicModel? _playerRelic2;
    private EnchantmentModel? _rewardEnchant;
    private CardModel? _enchantTargetCard;

    public override IEnumerable<EventOption> GenerateInitialOptions(PlayerChoiceContext ctx)
    {
        var player = Owner;
        var playerRelics = GetPlayerRelics(player);

        RollOptions(player, playerRelics);

        var options = new List<EventOption>();

        // ── 选项 1：遗物换遗物 ──
        if (_playerRelic1 != null && _rewardRelic != null)
        {
            // TODO: 本地化 key 需要在 localization/events.json 中添加
            // SHUN_MOD_RELIC_EXCHANGE.pages.INITIAL.options.OPT_1
            //   参数: {sacrificeName} {rewardName}
            options.Add(new EventOption(ctx, InitialOptionKey("OPT_1"), async () =>
            {
                RelicHelper.RemoveRelic(Owner, _playerRelic1!);
                // TODO: 确认 PlayerCmd.GainRelic 签名
                await PlayerCmd.GainRelic(Owner, _rewardRelic!);
                SetEventFinished(L10NLookup("pages.CLOSE.description"));
            }));
        }

        // ── 选项 2：遗物换附魔 ──
        if (_playerRelic2 != null && _rewardEnchant != null && _enchantTargetCard != null)
        {
            options.Add(new EventOption(ctx, InitialOptionKey("OPT_2"), async () =>
            {
                RelicHelper.RemoveRelic(Owner, _playerRelic2!);
                CardCmd.Enchant(_enchantTargetCard, _rewardEnchant!, 1);
                SetEventFinished(L10NLookup("pages.CLOSE.description"));
            }));
        }

        // ── 选项 3：消耗 5 HP 刷新 ──
        options.Add(new EventOption(ctx, InitialOptionKey("OPT_3"), async () =>
        {
            await PlayerCmd.Damage(Owner, 5);
            // 不调 SetEventFinished → 系统重新 GenerateInitialOptions 实现刷新
        }));

        return options;
    }

    // ════════════════════════════════════════════════════════
    // 内部辅助
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// 随机生成选项内容。
    /// </summary>
    private void RollOptions(Player player, IReadOnlyList<RelicModel> playerRelics)
    {
        if (playerRelics.Count == 0)
            return;

        // 选项 1：随机玩家遗物 → 随机遗物池遗物
        _playerRelic1 = playerRelics[Rng.Next(playerRelics.Count)];
        _rewardRelic = RollRelicFromPool();

        // 选项 2：随机玩家遗物 → 随机附魔给随机卡牌
        // 尽量不和选项 1 抽到同一个遗物
        if (playerRelics.Count > 1)
        {
            RelicModel pick2;
            do { pick2 = playerRelics[Rng.Next(playerRelics.Count)]; }
            while (pick2 == _playerRelic1);
            _playerRelic2 = pick2;
        }
        else
        {
            _playerRelic2 = _playerRelic1;
        }

        _rewardEnchant = RollEnchantment();
        _enchantTargetCard = RollCardFromDeck(player);
    }

    /// <summary>
    /// 从共享遗物池随机抽取一个遗物。
    /// </summary>
    private static RelicModel? RollRelicFromPool()
    {
        // 通过反射获取 RelicPoolModel 中注册的遗物类型
        // SharedRelicPool 在游戏内是单例，通过 ModelDb 获取
        var sharedPool = ModelDb.GetByIdOrNull<RelicPoolModel>(
            ModelDb.GetId(typeof(SharedRelicPool)));
        if (sharedPool == null)
            return null;

        // RelicPoolModel 内部存储条目列表，通过反射读取
        var entriesField = typeof(RelicPoolModel).GetField(
            "_entries", BindingFlags.NonPublic | BindingFlags.Instance);
        if (entriesField?.GetValue(sharedPool) is not IEnumerable<object> entries)
            return null;

        var entryList = entries.Cast<object>().ToList();
        if (entryList.Count == 0)
            return null;

        // 每个条目有 RelicType 属性（Type），实例化一个
        var entry = entryList[Rng.Next(entryList.Count)];
        var relicTypeProp = entry.GetType().GetProperty("RelicType");
        var relicType = relicTypeProp?.GetValue(entry) as Type;
        if (relicType == null)
            return null;

        return Activator.CreateInstance(relicType) as RelicModel;
    }

    /// <summary>
    /// 随机获取一个附魔类型并实例化。
    /// </summary>
    private static EnchantmentModel? RollEnchantment()
    {
        var enchantTypes = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t))
            .ToList();

        if (enchantTypes.Count == 0)
            return null;

        var chosen = enchantTypes[Rng.Next(enchantTypes.Count)];
        return Activator.CreateInstance(chosen) as EnchantmentModel;
    }

    /// <summary>
    /// 从牌组中随机选一张卡牌。
    /// </summary>
    private static CardModel? RollCardFromDeck(Player player)
    {
        var deck = PileType.Deck.GetPile(player).Cards;
        if (deck.Count == 0)
            return null;

        return deck[Rng.Next(deck.Count)];
    }

    /// <summary>
    /// 获取玩家当前持有的遗物列表。
    /// </summary>
    private static List<RelicModel> GetPlayerRelics(Player player)
    {
        var field = typeof(Player).GetField(
            "_relics", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(player) as List<RelicModel> ?? [];
    }
}
