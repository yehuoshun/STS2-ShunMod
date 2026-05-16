using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Events;

/// <summary>
/// 遗物交易所 — 用遗物换遗物、换附魔、刷新选项、退出。
/// </summary>
/// <remarks>
/// 可反复交易，直到选择退出：
/// <list type="number">
/// <item>随机一个玩家遗物 → 随机一个新遗物（从遗物池抽取）</item>
/// <item>随机一个玩家遗物 → 卡牌附魔（随机附魔类型，目标随机牌组中一张）</item>
/// <item>扣除 5 生命 → 刷新以上两个选项的随机结果</item>
/// <item>退出交易所</item>
/// </list>
/// 选项 1-3 不结束事件，选完后重新随机生成选项继续交易。
/// </remarks>
[Pool(typeof(EventRelicPool))]
public class ShunModRelicExchange : EventModel
{
    private static readonly System.Random Rnd = new();

    // ── 当前随机结果（每次生成选项时更新） ──
    private RelicModel? _playerRelic1;
    private RelicModel? _rewardRelic;
    private RelicModel? _playerRelic2;
    private EnchantmentModel? _rewardEnchant;
    private CardModel? _enchantTargetCard;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var player = Owner;
        var playerRelics = GetPlayerRelics(player);

        RollOptions(player, playerRelics);

        var options = new List<EventOption>();

        // ── 选项 1：遗物换遗物 ──
        if (_playerRelic1 != null && _rewardRelic != null && playerRelics.Count > 0)
        {
            var opt1 = new EventOption(InitialOptionKey("OPT_1"));
            opt1.OnSelect = async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic1!);
                await PlayerCmd.GainRelic(Owner!, _rewardRelic!);
            };
            options.Add(opt1);
        }

        // ── 选项 2：遗物换附魔 ──
        if (_playerRelic2 != null && _rewardEnchant != null && _enchantTargetCard != null)
        {
            var opt2 = new EventOption(InitialOptionKey("OPT_2"));
            opt2.OnSelect = async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic2!);
                CardCmd.Enchant(_enchantTargetCard, _rewardEnchant!, 1);
            };
            options.Add(opt2);
        }

        // ── 选项 3：消耗 5 HP 刷新 ──
        var opt3 = new EventOption(InitialOptionKey("OPT_3"));
        opt3.OnSelect = async () =>
        {
            await PlayerCmd.Damage(Owner!, 5);
        };
        options.Add(opt3);

        // ── 选项 4：退出 ──
        var opt4 = new EventOption(InitialOptionKey("OPT_4"));
        opt4.OnSelect = async () =>
        {
            SetEventFinished(L10NLookup("pages.CLOSE.description"));
            await Task.CompletedTask;
        };
        options.Add(opt4);

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

        _playerRelic1 = playerRelics[Rnd.Next(playerRelics.Count)];
        _rewardRelic = RollRelicFromPool();

        if (playerRelics.Count > 1)
        {
            RelicModel pick2;
            do { pick2 = playerRelics[Rnd.Next(playerRelics.Count)]; }
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
        var sharedPool = ModelDb.GetByIdOrNull<RelicPoolModel>(
            ModelDb.GetId(typeof(SharedRelicPool)));
        if (sharedPool == null)
            return null;

        var entriesField = typeof(RelicPoolModel).GetField(
            "_entries", BindingFlags.NonPublic | BindingFlags.Instance);
        if (entriesField?.GetValue(sharedPool) is not IEnumerable<object> entries)
            return null;

        var entryList = entries.Cast<object>().ToList();
        if (entryList.Count == 0)
            return null;

        var entry = entryList[Rnd.Next(entryList.Count)];
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

        var chosen = enchantTypes[Rnd.Next(enchantTypes.Count)];
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

        return deck[Rnd.Next(deck.Count)];
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
