using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Relics;

/// <summary>
/// 逆七咒之戒 — 七咒之戒的反转版。
/// 7 道诅咒逆转为祝福，7 道祝福部分逆转。
/// </summary>
[Pool(typeof(SharedRelicPool))]
public class ReverseRingOfSevenCurses : RelicModel
{
    private GoldModificationGuard? _goldGuard;

    private GoldModificationGuard GoldGuard => _goldGuard ??= new GoldModificationGuard(
        () => Owner,
        amount => Math.Floor(amount * 1.5m),
        async amount => await PlayerCmd.LoseGold(amount * 0.5m, Owner!)
    );

    // ═══════════════════════════════════════════
    // 基础属性
    // ═══════════════════════════════════════════

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override string BigIconPath =>
        "res://STS2_ShunMod/images/relics/reverse_ring_of_seven_curses.png";

    public override string PackedIconPath =>
        "res://STS2_ShunMod/images/relics/reverse_ring_of_seven_curses.png";

    // ═══════════════════════════════════════════
    // 7 诅咒 → 祝福（逆转为正面效果）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 祝福1：受到伤害 -50%
    /// 祝福2：对非 BOSS / BOSS 伤害 +25%
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != null && target.Player == Owner)
            return 0.5m;

        if (dealer != null && dealer.Player == Owner)
            return 1.25m;

        return 1m;
    }

    /// <summary>祝福3：获得格挡 +20%</summary>
    public override decimal ModifyBlockMultiplicative(
        Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target.Player == Owner)
            return 1.2m;
        return 1m;
    }

    /// <summary>祝福4：获得金币 +50%</summary>
    public override bool ShouldGainGold(decimal amount, Player player)
    {
        return GoldGuard.ShouldGainGold(amount, player);
    }

    public override async Task AfterGoldGained(Player player)
    {
        await GoldGuard.AfterGoldGained(player);
    }

    /// <summary>祝福5：休息处回复血量 +25%</summary>
    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
    {
        if (creature.Player == Owner || creature.PetOwner == Owner)
            return amount * 1.25m;
        return amount;
    }

    /// <summary>祝福6：击杀 BOSS 血量上限 +25%</summary>
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (room?.RoomType != RoomType.Boss || Owner == null)
            return;

        int maxHpGain = (int)Math.Ceiling(Owner.Creature.MaxHp * 0.25m);
        if (maxHpGain > 0)
        {
            Flash();
            await CreatureCmd.IncreaseMaxHp(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature, maxHpGain, isFromCard: false);
        }
    }

    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(
        Player player, IReadOnlyList<LocString> currentExtraText)
    {
        if (player != Owner)
            return currentExtraText;

        var list = new List<LocString>(currentExtraText);
        var extraText = new LocString(
            "relics",
            "ReverseRingOfSevenCurses.additionalRestSiteHealText");
        decimal baseHeal = (decimal)player.Creature.MaxHp * 0.3m;
        decimal actualHeal = baseHeal * 1.5m;
        int actualHealInt = (int)actualHeal;
        extraText.Add("ActualHeal", actualHealInt.ToString());
        list.Add(extraText);
        return list;
    }

    // ═══════════════════════════════════════════
    // 7 祝福部分逆转
    // ═══════════════════════════════════════════

    /// <summary>诅咒：抽牌 -1（原祝福 +1 逆转）</summary>
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player == Owner)
            return Math.Max(0, count - 1);
        return count;
    }

    /// <summary>祝福7：卡牌奖励 +1（原祝福 +1 保留）</summary>
    public override bool TryModifyRewards(
        Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || room == null)
            return false;

        if (room.RoomType is RoomType.Monster or RoomType.Boss or RoomType.Elite)
        {
            rewards.Add(new CardReward(
                CardCreationOptions.ForRoom(player, room.RoomType), 3, player));
        }
        return true;
    }

    /// <summary>祝福8：休息处升级卡牌 +1（原祝福 +1 保留）</summary>
    public override bool TryModifyRestSiteOptions(
        Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner)
            return false;

        var smithOption = options.OfType<SmithRestSiteOption>()
            .FirstOrDefault(opt => opt.Owner == Owner);
        if (smithOption != null)
        {
            smithOption.SmithCount += 1;
            return true;
        }
        return false;
    }

    // ═══════════════════════════════════════════

    public override RelicModel? GetUpgradeReplacement() => null;
}
