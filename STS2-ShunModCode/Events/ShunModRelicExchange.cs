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
/// 遗物交易所 — 可反复交易，直到选退出。
/// </summary>
[Pool(typeof(EventRelicPool))]
public class ShunModRelicExchange : EventModel
{
    private static readonly System.Random Rnd = new();

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

        if (_playerRelic1 != null && _rewardRelic != null && playerRelics.Count > 0)
        {
            options.Add(new EventOption(this, async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic1!);
                // TODO: 确认给玩家遗物的正确 API，IDE 里看 Owner. 补全
                GiveRelicToPlayer(Owner!, _rewardRelic!);
            }, InitialOptionKey("OPT_1")));
        }

        if (_playerRelic2 != null && _rewardEnchant != null && _enchantTargetCard != null)
        {
            options.Add(new EventOption(this, async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic2!);
                CardCmd.Enchant(_rewardEnchant!, _enchantTargetCard, 1);
            }, InitialOptionKey("OPT_2")));
        }

        options.Add(new EventOption(this, async () =>
        {
            // TODO: 确认扣血 API，IDE 里看 PlayerCmd. / DamageCmd. 补全
            await DamagePlayer(Owner!, 5);
        }, InitialOptionKey("OPT_3")));

        options.Add(new EventOption(this, async () =>
        {
            SetEventFinished(L10NLookup("pages.CLOSE.description"));
            await Task.CompletedTask;
        }, InitialOptionKey("OPT_4")));

        return options;
    }

    // ════════════════════════════════════════════════════════
    // 内部辅助
    // ════════════════════════════════════════════════════════

    private void RollOptions(Player player, IReadOnlyList<RelicModel> playerRelics)
    {
        if (playerRelics.Count == 0) return;

        _playerRelic1 = playerRelics[Rnd.Next(playerRelics.Count)];
        _rewardRelic = RollRelicFromPool();

        if (playerRelics.Count > 1)
        {
            RelicModel pick2;
            do { pick2 = playerRelics[Rnd.Next(playerRelics.Count)]; }
            while (pick2 == _playerRelic1);
            _playerRelic2 = pick2;
        }
        else _playerRelic2 = _playerRelic1;

        _rewardEnchant = RollEnchantment();
        _enchantTargetCard = RollCardFromDeck(player);
    }

    /// <summary>
    /// 给予玩家遗物（反射操作 _relics 列表 + 触发事件）。
    /// </summary>
    private static void GiveRelicToPlayer(Player player, RelicModel relic)
    {
        var field = typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(player) is not List<RelicModel> list) return;
        list.Add(relic);
        // TODO: 触发 RelicGained 事件（如有需要）
    }

    /// <summary>
    /// 对玩家造成伤害。
    /// </summary>
    private static async Task DamagePlayer(Player player, int amount)
    {
        // TODO: IDE 里找正确扣血 API，可能有 DamageCmd / PlayerCmd.TakeDamage 等
        // 临时用 Creature 直接扣，IDE 补全确认正确方法
        player.Creature.Health -= amount;
        await Task.CompletedTask;
    }

    private static RelicModel? RollRelicFromPool()
    {
        var sharedPool = ModelDb.GetByIdOrNull<RelicPoolModel>(
            ModelDb.GetId(typeof(SharedRelicPool)));
        if (sharedPool == null) return null;

        var entriesField = typeof(RelicPoolModel).GetField(
            "_entries", BindingFlags.NonPublic | BindingFlags.Instance);
        if (entriesField?.GetValue(sharedPool) is not IEnumerable<object> entries) return null;

        var entryList = entries.Cast<object>().ToList();
        if (entryList.Count == 0) return null;

        var entry = entryList[Rnd.Next(entryList.Count)];
        var relicType = entry.GetType().GetProperty("RelicType")?.GetValue(entry) as Type;
        return relicType != null ? Activator.CreateInstance(relicType) as RelicModel : null;
    }

    private static EnchantmentModel? RollEnchantment()
    {
        var enchantTypes = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t))
            .ToList();
        if (enchantTypes.Count == 0) return null;
        return Activator.CreateInstance(enchantTypes[Rnd.Next(enchantTypes.Count)]) as EnchantmentModel;
    }

    private static CardModel? RollCardFromDeck(Player player)
    {
        var deck = PileType.Deck.GetPile(player).Cards;
        return deck.Count > 0 ? deck[Rnd.Next(deck.Count)] : null;
    }

    private static List<RelicModel> GetPlayerRelics(Player player)
    {
        var field = typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(player) as List<RelicModel> ?? [];
    }
}
