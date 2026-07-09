using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ShunMod.Core;

/// <summary>
///     自定义词条初始化 — 注册词条定义，由 Core 模块启动时调用。
/// </summary>
public static class ShunKeywordInit
{
    /// <summary>是否已初始化。</summary>
    private static bool _initialized;

    /// <summary>
    ///     注册所有内置自定义词条。
    ///     各模块在自身的 ModEntry.Initialize 末尾调用此方法即可。
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // ── 注册词条定义 ──
        CustomKeywordRegistry.DefineKeyword(
            "forever",
            "永远",
            "该卡牌永远都会在手牌中——如果存在于抽牌堆、弃牌堆或消耗堆，会直接返回手牌。",
            KeywordDisplayPosition.AfterDescription
        );

        Log.Info("[ShunMod_Core] Custom keywords initialized: forever");
    }
}