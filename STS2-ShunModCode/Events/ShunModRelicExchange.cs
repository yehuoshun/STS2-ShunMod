using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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

        // PlayerChoiceContext — IDE 里补全看 EventModel 有什么属性/字段能拿到 ctx
        // 候选: Context / ChoiceContext / PlayerContext / Player.ChoiceContext
        var ctx = /* TODO: 用 IDE 补全 EventModel 看哪个能拿到 PlayerChoiceContext */ null!;

        if (_playerRelic1 != null && _rewardRelic != null && playerRelics.Count > 0)
        {
            options.Add(new EventOption(ctx, InitialOptionKey("OPT_1"), async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic1!);
                await PlayerCmd.GainRelic(Owner!, _rewardRelic!);
            }));
        }

        if (_playerRelic2 != null && _rewardEnchant != null && _enchantTargetCard != null)
        {
            options.Add(new EventOption(ctx, InitialOptionKey("OPT_2"), async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic2!);
                CardCmd.Enchant(_enchantTargetCard, _rewardEnchant!, 1);
            }));
        }

        options.Add(new EventOption(ctx, InitialOptionKey("OPT_3"), async () =>
        {
            await PlayerCmd.Damage(Owner!, 5);
        }));

        options.Add(new EventOption(ctx, InitialOptionKey("OPT_4"), async () =>
        {
            SetEventFinished(L10NLookup("pages.CLOSE.description"));
            await Task.CompletedTask;
        }));

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
