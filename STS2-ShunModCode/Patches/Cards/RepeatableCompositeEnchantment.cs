using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     复合附魔 — 将多个附魔包装为一个 EnchantmentModel，实现无限附魔。
///
///     原理：
///     1. 作为 EnchantmentModel 子类挂在 card.Enchantment 上
///     2. 内部维护 List&lt;EnchantmentModel&gt; 存储所有实际附魔
///     3. 所有行为（伤害/格挡计算、Hook、序列化）委托给内部附魔
///     4. AddOrStackEnchantment：同类叠加层数，异类追加
///     5. CanEnchant 返回 false 防止嵌套
/// </summary>
public sealed class RepeatableCompositeEnchantment : EnchantmentModel
{
    private List<EnchantmentModel> _innerEnchantments = new();
    private List<EnchantmentModel> _subscribed = new();

    // ══════════════════════════ 序列化 ══════════════════════════

    [SavedProperty]
    private string? SavedEnchantmentsJson
    {
        get
        {
            if (_innerEnchantments.Count == 0) return null;
            var arr = _innerEnchantments.Select(e => e.ToSerializable()).ToArray();
            return JsonSerializer.Serialize(arr);
        }
        set
        {
            Unsubscribe();
            _innerEnchantments = new List<EnchantmentModel>();
            if (string.IsNullOrWhiteSpace(value))
            {
                Amount = 0;
                return;
            }
            var arr = JsonSerializer.Deserialize<SerializableEnchantment[]>(value);
            if (arr == null)
            {
                Amount = 0;
                return;
            }
            foreach (var s in arr)
                _innerEnchantments.Add(EnchantmentModel.FromSerializable(s));
            Amount = _innerEnchantments.Count;
            RefreshStatus();
        }
    }

    // ══════════════════════════ 核心属性 ══════════════════════════

    public IReadOnlyList<EnchantmentModel> InnerEnchantments
    {
        get
        {
            EnsureBindings();
            return _innerEnchantments;
        }
    }

    public override bool CanEnchant(CardModel card) => false;

    public override bool HasExtraCardText => false;

    public override bool ShowAmount
    {
        get
        {
            EnsureBindings();
            return _innerEnchantments.Count > 1 || GetLead()?.ShowAmount == true;
        }
    }

    public override int DisplayAmount
    {
        get
        {
            EnsureBindings();
            if (_innerEnchantments.Count > 1) return _innerEnchantments.Count;
            return GetLead()?.DisplayAmount ?? 0;
        }
    }

    public override bool ShouldStartAtBottomOfDrawPile
    {
        get
        {
            EnsureBindings();
            return _innerEnchantments.Any(e => e.ShouldStartAtBottomOfDrawPile);
        }
    }

    public override bool ShouldGlowGold
    {
        get
        {
            EnsureBindings();
            return _innerEnchantments.Any(e => e.ShouldGlowGold);
        }
    }

    public override bool ShouldGlowRed
    {
        get
        {
            EnsureBindings();
            return _innerEnchantments.Any(e => e.ShouldGlowRed);
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            EnsureBindings();
            return _innerEnchantments.SelectMany(e => e.HoverTips).ToList();
        }
    }

    // ══════════════════════════ 查询 ══════════════════════════

    public EnchantmentModel? GetLead()
    {
        EnsureBindings();
        return _innerEnchantments.LastOrDefault();
    }

    public bool ContainsType(Type type)
    {
        EnsureBindings();
        return _innerEnchantments.Any(e => e.GetType() == type);
    }

    public EnchantmentModel? Find(Type type)
    {
        EnsureBindings();
        return _innerEnchantments.FirstOrDefault(e => e.GetType() == type);
    }

    // ══════════════════════════ 增删 ══════════════════════════

    /// <summary>
    ///     导入卡牌已有的原版附魔到复合中。
    /// </summary>
    public void ImportExisting(EnchantmentModel enchantment)
    {
        AssertMutable();
        EnsureCompositeCard();

        if (!enchantment.HasCard)
            enchantment.ApplyInternal(Card, enchantment.Amount);
        else if (enchantment.Card != Card)
        {
            enchantment.ClearInternal();
            enchantment.ApplyInternal(Card, enchantment.Amount);
        }

        _innerEnchantments.Add(enchantment);
        Subscribe(enchantment);
        Amount = _innerEnchantments.Count;
        RefreshStatus();
    }

    /// <summary>
    ///     添加或叠加附魔。同类 → 叠加层数，异类 → 追加。
    /// </summary>
    public EnchantmentModel AddOrStack(EnchantmentModel enchantment, decimal amount)
    {
        AssertMutable();
        EnsureCompositeCard();
        EnsureBindings();

        var existing = Find(enchantment.GetType());
        if (existing != null)
        {
            existing.Amount += (int)amount;
            existing.RecalculateValues();
            Card.DynamicVars.RecalculateForUpgradeOrEnchant();
            RefreshStatus();
            return existing;
        }

        enchantment.ApplyInternal(Card, amount);
        _innerEnchantments.Add(enchantment);
        Subscribe(enchantment);
        Amount = _innerEnchantments.Count;
        enchantment.ModifyCard();
        RefreshStatus();
        return enchantment;
    }

    // ══════════════════════════ 效果计算 ══════════════════════════

    public override void ModifyCard()
    {
        EnsureBindings();
        foreach (var e in _innerEnchantments)
            e.ModifyCard();
    }

    public override void ClearInternal()
    {
        Unsubscribe();
        foreach (var e in _innerEnchantments)
            e.ClearInternal();
        _innerEnchantments.Clear();
        Amount = 0;
        base.ClearInternal();
    }

    protected override void DeepCloneFields(EnchantmentModel clone)
    {
        base.DeepCloneFields(clone);
        if (clone is RepeatableCompositeEnchantment c)
        {
            c._innerEnchantments = _innerEnchantments
                .Select(e => (EnchantmentModel)e.ClonePreservingMutability())
                .ToList();
            c.Amount = c._innerEnchantments.Count;
            c.RefreshStatus();
        }
    }

    protected override decimal CalculateFinalBlock(decimal originalBlock, ValueProp props)
    {
        var result = originalBlock;
        foreach (var e in _innerEnchantments)
        {
            result += e.EnchantBlockAdditive(result, props);
            result *= e.EnchantBlockMultiplicative(result, props);
        }
        return result;
    }

    protected override decimal CalculateFinalDamage(decimal originalDamage, ValueProp props)
    {
        var result = originalDamage;
        foreach (var e in _innerEnchantments)
        {
            result += e.EnchantDamageAdditive(result, props);
            result *= e.EnchantDamageMultiplicative(result, props);
        }
        return result;
    }

    // ══════════════════════════ 生命周期 Hook ══════════════════════════

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        EnsureBindings();
        foreach (var e in _innerEnchantments)
            await e.AfterCardPlayed(context, cardPlay);
        RefreshStatus();
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        EnsureBindings();
        foreach (var e in _innerEnchantments)
            await e.AfterCardDrawn(choiceContext, card, fromHandDraw);
        RefreshStatus();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        EnsureBindings();
        foreach (var e in _innerEnchantments)
            await e.AfterPlayerTurnStart(choiceContext, player);
        RefreshStatus();
    }

    public override async Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        EnsureBindings();
        foreach (var e in _innerEnchantments)
            await e.BeforeFlush(choiceContext, player);
        RefreshStatus();
    }

    public override void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
    {
        EnsureBindings();
        foreach (var e in _innerEnchantments)
            e.ModifyShuffleOrder(player, cards, isInitialShuffle);
    }

    // ══════════════════════════ 内部 ══════════════════════════

    private void EnsureCompositeCard()
    {
        if (!HasCard)
            throw new InvalidOperationException("复合附魔尚未挂载到卡牌");
    }

    private void EnsureBindings()
    {
        if (!HasCard) return;
        foreach (var e in _innerEnchantments)
        {
            if (!e.HasCard || e.Card != Card)
            {
                if (e.HasCard) e.ClearInternal();
                e.ApplyInternal(Card, e.Amount);
            }
            Subscribe(e);
        }
    }

    private void Subscribe(EnchantmentModel enchantment)
    {
        if (_subscribed.Contains(enchantment)) return;
        enchantment.StatusChanged += OnStatusChanged;
        _subscribed.Add(enchantment);
    }

    private void Unsubscribe()
    {
        foreach (var e in _subscribed)
            e.StatusChanged -= OnStatusChanged;
        _subscribed.Clear();
    }

    private void OnStatusChanged() => RefreshStatus();

    internal void RefreshStatus()
    {
        // EnchantmentStatus: 0=Active, 1=Consumed
        Status = _innerEnchantments.Any(e => (int)e.Status == 0) ? 0 : 1;
    }
}