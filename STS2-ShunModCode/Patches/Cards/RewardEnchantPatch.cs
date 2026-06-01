using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using STS2_ShunMod.Core;
using CoreHook = MegaCrit.Sts2.Core.Hooks.Hook;

namespace STS2_ShunMod.Patches;

/// <summary>
///     奖励附魔 — 战斗奖励卡牌概率获得随机附魔。
///     参照 s1f102500012/sts2mod 奖励附魔 模块改写。
///
///     核心机制：
///     1. 战斗奖励卡牌 → 概率附魔（每幕 12.5%，随幕数线性递增）
///     2. 商店卡牌 → 概率附魔
///     3. 附魔数量 = 当前幕数 + 1
///     4. 排除 Clone / DeprecatedEnchantment / TezcatarasEmber
///     5. Inky 只对攻击型敌方卡牌生效
///     6. 尊重 NoModifyHooks / NoCardModelModifications 标记
/// </summary>

// ════════════════════════════════ 战斗奖励附魔 ════════════════════════════════

[HarmonyPatch]
public static class RewardEnchant_TryModifyCardRewardOptions
{
    private const decimal EnchantChancePerAct = 0.125m;
    private const string VanillaEnchantmentNamespace = "MegaCrit.Sts2.Core.Models.Enchantments";

    /// <summary>不参与奖励附魔的附魔类型。</summary>
    private static readonly HashSet<Type> ExcludedRewardEnchantmentTypes = new()
    {
        typeof(Clone),
        typeof(DeprecatedEnchantment),
        typeof(TezcatarasEmber)
    };

    private static IReadOnlyList<EnchantmentModel>? _vanillaEnchantments;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var method = typeof(CoreHook).GetMethod(
            nameof(CoreHook.TryModifyCardRewardOptions),
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[]
            {
                typeof(IRunState),
                typeof(Player),
                typeof(List<CardCreationResult>),
                typeof(CardCreationOptions),
                typeof(List<AbstractModel>).MakeByRefType()
            },
            null);

        if (method != null)
        {
            ShunLogger.Info("奖励附魔", "✅ 已绑定 CoreHook.TryModifyCardRewardOptions");
            yield return method;
        }
        else
        {
            ShunLogger.Error("奖励附魔", "❌ 未找到 CoreHook.TryModifyCardRewardOptions，补丁跳过！游戏 API 可能已变更。");
        }
    }

    [HarmonyPostfix]
    private static void Postfix(
        Player player,
        List<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions,
        ref bool __result)
    {
        try
        {
            if (!ShouldProcessRewards(cardRewardOptions, creationOptions))
                return;

            var enchantedAny = TryEnchantRewardCards(player, creationOptions, cardRewardOptions);
            __result = __result || enchantedAny;
        }
        catch (Exception ex)
        {
            ShunLogger.Error("奖励附魔", $"❌ Postfix 异常: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool ShouldProcessRewards(
        List<CardCreationResult> cardRewardOptions,
        CardCreationOptions creationOptions)
    {
        if (cardRewardOptions.Count == 0) return false;
        if (creationOptions.Source != CardCreationSource.Encounter) return false;
        if (creationOptions.Flags.HasFlag(CardCreationFlags.NoModifyHooks)) return false;
        if (creationOptions.Flags.HasFlag(CardCreationFlags.NoCardModelModifications)) return false;
        return true;
    }

    private static bool TryEnchantRewardCards(
        Player player,
        CardCreationOptions creationOptions,
        List<CardCreationResult> cardRewardOptions)
    {
        var enchantedAny = false;
        var rng = creationOptions.RngOverride ?? player.PlayerRng.Rewards;

        foreach (var reward in cardRewardOptions)
        {
            if (RewardEnchantHelper.TryApplyRandomEnchantment(reward, player, rng, "reward"))
                enchantedAny = true;
        }

        return enchantedAny;
    }

    // ═══ 公开给 MerchantCardPopulate 复用的 API ═══

    internal static IReadOnlyList<EnchantmentModel> GetVanillaEnchantments()
    {
        return _vanillaEnchantments ??= typeof(EnchantmentModel).Assembly.GetTypes()
            .Where(t => !t.IsAbstract
                        && typeof(EnchantmentModel).IsAssignableFrom(t)
                        && t.Namespace == VanillaEnchantmentNamespace
                        && t.Name != "EnchantmentModel"
                        && t.Name != "DeprecatedEnchantment"
                        && t.Name != "MockFreeEnchantment")
            .Select(t => ModelDb.GetByIdOrNull<EnchantmentModel>(ModelDb.GetId(t)))
            .OfType<EnchantmentModel>()
            .OrderBy(e => e.Id.Entry, StringComparer.Ordinal)
            .ToList();
    }

    internal static decimal GetEnchantChance(int currentActIndex)
    {
        return Math.Clamp((currentActIndex + 1) * EnchantChancePerAct, 0m, 1m);
    }

    internal static decimal GetEnchantAmount(int currentActIndex)
    {
        return currentActIndex + 1;
    }

    internal static bool IsEligibleRewardEnchantment(CardModel card, EnchantmentModel enchantment)
    {
        var type = enchantment.GetType();
        if (ExcludedRewardEnchantmentTypes.Contains(type)) return false;
        if (!enchantment.CanEnchant(card)) return false;
        if (enchantment is Inky && !IsInkyCompatibleRewardCard(card)) return false;
        return true;
    }

    private static bool IsInkyCompatibleRewardCard(CardModel card)
    {
        return card.Type == CardType.Attack
               && card.TargetType is TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy;
    }
}

// ════════════════════════════════ 商店卡牌附魔 ════════════════════════════════

[HarmonyPatch(typeof(MerchantCardEntry), nameof(MerchantCardEntry.Populate))]
public static class RewardEnchant_MerchantCardPopulate
{
    [HarmonyPostfix]
    private static void Postfix(MerchantCardEntry __instance)
    {
        try
        {
            var creationResult = __instance.CreationResult;
            if (creationResult == null) return;

            var player = creationResult.Card.Owner;
            if (player == null) return;

            var shopsRng = player.PlayerRng.Shops;
            var currentCard = creationResult.Card;
            var derivedName =
                $"ShunMod.shop.{shopsRng.Counter}.{currentCard.Id.Entry}.{currentCard.CurrentUpgradeLevel}.{currentCard.Enchantment?.Id.Entry ?? "none"}";
            var localRng = new Rng(shopsRng.Seed, derivedName);

            RewardEnchantHelper.TryApplyRandomEnchantment(creationResult, player, localRng, "merchant");
        }
        catch (Exception ex)
        {
            ShunLogger.Error("奖励附魔", $"❌ 商店 Postfix 异常: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

// ════════════════════════════════ 共享逻辑 ════════════════════════════════════

internal static class RewardEnchantHelper
{
    /// <summary>
    ///     对单张卡牌尝试随机附魔。
    /// </summary>
    /// <returns>是否成功附魔</returns>
    public static bool TryApplyRandomEnchantment(
        CardCreationResult result,
        Player player,
        Rng rng,
        string sourceLabel)
    {
        var currentCard = result.Card;
        var enchantments = RewardEnchant_TryModifyCardRewardOptions.GetVanillaEnchantments();
        var candidates = enchantments
            .Where(e => RewardEnchant_TryModifyCardRewardOptions.IsEligibleRewardEnchantment(currentCard, e))
            .ToList();

        if (candidates.Count == 0) return false;

        var chance = RewardEnchant_TryModifyCardRewardOptions.GetEnchantChance(player.RunState.CurrentActIndex);
        if ((decimal)rng.NextFloat() > chance) return false;

        var selected = rng.NextItem(candidates);
        if (selected == null) return false;

        var enchantedCard = player.RunState.CloneCard(currentCard);
        var enchantAmount = RewardEnchant_TryModifyCardRewardOptions.GetEnchantAmount(player.RunState.CurrentActIndex);
        CardCmd.Enchant(selected.ToMutable(), enchantedCard, enchantAmount);
        result.ModifyCard(enchantedCard);

        ShunLogger.Info("奖励附魔",
            $"Added {selected.Id.Entry} x{enchantAmount} to {sourceLabel} card {enchantedCard.Id.Entry}.");
        return true;
    }
}