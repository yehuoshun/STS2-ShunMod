using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core;
using ShunMod.Shun.Helpers;
using ShunMod.Core.Core.Registry;
using ShunMod.Shun.UI;

namespace ShunMod.Shun.Events;

/// <summary>
///     遗物交易所事件 — 自选模式。
///
///     设计决策：为什么用 ShunEventModel 而不是直接写 EventOption？
///     - ShunEventModel 是 Shun 自定义的事件基类，封装了事件生命周期管理、状态持久化等能力。
///     - 本事件本身不做 UI 渲染（「进入」选项触发后弹出 Overlay 屏幕），事件选项只作为入口。
///     - 真正的交易逻辑在 ShunModRelicExchangeScreen（Overlay 屏幕）中处理。
///
///     事件选项结构：
///     1. 「进入交易所」— 弹出 Overlay 屏幕，让玩家进行自选交易
///     2. 「扣血刷新」— 消耗 5 HP 重新随机事件选项（保留旧版功能，未实现实际 reroll 逻辑）
///     3. 「离开」— 什么都不做，直接退出事件
/// </summary>
[EventPool]
public class ShunModRelicExchange : ShunEventModel
{
    // ═══════════════════════════════════════════════════════════════
    //  静态数据
    // ═══════════════════════════════════════════════════════════════
    //
    //  可交易稀有度列表 — 与 ShunModRelicExchangeScreen 中的定义保持一致。
    //  为什么需要两处定义？
    //  - Event 层（本文件）需要 TradeableRarities 用于 BuildEnterHoverTips 的过滤。
    //  - Screen 层需要 TradeableRarities 用于 Roll 逻辑和 UI 过滤。
    //  - 两处代码是独立编译的，不共享静态成员（Event 在游戏事件系统里，Screen 在 UI 系统里）。
    //  - 如果后续需要统一，可以考虑提取到 Core 项目的静态配置类中。

    private static readonly HashSet<RelicRarity> TradeableRarities =
        [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop, RelicRarity.None];

    // ═══════════════════════════════════════════════════════════════
    //  事件入口
    // ═══════════════════════════════════════════════════════════════

    // CanonicalVars 和 CalculateVars 为空：
    // 本事件不使用动态变量（DynamicVar），所有文本在运行时通过 L10NLookup 直接获取。
    // 这两个是 ShunEventModel 的抽象成员，必须实现，所以返回空。
    protected override IEnumerable<DynamicVar> CanonicalVars => [];
    public override void CalculateVars() { }

    /// <summary>
    ///     生成事件初始选项。
    ///
    ///     选项 1「进入交易所」：
    ///     - 检查玩家是否有可交易的遗物。如果没有，切换到「没有可交易遗物」的状态页并显示离开按钮。
    ///     - 如果有，通过 ShunModRelicExchangeCoordinator 弹出 Overlay 屏幕。
    ///     - 在进入前通过 BuildEnterHoverTips 展示当前可交易遗物的悬浮提示，让玩家提前预览。
    ///
    ///     选项 2「扣血刷新」：
    ///     - 消耗 5 HP 重新随机事件选项（旧版功能，目前事件选项是固定的，后续可扩展为真正的 reroll）。
    ///     - 为什么用 LoseHpInternal 而不是 LoseHp？LoseHpInternal 是玩家内部方法，不触发伤害事件。
    ///       这样不会触发受伤特效、音效，仅扣血，对玩家来说是一种"代价"而非"伤害"。
    ///
    ///     选项 3「离开」：
    ///     - 直接返回，不做任何操作。
    /// </summary>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var player = Owner;

        return new List<EventOption>
        {
            // ── 选项 1：进入交易所 ──
            new(this, async () =>
            {
                if (player == null) return;
                var tradeable = player.Relics.Where(IsTradeable).ToList();
                if (tradeable.Count == 0)
                {
                    // 没有可交易的遗物时，切换到提示页面，只显示离开按钮
                    SetEventState(L10NLookup("pages.NO_TRADEABLE.description"),
                    [
                        new EventOption(this, static () => System.Threading.Tasks.Task.CompletedTask, "OPT_LEAVE")
                    ]);
                    return;
                }

                // 弹出 Overlay 屏幕，await 等待玩家完成选择
                await ShunModRelicExchangeCoordinator.ShowExchange(player);
            }, $"{Id.Entry}.pages.INITIAL.options.OPT_ENTER.title",
                BuildEnterHoverTips().ToArray()),

            // ── 选项 2：扣血刷新 ──
            // 保留旧版功能，让玩家可以消耗 HP 来期望更好的选项。
            // 注意：目前事件选项是固定的（GenerateInitialOptions 只在进入事件时调用一次），
            // 刷新后实际上不会重新生成选项。这是已知的遗留行为，后续可改进。
            new(this, async () =>
            {
                if (player == null) return;
                if (player.Creature.CurrentHp > 5)
                {
                    player.Creature.LoseHpInternal(5, 0);
                    Log.Info("[ShunMod_Shun] RelicExchange: HP refresh, options will re-roll on next entry");
                }
            }, $"{Id.Entry}.pages.INITIAL.options.OPT_REFRESH.title"),

            // ── 选项 3：离开 ──
            new(this, static () => System.Threading.Tasks.Task.CompletedTask, $"{Id.Entry}.pages.INITIAL.options.OPT_LEAVE.title"),
        };
    }

    /// <summary>
    ///     构建悬浮提示列表，用于在「进入交易所」选项上展示玩家可交易遗物的预览。
    ///
    ///     为什么需要这个？
    ///     - 玩家在点击「进入」之前就能看到自己有哪些遗物可以交易，帮助决策。
    ///     - 如果玩家没有可交易的遗物，直接在选项 1 上就看不到提示，点击后才会被重定向到提示页。
    /// </summary>
    private List<IHoverTip> BuildEnterHoverTips()
    {
        var player = Owner;
        if (player == null) return [];

        var tips = new List<IHoverTip>();
        var tradeable = player.Relics.Where(IsTradeable).ToList();
        foreach (var relic in tradeable)
        {
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(relic));
        }
        return tips;
    }

    /// <summary>判断遗物是否可交易，与 TradeableRarities 保持一致。</summary>
    private static bool IsTradeable(RelicModel r) => TradeableRarities.Contains(r.Rarity);
}