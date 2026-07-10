using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace ShunMod.Shun.UI;

/// <summary>
///     遗物交易所 Coordinator — 管理 Overlay 屏幕生命周期。
///     从事件触发 → 创建屏幕 → 推入 NOverlayStack → 等待选择 → 执行交易。
/// </summary>
public static class ShunModRelicExchangeCoordinator
{
    public static async Task ShowExchange(Player player)
    {
        if (NOverlayStack.Instance == null)
        {
            Log.Warn("[ShunMod_Shun] RelicExchange: NOverlayStack not available");
            return;
        }

        // 1. 等待 OverlayStack 就绪
        for (int i = 0; i < 60; i++)
        {
            if (NOverlayStack.Instance != null)
                break;
            await Task.Yield();
        }

        if (NOverlayStack.Instance == null)
        {
            Log.Warn("[ShunMod_Shun] RelicExchange: NOverlayStack still null after wait");
            return;
        }

        // 2. 创建屏幕
        var screen = new ShunModRelicExchangeScreen(player);

        // 3. 压入栈
        NOverlayStack.Instance.Push(screen);

        // 4. 等待选择结果
        var result = await screen.WaitForSelection();

        // 5. 执行交易
        if (result != null)
        {
            await ExecuteTrade(player, result);
        }

        // 6. 关闭屏幕
        if (NOverlayStack.Instance != null)
        {
            NOverlayStack.Instance.Remove(screen);
        }
    }

    private static async Task ExecuteTrade(Player player, RelicExchangeResult result)
    {
        if (result.LoseRelic == null) return;

        Log.Info($"[ShunMod_Shun] RelicExchange: trade {result.LoseRelic.Id.Entry} -> {result.GainRelic?.Id.Entry ?? result.GainEnchant?.Id.Entry}");

        await RelicCmd.Remove(result.LoseRelic);

        if (result.GainRelic != null)
        {
            var mutableGain = (RelicModel)result.GainRelic.MutableClone();
            await RelicCmd.Obtain(mutableGain, player);
        }
        else if (result.GainEnchant != null)
        {
            // 附魔模式：需要玩家选择一张卡牌附加
            // 这里触发 CardSelectCmd 选牌
            Log.Info("[ShunMod_Shun] RelicExchange: enchant mode - card selection needed");
            // TODO: 后续实现选牌附加附魔流程
        }
    }
}

/// <summary>
///     交易结果，包含失去的遗物和获得的奖励。
/// </summary>
public sealed record RelicExchangeResult(
    RelicModel LoseRelic,
    RelicModel? GainRelic,
    EnchantmentModel? GainEnchant);