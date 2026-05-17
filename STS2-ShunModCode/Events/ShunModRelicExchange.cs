using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Events;

/// <summary>
///     遗物交易所 — 可反复交易，直到选退出。
/// </summary>
public class ShunModRelicExchange : EventModel
{
    // ════════════════════════════════════════════════════════
    // 事件背景图路径修正 — 游戏默认路径不含 mod 前缀
    // ════════════════════════════════════════════════════════

    public override IEnumerable<string> GetAssetPaths(IRunState runState)
    {
        var paths = base.GetAssetPaths(runState).ToList();
        var defaultPath = ImageHelper.GetImagePath($"events/{Id.Entry.ToLowerInvariant()}.png");
        var modPath = $"res://STS2-ShunMod/images/events/{Id.Entry.ToLowerInvariant()}.png";
        var index = paths.IndexOf(defaultPath);
        if (index >= 0)
            paths[index] = modPath;
        return paths;
    }

    // ════════════════════════════════════════════════════════
    // 不可交易的遗物稀有度 — 初始遗物 / Circlet 等
    // ════════════════════════════════════════════════════════

    private static readonly HashSet<RelicRarity> NonTradeableRarities =
    [
        RelicRarity.Starter
    ];

    /// <summary>
    ///     遗物名称黑名单 — 这些遗物即使不是 Starter 稀有度也不可交易。
    ///     例如 Circlet（后备遗物）等。
    /// </summary>
    private static readonly HashSet<string> NonTradeableRelicNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Circlet"
    };

    /// <summary>
    ///     附魔名称黑名单 — 隐藏/系统附魔不应出现在随机池中。
    /// </summary>
    private static readonly HashSet<string> NonTradeableEnchantNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "EnchantmentModel", // 基类，不应出现
    };

    // ════════════════════════════════════════════════════════
    // 随机状态
    // ════════════════════════════════════════════════════════

    private static readonly Random Rnd = new();
    private RelicModel? _playerRelic1;
    private RelicModel? _playerRelic2;
    private EnchantmentModel? _rewardEnchant;
    private RelicModel? _rewardRelic;
    private CardModel? _enchantTargetCard;

    /// <summary>
    ///     标记本轮 OPT_1 已被执行（防止 OPT_2 对同一遗物二次操作）。
    /// </summary>
    private bool _opt1Executed;

    // ════════════════════════════════════════════════════════
    // DynamicVars — 游戏引擎自动将 {VAR} 占位符替换为对应值
    // ════════════════════════════════════════════════════════

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
        var relics = GetTradeableRelics(player);
        RollOptions(player, relics);

        ((StringVar)DynamicVars["LOSE_RELIC_1"]).Value = _playerRelic1?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["GAIN_RELIC"]).Value = _rewardRelic?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["LOSE_RELIC_2"]).Value = _playerRelic2?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["ENCHANT_NAME"]).Value = _rewardEnchant?.Title ?? EmptyLoc;
        ((StringVar)DynamicVars["CARD_NAME"]).Value = _enchantTargetCard?.Title ?? EmptyLoc;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var player = Owner;
        if (player == null) return [MakeExitOption()];
        var relics = GetTradeableRelics(player);

        var options = new List<EventOption>();

        // 选项 1: 遗物换遗物
        if (_playerRelic1 != null && _rewardRelic != null && relics.Count > 0)
        {
            var losingRelic = _playerRelic1;
            var gainingRelic = _rewardRelic;
            options.Add(new EventOption(this, async () =>
            {
                RelicHelper.RemoveRelic(Owner!, losingRelic);
                GiveRelicToPlayer(Owner!, gainingRelic);
                _opt1Executed = true;
            }, InitialOptionKey("OPT_1")));
        }

        // 选项 2: 遗物换附魔 — 至少 2 个可交易遗物才显示
        // （避免仅 1 遗物时 OPT_1/OPT_2 争抢同一遗物）
        var canShowOpt2 = _playerRelic2 != null
            && _rewardEnchant != null
            && _enchantTargetCard != null
            && relics.Count >= 2;
        if (canShowOpt2)
        {
            var losingRelic = _playerRelic2;
            var enchant = _rewardEnchant;
            var targetCard = _enchantTargetCard;
            options.Add(new EventOption(this, async () =>
            {
                // 如果 OPT_1 已执行且 OPT_2 用的是同一遗物，则不重复移除
                if (!_opt1Executed || losingRelic != _playerRelic1)
                    RelicHelper.RemoveRelic(Owner!, losingRelic);
                CardCmd.Enchant(enchant, targetCard, 1);
            }, InitialOptionKey("OPT_2")));
        }

        // 选项 3: 扣 5 HP 刷新
        if (relics.Count > 0)
        {
            var self = this;
            options.Add(new EventOption(this, async () =>
            {
                await DamagePlayer(Owner!, 5);
                // 重新 roll + 更新 DynamicVars
                var freshRelics = GetTradeableRelics(Owner!);
                self.RollOptions(Owner!, freshRelics);
                self.CalculateVars();
                self._opt1Executed = false;
                self.SetEventState(self.GenerateInitialOptions());
            }, InitialOptionKey("OPT_3")));
        }

        // 选项 4: 退出
        options.Add(MakeExitOption());

        return options;
    }

    // ════════════════════════════════════════════════════════
    // 内部辅助
    // ════════════════════════════════════════════════════════

    private EventOption MakeExitOption()
    {
        return new EventOption(this, async () =>
        {
            SetEventFinished(L10NLookup("pages.CLOSE.description"));
        }, InitialOptionKey("OPT_4"));
    }

    private void RollOptions(Player player, IReadOnlyList<RelicModel> playerRelics)
    {
        _playerRelic1 = null;
        _playerRelic2 = null;
        _rewardRelic = null;
        _rewardEnchant = null;
        _enchantTargetCard = null;

        if (playerRelics.Count == 0) return;

        _playerRelic1 = playerRelics[Rnd.Next(playerRelics.Count)];
        _rewardRelic = RollRelicFromPool();

        // OPT_2：用另一个遗物（需要 ≥2 个可交易遗物）
        if (playerRelics.Count >= 2)
        {
            RelicModel pick2;
            do
            {
                pick2 = playerRelics[Rnd.Next(playerRelics.Count)];
            } while (pick2 == _playerRelic1);

            _playerRelic2 = pick2;
        }

        _rewardEnchant = RollEnchantment();
        _enchantTargetCard = RollCardFromDeck(player);
    }

    /// <summary>
    ///     给予玩家遗物。优先使用公开 API Player.GainRelic，
    ///     回退到反射调用 AddRelicInternal。
    /// </summary>
    private static void GiveRelicToPlayer(Player player, RelicModel relic)
    {
        // 优先尝试公开 API
        var gainMethod = typeof(Player).GetMethod("GainRelic",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(RelicModel)]);
        if (gainMethod != null)
        {
            gainMethod.Invoke(player, [relic]);
            return;
        }

        // 回退到 AddRelicInternal
        var addMethod = typeof(Player).GetMethod("AddRelicInternal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        addMethod?.Invoke(player, [relic]);
    }

    /// <summary>
    ///     对玩家造成伤害。优先使用 Creature.ChangeHp，
    ///     回退到反射 TakeDamage / 直接扣血。
    /// </summary>
    private static async Task DamagePlayer(Player player, int amount)
    {
        var creature = player.Creature;

        // 方案 1：尝试 Creature.ChangeHp（如存在会触发死亡检测）
        var changeHpMethod = typeof(Creature).GetMethod("ChangeHp",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(decimal)]);
        if (changeHpMethod != null)
        {
            changeHpMethod.Invoke(creature, [(decimal)-amount]);
            await Task.CompletedTask;
            return;
        }

        // 方案 2：反射 TakeDamage
        var takeDmgMethod = typeof(Creature).GetMethod("TakeDamage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (takeDmgMethod != null)
        {
            takeDmgMethod.Invoke(creature, [(decimal)amount]);
            await Task.CompletedTask;
            return;
        }

        // 方案 3：直接扣血（最后手段）
        var hpProp = typeof(Creature).GetProperty("CurrentHp");
        if (hpProp != null)
        {
            var current = (decimal)(hpProp.GetValue(creature) ?? 0m);
            hpProp.SetValue(creature, Math.Max(0, current - amount));
        }

        await Task.CompletedTask;
    }

    private static RelicModel? RollRelicFromPool()
    {
        var allRelicsProp = typeof(ModelDb).GetProperty("AllRelics",
            BindingFlags.Public | BindingFlags.Static);
        if (allRelicsProp?.GetValue(null) is IEnumerable<RelicModel> relics)
        {
            var list = relics
                .Where(r => !NonTradeableRarities.Contains(r.Rarity))
                .Where(r => !NonTradeableRelicNames.Contains(r.GetType().Name))
                .ToList();
            return list.Count > 0 ? list[Rnd.Next(list.Count)] : null;
        }

        // 回退：扫描所有 RelicModel 子类型并从 ModelDb 取实例
        var relicTypes = typeof(RelicModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(RelicModel).IsAssignableFrom(t))
            .ToList();
        var valid = relicTypes
            .Select(t => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(t)))
            .Where(r => r != null)
            .Cast<RelicModel>()
            .Where(r => !NonTradeableRarities.Contains(r.Rarity))
            .Where(r => !NonTradeableRelicNames.Contains(r.GetType().Name))
            .ToList();
        return valid.Count > 0 ? valid[Rnd.Next(valid.Count)] : null;
    }

    private static EnchantmentModel? RollEnchantment()
    {
        var enchantTypes = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t))
            .Where(t => !NonTradeableEnchantNames.Contains(t.Name))
            .ToList();
        var valid = enchantTypes
            .Select(t => ModelDb.GetByIdOrNull<EnchantmentModel>(ModelDb.GetId(t)))
            .Where(e => e != null)
            .Cast<EnchantmentModel>()
            .ToList();
        return valid.Count > 0 ? valid[Rnd.Next(valid.Count)] : null;
    }

    private static CardModel? RollCardFromDeck(Player player)
    {
        var deck = player.Deck.Cards;
        return deck.Count > 0 ? deck[Rnd.Next(deck.Count)] : null;
    }

    /// <summary>
    ///     获取玩家可交易的遗物列表（排除初始遗物和黑名单遗物）。
    /// </summary>
    private static List<RelicModel> GetTradeableRelics(Player? player)
    {
        if (player == null) return [];
        return player.Relics
            .Where(r => !NonTradeableRarities.Contains(r.Rarity))
            .Where(r => !NonTradeableRelicNames.Contains(r.GetType().Name))
            .ToList();
    }
}
