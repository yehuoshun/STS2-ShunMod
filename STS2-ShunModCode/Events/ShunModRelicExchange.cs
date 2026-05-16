using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
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

    private static readonly Random Rnd = new();
    private CardModel? _enchantTargetCard;

    private RelicModel? _playerRelic1;
    private RelicModel? _playerRelic2;
    private EnchantmentModel? _rewardEnchant;
    private RelicModel? _rewardRelic;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var player = Owner;
        if (player == null) return [];
        var playerRelics = GetPlayerRelics(player);
        RollOptions(player, playerRelics);

        var options = new List<EventOption>();
        var entry = Id.Entry;

        // 选项 1: 指定遗物换指定遗物
        if (_playerRelic1 != null && _rewardRelic != null && playerRelics.Count > 0)
        {
            var loseName = _playerRelic1.Title.ToString();
            var gainName = _rewardRelic.Title.ToString();

            // L10NLookup + LocString.Add 注入动态变量，ToString() 解析为最终文本
            var opt1Title = L10NLookup($"{entry}.pages.INITIAL.options.OPT_1.title");
            opt1Title.Add("LOSE_RELIC", loseName);
            opt1Title.Add("GAIN_RELIC", gainName);

            // EventOption(owner, action, title, titleTip, bodyTip) — 5 参数构造器
            // title 用解析后的 string，IHoverTip 传 null（无额外悬浮提示）
            options.Add(new EventOption(this, async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic1!);
                GiveRelicToPlayer(Owner!, _rewardRelic!);
            }, opt1Title.ToString(), null!, null!));
        }

        // 选项 2: 指定遗物换指定附魔
        if (_playerRelic2 != null && _rewardEnchant != null && _enchantTargetCard != null)
        {
            var loseName = _playerRelic2.Title.ToString();
            var enchantName = _rewardEnchant.Title.ToString();
            var cardName = _enchantTargetCard.Title.ToString();

            // L10NLookup + LocString.Add 注入动态变量
            var opt2Title = L10NLookup($"{entry}.pages.INITIAL.options.OPT_2.title");
            opt2Title.Add("LOSE_RELIC", loseName);
            opt2Title.Add("ENCHANT_NAME", enchantName);
            opt2Title.Add("CARD_NAME", cardName);

            // EventOption(owner, action, title, titleTip, bodyTip) — 5 参数构造器
            options.Add(new EventOption(this, async () =>
            {
                RelicHelper.RemoveRelic(Owner!, _playerRelic2!);
                CardCmd.Enchant(_rewardEnchant!, _enchantTargetCard, 1);
            }, opt2Title.ToString(), null!, null!));
        }

        // 选项 3: 扣 5 HP 刷新（仅在至少有一个遗物可交易时显示）
        if (playerRelics.Count > 0)
        {
            options.Add(new EventOption(this, async () =>
            {
                // 扣血
                await DamagePlayer(Owner!, 5);
                // 重新生成选项并刷新（GenerateInitialOptions 内部已调用 RollOptions）
                var freshOptions = GenerateInitialOptions();
                RefreshOptions(freshOptions);
            }, InitialOptionKey("OPT_3")));
        }

        // 选项 4: 退出
        options.Add(new EventOption(this, async () =>
        {
            SetEventFinished(L10NLookup("pages.CLOSE.description"));
        }, InitialOptionKey("OPT_4")));

        return options;
    }

    // ════════════════════════════════════════════════════════
    // 内部辅助
    // ════════════════════════════════════════════════════════

    /// <summary>
    ///     刷新事件选项列表。尝试 EventOptions / Options / _currentOptions 等属性/字段。
    ///     参照 STS2Plus TryWriteOptions 反射模式。
    /// </summary>
    private void RefreshOptions(IReadOnlyList<EventOption> options)
    {
        var type = GetType();
        // 优先尝试公共属性
        foreach (var propName in new[] { "EventOptions", "CurrentOptions", "Options" })
        {
            var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.CanWrite == true)
            {
                prop.SetValue(this, options);
                return;
            }
        }
        // 回退到私有字段
        foreach (var fieldName in new[] { "_currentOptions", "_eventOptions", "_options" })
        {
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(this, options);
                return;
            }
        }
    }

    private void RollOptions(Player player, IReadOnlyList<RelicModel> playerRelics)
    {
        if (playerRelics.Count == 0) return;

        _playerRelic1 = playerRelics[Rnd.Next(playerRelics.Count)];
        _rewardRelic = RollRelicFromPool();

        if (playerRelics.Count > 1)
        {
            RelicModel pick2;
            do
            {
                pick2 = playerRelics[Rnd.Next(playerRelics.Count)];
            } while (pick2 == _playerRelic1);

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
    ///     给予玩家遗物（反射调用 Player.AddRelicInternal，因为方法是 internal/private）。
    /// </summary>
    private static void GiveRelicToPlayer(Player player, RelicModel relic)
    {
        var method = typeof(Player).GetMethod("AddRelicInternal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(player, [relic]);
    }

    /// <summary>
    ///     对玩家造成伤害（通过 CreatureCmd.DealDamage 或直接扣血）。
    ///     TODO: 游戏提供公用伤害 API 后替换反射实现。
    /// </summary>
    private static async Task DamagePlayer(Player player, int amount)
    {
        // 尝试通过 Creature 内部的 TakeDamage 方法造成伤害
        var takeDmgMethod = typeof(Creature).GetMethod("TakeDamage",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (takeDmgMethod != null)
        {
            takeDmgMethod.Invoke(player.Creature, [(decimal)amount]);
        }
        else
        {
            // 回退：直接扣当前生命值
            var hpProp = typeof(Creature).GetProperty("CurrentHp");
            if (hpProp != null)
            {
                var current = (decimal)(hpProp.GetValue(player.Creature) ?? 0m);
                hpProp.SetValue(player.Creature, Math.Max(0, current - amount));
            }
        }

        await Task.CompletedTask;
    }

    private static RelicModel? RollRelicFromPool()
    {
        // ModelDb.AllRelics 不存在时回退到反射获取所有 RelicModel 子类
        var allRelicsProp = typeof(ModelDb).GetProperty("AllRelics",
            BindingFlags.Public | BindingFlags.Static);
        if (allRelicsProp?.GetValue(null) is IEnumerable<RelicModel> relics)
        {
            var list = relics.ToList();
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
            .ToList();
        return valid.Count > 0 ? valid[Rnd.Next(valid.Count)] : null;
    }

    private static EnchantmentModel? RollEnchantment()
    {
        // 从 ModelDb 取正规实例，不能用 Activator.CreateInstance（会触发 DuplicateModelException）
        var enchantTypes = typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t))
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

    private static List<RelicModel> GetPlayerRelics(Player? player)
    {
        return player?.Relics.ToList() ?? [];
    }
}