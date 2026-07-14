using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace ShunMod.Shun.UI;

/// <summary>
///     遗物交易所 Overlay 屏幕。
///
///     设计决策：为什么用 Overlay 屏幕而不是原生 EventUI？
///     - 原生 EventUI 的选项布局是固定的（文本+按钮列表），无法展示多张遗物卡片供玩家选择。
///     - Overlay 屏幕覆盖在游戏画面之上，可以自由构建复杂布局（卡片网格、滚动、状态提示等）。
///     - 实现 IOverlayScreen + IScreenContext 使本屏幕可以压入游戏屏幕栈，与游戏原生 UI 共存。
///
///     交互流程：
///     1. 玩家选择要卖掉的遗物（上半部分）
///     2. 玩家选择要获得的奖励（下半部分：遗物 or 附魔）
///     3. 点击确认按钮完成交易
///     4. 选择结果通过 TaskCompletionSource 异步返回给调用方
/// </summary>
internal sealed partial class ShunModRelicExchangeScreen : Control, IOverlayScreen, IScreenContext
{
    // ═══════════════════════════════════════════════════════════════
    //  常量
    // ═══════════════════════════════════════════════════════════════
    //
    //  为什么用常量而不是配置？
    //  - 这些是 UI 布局的硬数值，在运行时不会变化。
    //  - 放在常量区避免 magic number 散落在代码各处，便于统一调参。
    //  - 如果未来需要支持 UI 缩放或分辨率适配，从这里改即可。

    /// <summary>内容面板尺寸（1180×780），适配 16:9 分辨率下不挡住游戏画面。</summary>
    private static readonly Vector2 ScreenSize = new(1180f, 780f);

    /// <summary>单张卡片尺寸（200×280），三张以上可横向滚动查看。</summary>
    private static readonly Vector2 CardSize = new(200f, 280f);

    /// <summary>卡片之间的间距，视觉上区分每张卡片。</summary>
    private static readonly int CardSeparation = 20;

    // 颜色常量 — 统一色调，避免 UI 界面颜色不一致
    private static readonly Color DimColor = new(0.02f, 0.025f, 0.035f, 0.56f);   // 半透明黑色遮罩
    private static readonly Color PanelBg = new(0.04f, 0.05f, 0.08f, 0.4f);        // 面板背景（深色半透明）
    private static readonly Color PanelBorder = new(0.48f, 0.55f, 0.66f, 0.35f);   // 面板边框（灰蓝）
    private static readonly Color AccentGold = new(0.94f, 0.76f, 0.35f);            // 强调色（金色），用于边框和选中态
    private static readonly Color TextPrimary = new(0.96f, 0.97f, 0.99f, 0.98f);    // 主文字色（白）
    private static readonly Color TextBody = new(0.9f, 0.93f, 0.97f, 0.92f);        // 正文色（灰白）
    private static readonly Color CardBg = new(0.08f, 0.1f, 0.14f, 0.74f);          // 卡片背景（深色半透明）
    private static readonly Color CardBgHover = new(0.1f, 0.12f, 0.18f, 0.84f);     // 卡片悬停背景
    private static readonly Color CardBgDisabled = new(0.08f, 0.09f, 0.12f, 0.62f); // 卡片禁用背景

    /// <summary>
    ///     可交易的遗物稀有度列表。
    ///     排除了 Starter（初始遗物）和 Boss/BossRare（Boss 遗物太强，交易会破坏平衡）。
    ///     None 稀有度是某些特殊遗物（如诅咒遗物）的兜底，允许交易可以增加策略深度。
    /// </summary>
    private static readonly HashSet<RelicRarity> TradeableRarities =
        [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop, RelicRarity.None];

    // ═══════════════════════════════════════════════════════════════
    //  字段
    // ═══════════════════════════════════════════════════════════════

    // 为什么用 TaskCompletionSource 而不是回调？
    // - 交易所的调用方（ShunModRelicExchangeCoordinator）需要等待玩家完成选择后才继续执行。
    // - TaskCompletionSource 提供 awaitable 的异步模型，比回调更简洁，且天然支持取消/超时。
    private readonly Player _player;
    private readonly TaskCompletionSource<RelicExchangeResult?> _completionSource = new();

    // 预生成的奖励选项（在构造时确定，避免玩家通过反复打开/关闭来 reroll）
    private readonly List<RelicModel> _gainOptions = [];
    private readonly List<EnchantmentModel> _enchantOptions = [];

    // 当前玩家选择状态
    private RelicModel? _selectedLoseRelic;     // 玩家选中要卖掉的遗物
    private RelicModel? _selectedGainRelic;     // 玩家选中要获得的遗物（与附魔互斥）
    private EnchantmentModel? _selectedGainEnchant; // 玩家选中要获得的附魔（与遗物互斥）
    private bool _choiceLocked;                 // 确认后锁定，防止重复触发

    // UI 节点缓存 — 避免每次更新都重新遍历查找，减少 GC 压力
    // 为什么不用 Godot 的 GetNode/FindChild？
    // - 场景完全由代码构建，没有 .tscn 文件，节点路径不固定。
    // - 缓存引用比每次 GetNode 更快，尤其是在 Rebuild 频繁触发的场景下。
    private HBoxContainer? _playerRelicsRow;
    private HBoxContainer? _rewardRow;
    private Button? _confirmButton;
    private Button? _cancelButton;
    private MegaLabel? _statusLabel;

    // ═══════════════════════════════════════════════════════════════
    //  属性 — IOverlayScreen 接口实现
    // ═══════════════════════════════════════════════════════════════

    // ScreenType 设为 Rewards 类型，让游戏知道这是一个奖励类屏幕（影响背景暗化、阴影等渲染效果）。
    public NetScreenType ScreenType => NetScreenType.Rewards;

    // UseSharedBackstop = true 表示复用游戏已有的返回遮罩，不需要本屏幕自己处理返回键。
    public bool UseSharedBackstop => true;

    // 不设置默认焦点，让玩家可以自由点击任意卡片。
    public Control? DefaultFocusedControl => null;

    // ═══════════════════════════════════════════════════════════════
    //  构造
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    ///     构造时先 Roll 奖励选项，再构建 UI。
    ///     顺序为什么不能反？
    ///     - BuildUi 最后会调用 RebuildPlayerRelics() 和 RebuildRewards()，
    ///       如果此时 _gainOptions / _enchantOptions 还没填充，卡片区域就是空的。
    ///     - 所以 RollGainOptions() 必须在 BuildUi() 之前执行。
    /// </summary>
    internal ShunModRelicExchangeScreen(Player player)
    {
        _player = player;
        Name = "ShunModRelicExchange";
        RollGainOptions();
        BuildUi();
    }

    /// <summary>工厂方法，创建并返回屏幕实例。</summary>
    public static ShunModRelicExchangeScreen Create(Player player)
    {
        return new ShunModRelicExchangeScreen(player);
    }

    /// <summary>异步等待玩家选择结果。</summary>
    public Task<RelicExchangeResult?> WaitForSelection()
    {
        return _completionSource.Task;
    }

    // ═══════════════════════════════════════════════════════════════
    //  IOverlayScreen 接口实现 — 生命周期钩子
    // ═══════════════════════════════════════════════════════════════
    //
    //  Overlay 屏幕有四个生命周期事件：打开/关闭/显示/隐藏。
    //  本屏幕不需要在这些节点做额外操作，但接口要求实现，所以保留空方法体。
    //  如果以后需要进场动画、音效等，可以在这里补。

    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed() { }
    public void AfterOverlayShown() { }
    public void AfterOverlayHidden() { }

    // ═══════════════════════════════════════════════════════════════
    //  Roll 逻辑 — 奖励池生成
    // ═══════════════════════════════════════════════════════════════
    //
    //  为什么 Roll 3 个遗物 + 2 个附魔？
    //  - 3 个遗物选项让玩家有足够选择空间，但不会太多导致选择困难。
    //  - 2 个附魔选项作为补充，让玩家在遗物不满意时有替代方案。
    //  - 总选项数 5 个，在一行内展示（卡片宽度 320px × 5 = 1600px，略超面板宽度，
    //    但用 ScrollContainer 支持横向滚动，不强制全部可见）。

    private void RollGainOptions()
    {
        _gainOptions.Clear();
        _enchantOptions.Clear();

        var player = _player;
        if (player == null) return;

        // 滚 3 个遗物选项
        // 循环上限 relicCount * 3：防止在极端情况下（可用遗物很少且重复碰撞）无限循环。
        // 只要集齐 3 个不重复的遗物就提前退出。
        int relicCount = 3;
        for (int i = 0; i < relicCount * 3 && _gainOptions.Count < relicCount; i++)
        {
            var relic = RollRandomRelic();
            if (relic == null) continue;
            if (_gainOptions.Any(r => r.Id == relic.Id)) continue;
            if (relic.Rarity == RelicRarity.Starter) continue;
            _gainOptions.Add(relic);
        }

        // 滚 2 个附魔选项
        // 附魔池最多 5 个候选，从中随机取 2 个不重复的。
        int enchantCount = 2;
        var enchants = GetEnchantPool();
        var rolled = new HashSet<string>(); // 用 HashSet 去重，比 List.Contains 更快
        for (int i = 0; i < enchants.Count && _enchantOptions.Count < enchantCount; i++)
        {
            var enchant = enchants[i];
            if (rolled.Add(enchant.Id.Entry))
                _enchantOptions.Add(enchant);
        }
    }

    /// <summary>
    ///     从全量遗物库中随机选一个可用遗物。
    ///     为什么用 try/catch？
    ///     - ModelDb.AllRelics 可能在游戏未完全加载时抛出异常（如动态加载 mod 遗物时）。
    ///     - 捕获异常后返回 null，由调用方决定跳过还是重试。
    /// </summary>
    private static RelicModel? RollRandomRelic()
    {
        try
        {
            return ModelDb.AllRelics
                .Where(r => TradeableRarities.Contains(r.Rarity) && r.Rarity != RelicRarity.Starter)
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Log.Warn($"[ShunMod_Shun] RelicExchange.RollRandomRelic: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     获取可用附魔池。
    ///
    ///     为什么用反射遍历所有 EnchantmentModel 子类？
    ///     - 附魔是通过游戏注册表管理的，但 STS2 没有提供"获取所有已注册附魔"的 API。
    ///     - 通过反射枚举程序集中所有 EnchantmentModel 的非抽象子类，可以自动包含所有 mod 的附魔。
    ///     - 每个附魔实例化后检查是否可添加，无法实例化的跳过（某些附魔有特殊构造参数）。
    ///
    ///     为什么不设黑名单？
    ///     - 之前有一个 EnchantBlacklist 排除特定附魔，但附魔本身是遗物增强机制，没有理由禁止玩家选择。
    ///       玩家在不同 build 中对附魔的需求不同，应该由玩家自己判断，而不是 mod 作者替玩家做决定。
    ///     - 如果某个附魔确实有问题（如游戏崩溃），应该在游戏层面修复，而不是在 mod 里屏蔽。
    /// </summary>
    private static List<EnchantmentModel> GetEnchantPool()
    {
        try
        {
            var enchantTypes = typeof(EnchantmentModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t))
                .ToList();

            var result = new List<EnchantmentModel>();
            foreach (var type in enchantTypes)
            {
                try
                {
                    var enchant = (EnchantmentModel)Activator.CreateInstance(type)!;
                    result.Add(enchant);
                }
                catch
                {
                    // 跳过无法实例化的附魔：
                    // 某些附魔的构造函数需要参数（如传入稀有度、ID 等），
                    // 用无参构造会抛异常，直接跳过不处理。
                }
            }

            // 随机打乱后取前 5 个，避免每次打开交易所都是同一个排序。
            return result.OrderBy(_ => Random.Shared.Next()).Take(5).ToList();
        }
        catch (Exception ex)
        {
            Log.Warn($"[ShunMod_Shun] RelicExchange.GetEnchantPool: {ex.Message}");
            return [];
        }
    }

    /// <summary>判断遗物是否可交易（与 TradeableRarities 保持一致）。</summary>
    private static bool IsTradeable(RelicModel r) => TradeableRarities.Contains(r.Rarity);

    // ═══════════════════════════════════════════════════════════════
    //  UI 构建 — 纯代码布局
    // ═══════════════════════════════════════════════════════════════
    //
    //  为什么不用 Godot 的 .tscn 场景文件？
    //  - 这个屏幕是动态创建的，不需要在编辑器中预览。
    //  - 纯代码构建避免 .tscn 文件与代码解耦，减少文件数量，功能更内聚。
    //  - 如果以后需要可视化编辑，可以将这部分提取到 .tscn 并用代码加载。
    //
    //  布局结构（从上到下）：
    //  1. 遮罩层（DimOverlay）— 半透明黑色，覆盖背景，让玩家聚焦于弹窗
    //  2. 居中容器（ScreenCenter）— 将内容面板居中
    //  3. 内容面板（ContentPanel）— 带圆角和阴影的面板容器
    //  4. 外边距（ContentMargin）— 面板内边距
    //  5. 垂直布局（Root VBox）— 从上到下排列：标题→遗物行→奖励行→状态→按钮

    private void BuildUi()
    {
        // 遮罩层：覆盖整个屏幕，半透明暗色，阻止点击穿透到下层 UI。
        // MouseFilter = Stop 确保点击遮罩层不会穿透到游戏画面。
        ColorRect backdrop = new()
        {
            Name = "DimOverlay",
            Color = DimColor,
            MouseFilter = MouseFilterEnum.Stop
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        // 居中容器：让内容面板在屏幕中央显示，不依赖绝对定位。
        // MouseFilter = Ignore 让点击事件穿透到遮罩层（Stop）而不是被容器拦截。
        CenterContainer screenCenter = new()
        {
            Name = "ScreenCenter",
            MouseFilter = MouseFilterEnum.Ignore
        };
        screenCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(screenCenter);

        // 内容面板：带圆角和阴影的容器，视觉上"浮"在游戏画面之上。
        // 使用 PanelContainer 而不是九宫格背景图，因为：
        // - 不需要额外资源文件
        // - 颜色和边框可以通过 StyleBoxFlat 动态调整
        // - 适配不同屏幕比例时不会变形
        PanelContainer contentPanel = new()
        {
            Name = "ContentPanel",
            CustomMinimumSize = ScreenSize,
            MouseFilter = MouseFilterEnum.Ignore
        };
        contentPanel.AddThemeStyleboxOverride("panel", CreateContentPanelStyle());
        screenCenter.AddChild(contentPanel);

        // 外边距：面板内部留白，让内容不紧贴边框。
        MarginContainer contentMargin = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        contentMargin.AddThemeConstantOverride("margin_left", 30);
        contentMargin.AddThemeConstantOverride("margin_right", 30);
        contentMargin.AddThemeConstantOverride("margin_top", 28);
        contentMargin.AddThemeConstantOverride("margin_bottom", 28);
        contentPanel.AddChild(contentMargin);

        // 总根 VBox：垂直排列所有子区域。
        // AlignmentMode.Center 让所有子节点在垂直方向上居中排列。
        VBoxContainer root = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 16);
        contentMargin.AddChild(root);

        // ── 标题 ──
        // 使用 MegaLabel（带自动缩放功能的文本控件），支持 MinFontSize ~ MaxFontSize 自适应。
        // 当面板宽度不足时自动缩小字号，避免文字超出。
        MegaLabel title = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 42,
            MinFontSize = 28
        };
        title.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        title.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        title.AddThemeConstantOverride("outline_size", 1);
        title.Modulate = TextPrimary;
        title.SetTextAutoSize("遗物交易所");
        root.AddChild(title);

        // ── 上半部分：你的遗物 ──
        MegaLabel relicsTitle = new()
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 24,
            MinFontSize = 18
        };
        relicsTitle.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        relicsTitle.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        relicsTitle.AddThemeConstantOverride("outline_size", 1);
        relicsTitle.Modulate = TextPrimary;
        relicsTitle.SetTextAutoSize("选择要卖掉的遗物");
        root.AddChild(relicsTitle);

        // 玩家遗物行：用 ScrollContainer 包裹，不要紧卡片太多时溢出面板。
        // 为什么用 ScrollContainer 而不是 GridContainer？
        // - 卡片数量不固定（玩家遗物数量可能多于 5 个）。
        // - ScrollContainer 支持横向滚动，所有卡片都可见，不压缩。
        ScrollContainer relicScroll = new()
        {
            Name = "PlayerRelicsScroll",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, CardSize.Y + 40f)
        };
        ApplyScrollbarStyle(relicScroll);
        root.AddChild(relicScroll);

        _playerRelicsRow = new HBoxContainer()
        {
            Name = "PlayerRelicsRow",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _playerRelicsRow.AddThemeConstantOverride("separation", CardSeparation);
        relicScroll.AddChild(_playerRelicsRow);

        // ── 下半部分：可选奖励 ──
        MegaLabel rewardTitle = new()
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 24,
            MinFontSize = 18
        };
        rewardTitle.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        rewardTitle.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        rewardTitle.AddThemeConstantOverride("outline_size", 1);
        rewardTitle.Modulate = TextPrimary;
        rewardTitle.SetTextAutoSize("选择要获得的奖励");
        root.AddChild(rewardTitle);

        // 奖励行：同样用 ScrollContainer 包裹（遗物卡片 + 附魔卡片混排）。
        ScrollContainer rewardScroll = new()
        {
            Name = "RewardScroll",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, CardSize.Y + 40f)
        };
        ApplyScrollbarStyle(rewardScroll);
        root.AddChild(rewardScroll);

        _rewardRow = new HBoxContainer()
        {
            Name = "RewardRow",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _rewardRow.AddThemeConstantOverride("separation", CardSeparation);
        rewardScroll.AddChild(_rewardRow);

        // ── 状态提示 ──
        // 初始不可见，玩家进行选择后显示当前操作状态。
        // 使用 MegaLabel 保证文字在不同屏幕尺寸下可读。
        _statusLabel = new MegaLabel()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 20,
            MinFontSize = 15,
            Visible = false
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        _statusLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        _statusLabel.AddThemeConstantOverride("outline_size", 1);
        _statusLabel.Modulate = new Color(0.88f, 0.92f, 0.97f, 0.82f);
        root.AddChild(_statusLabel);

        // ── 按钮行 ──
        // 两个按钮：离开（取消）和确认交易。
        // 确认按钮在玩家未完成选择时处于 Disabled 状态，防止误触。
        HBoxContainer buttonRow = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        buttonRow.AddThemeConstantOverride("separation", 20);
        root.AddChild(buttonRow);

        _cancelButton = new Button()
        {
            Text = "离开",
            CustomMinimumSize = new Vector2(160f, 48f)
        };
        StyleButton(_cancelButton, new Color(0.6f, 0.3f, 0.3f)); // 红色主题，暗示"放弃"
        _cancelButton.Pressed += OnCancel;
        buttonRow.AddChild(_cancelButton);

        _confirmButton = new Button()
        {
            Text = "确认交易",
            CustomMinimumSize = new Vector2(200f, 48f),
            Disabled = true // 初始禁用，玩家选择后才启用
        };
        StyleButton(_confirmButton, AccentGold); // 金色主题，暗示"有价值"
        _confirmButton.Pressed += OnConfirm;
        buttonRow.AddChild(_confirmButton);

        // 填充卡片内容
        RebuildPlayerRelics();
        RebuildRewards();
    }

    // ═══════════════════════════════════════════════════════════════
    //  重建方法 — 在交互变更时刷新 UI
    // ═══════════════════════════════════════════════════════════════
    //
    //  为什么用"重建"而不是"更新"？
    //  - 卡片选中状态变化时，需要更新整张卡片的边框样式和选中视觉效果。
    //  - 逐个更新节点属性比较麻烦（需要遍历并找到对应的卡片），
    //    重新创建子节点虽然开销稍大，但逻辑清晰，且玩家交互频率低，性能不是问题。
    //  - 注意：重建前先 RemoveChild + QueueFree 清理旧节点，避免内存泄漏。

    private void RebuildPlayerRelics()
    {
        if (_playerRelicsRow == null) return;

        foreach (Node child in _playerRelicsRow.GetChildren())
        {
            _playerRelicsRow.RemoveChild(child);
            child.QueueFree();
        }

        var tradeableRelics = _player.Relics.Where(IsTradeable).ToList();
        foreach (var relic in tradeableRelics)
        {
            var card = CreateRelicCard(relic, isPlayerRelic: true, isSelected: relic == _selectedLoseRelic);
            _playerRelicsRow.AddChild(card);
        }
    }

    private void RebuildRewards()
    {
        if (_rewardRow == null) return;

        foreach (Node child in _rewardRow.GetChildren())
        {
            _rewardRow.RemoveChild(child);
            child.QueueFree();
        }

        // 遗物奖励卡片
        foreach (var relic in _gainOptions)
        {
            bool isSelected = relic == _selectedGainRelic;
            var card = CreateRelicCard(relic, isPlayerRelic: false, isSelected: isSelected);
            _rewardRow.AddChild(card);
        }

        // 附魔奖励卡片（与遗物奖励混排，让玩家可以自由对比）
        foreach (var enchant in _enchantOptions)
        {
            bool isSelected = enchant == _selectedGainEnchant;
            var card = CreateEnchantCard(enchant, isSelected);
            _rewardRow.AddChild(card);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  卡片创建 — 遗物卡片 & 附魔卡片
    // ═══════════════════════════════════════════════════════════════
    //
    //  为什么把遗物和附魔分成两个方法？
    //  - 遗物卡片有图标 Texture2D，附魔卡片没有图标（用 ⚡ 符号代替）。
    //  - 两者布局相似但细节不同（颜色、数据源、点击事件），分开更清晰。
    //  - 如果未来需要为附魔卡片加图标，可以独立修改而不影响遗物卡片。

    /// <summary>创建一张遗物卡片（可点击选中）。</summary>
    /// <param name="relic">要展示的遗物。</param>
    /// <param name="isPlayerRelic">是否为玩家拥有的遗物（影响点击事件绑定）。</param>
    /// <param name="isSelected">是否已选中（影响边框样式）。</param>
    private Control CreateRelicCard(RelicModel relic, bool isPlayerRelic, bool isSelected)
    {
        // 外层 slot 容器：固定卡片尺寸，防止内容撑大。
        Control slot = new()
        {
            Name = $"RelicCard_{relic.Id.Entry}",
            CustomMinimumSize = CardSize,
            MouseFilter = MouseFilterEnum.Ignore
        };

        // Button 作为可点击区域：覆盖整个卡片区域。
        // 为什么用 Button 而不是 Area2D 或 Control + 事件？
        // - Button 自带焦点管理、悬停效果、键盘导航等特性。
        // - Godot 的 UI 系统对 Button 有原生支持，样式切换更方便。
        Button button = new()
        {
            CustomMinimumSize = CardSize,
            Text = string.Empty,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            ClipContents = false
        };
        button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ApplyCardStyle(button, isSelected);

        // 卡片内部布局：MarginContainer → VBoxContainer
        // MarginContainer 提供内边距，VBoxContainer 垂直排列图标→名称→描述。
        MarginContainer margin = new();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        button.AddChild(margin);

        VBoxContainer content = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);

        // 图标区域：固定高度 80px，居中显示。
        // 优先使用 BigIcon（大图标），没有则降级到 Icon（小图标）。
        CenterContainer iconBox = new()
        {
            CustomMinimumSize = new Vector2(0f, 80f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(iconBox);

        Texture2D? icon = relic.BigIcon ?? relic.Icon;
        if (icon != null)
        {
            TextureRect tex = new()
            {
                MouseFilter = MouseFilterEnum.Ignore,
                Texture = icon,
                CustomMinimumSize = new Vector2(64f, 64f),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };
            iconBox.AddChild(tex);
            AttachRelicHoverTips(tex, relic);
        }

        // 名称：自动换行，字号自适应。
        MegaLabel nameLabel = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 18,
            MinFontSize = 12
        };
        nameLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        nameLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        nameLabel.AddThemeConstantOverride("outline_size", 1);
        nameLabel.Modulate = TextPrimary;
        nameLabel.SetTextAutoSize(relic.Title.GetFormattedText());
        content.AddChild(nameLabel);

        // 描述：使用 MegaRichTextLabel 支持 BBCode 富文本样式。
        // 大多数遗物描述包含动态变量（如数值），GetFormattedText() 会替换这些变量。
        MegaRichTextLabel desc = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxFontSize = 14,
            MinFontSize = 10,
            BbcodeEnabled = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        desc.AddThemeColorOverride("default_color", new Color(0.9f, 0.93f, 0.97f, 0.92f));
        desc.AddThemeColorOverride("default_color", TextBody);
        desc.SetTextAutoSize(relic.DynamicDescription.GetFormattedText());
        content.AddChild(desc);

        // 点击事件绑定
        if (isPlayerRelic)
        {
            button.Pressed += () => OnPlayerRelicSelected(relic);
        }
        else
        {
            button.Pressed += () => OnRewardSelected(relic, null);
        }

        slot.AddChild(button);
        return slot;
    }

    /// <summary>创建一张附魔卡片（与遗物卡片视觉区分）。</summary>
    private Control CreateEnchantCard(EnchantmentModel enchant, bool isSelected)
    {
        // 结构同 CreateRelicCard，但附魔没有图标，用 ⚡ 符号代替。
        Control slot = new()
        {
            Name = $"EnchantCard_{enchant.Id.Entry}",
            CustomMinimumSize = CardSize,
            MouseFilter = MouseFilterEnum.Ignore
        };

        Button button = new()
        {
            CustomMinimumSize = CardSize,
            Text = string.Empty,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        button.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ApplyCardStyle(button, isSelected);

        MarginContainer margin = new();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        button.AddChild(margin);

        VBoxContainer content = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);

        // 附魔图标（⚡ 符号，绿色调，与遗物金色调区分）
        CenterContainer iconBox = new()
        {
            CustomMinimumSize = new Vector2(0f, 80f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(iconBox);

        MegaLabel enchantLabel = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxFontSize = 28,
            MinFontSize = 18
        };
        enchantLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        enchantLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        enchantLabel.AddThemeConstantOverride("outline_size", 1);
        enchantLabel.Modulate = new Color(0.6f, 0.9f, 0.6f); // 绿色调
        enchantLabel.SetTextAutoSize("⚡");
        iconBox.AddChild(enchantLabel);

        // 附魔名称（绿色文字，与遗物白色文字区分）
        MegaLabel nameLabel = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 18,
            MinFontSize = 12
        };
        nameLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        nameLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        nameLabel.AddThemeConstantOverride("outline_size", 1);
        nameLabel.Modulate = new Color(0.6f, 0.9f, 0.6f);
        nameLabel.SetTextAutoSize(enchant.Title.GetFormattedText());
        content.AddChild(nameLabel);

        // 附魔描述
        MegaRichTextLabel desc = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxFontSize = 14,
            MinFontSize = 10,
            BbcodeEnabled = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        desc.AddThemeColorOverride("default_color", new Color(0.9f, 0.93f, 0.97f, 0.92f));
        desc.AddThemeColorOverride("default_color", TextBody);
        desc.SetTextAutoSize(enchant.DynamicDescription.GetFormattedText());
        content.AddChild(desc);

        button.Pressed += () => OnRewardSelected(null, enchant);
        slot.AddChild(button);
        return slot;
    }

    // ═══════════════════════════════════════════════════════════════
    //  样式 — StyleBoxFlat 创建方法
    // ═══════════════════════════════════════════════════════════════
    //
    //  为什么用 StyleBoxFlat 而不是 .theme / .tres 资源文件？
    //  - 纯代码构建，不需要额外资源文件，便于分发。
    //  - StyleBoxFlat 支持运行时动态调整颜色（如选中态切换边框颜色）。
    //  - 所有样式统一由这三个方法管理，调参入口集中。

    /// <summary>创建内容面板的 StyleBox（圆角 + 阴影）。</summary>
    private static StyleBoxFlat CreateContentPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 28,
            CornerRadiusTopRight = 28,
            CornerRadiusBottomLeft = 28,
            CornerRadiusBottomRight = 28,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            ShadowColor = new Color(0f, 0f, 0f, 0.26f),
            ShadowSize = 18,
            ShadowOffset = new Vector2(0f, 10f)
        };
    }

    /// <summary>创建单张卡片的 StyleBox（可指定背景色、边框色、边框宽度）。</summary>
    private static StyleBoxFlat CreateCardStyle(Color background, Color border, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 20,
            CornerRadiusTopRight = 20,
            CornerRadiusBottomLeft = 20,
            CornerRadiusBottomRight = 20,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            ShadowColor = new Color(0f, 0f, 0f, 0.18f),
            ShadowSize = 12,
            ShadowOffset = new Vector2(0f, 8f)
        };
    }

    /// <summary>
    ///     应用卡片样式到 Button。
    ///     选中态的卡片边框更粗 + 绿色边框，未选中态为金色边框。
    ///     为什么是绿色表示选中？
    ///     - 绿色在视觉上表示"确认/已选"，与金色（默认高亮色）形成对比，让选中状态一目了然。
    /// </summary>
    private void ApplyCardStyle(Button button, bool isSelected)
    {
        Color border = isSelected ? new Color(0.3f, 0.8f, 0.3f) : AccentGold;
        int borderWidth = isSelected ? 4 : 2;

        button.AddThemeStyleboxOverride("normal", CreateCardStyle(CardBg, border, borderWidth));
        button.AddThemeStyleboxOverride("hover", CreateCardStyle(CardBgHover, AccentGold, 3));
        button.AddThemeStyleboxOverride("pressed", CreateCardStyle(new Color(0.07f, 0.09f, 0.13f, 0.9f), AccentGold.Lightened(0.14f), 4));
        button.AddThemeStyleboxOverride("focus", CreateCardStyle(CardBgHover, AccentGold, 3));
        button.AddThemeStyleboxOverride("disabled", CreateCardStyle(CardBgDisabled, AccentGold.Darkened(0.4f), 2));
    }

    /// <summary>为 ScrollContainer 添加半透明水平滚动条。</summary>
    private static void ApplyScrollbarStyle(ScrollContainer container)
    {
        container.AddThemeStyleboxOverride("scrollbar", new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.22f, 0.3f, 0.35f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });
        container.AddThemeStyleboxOverride("grabber", new StyleBoxFlat
        {
            BgColor = new Color(0.5f, 0.6f, 0.7f, 0.65f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });
        container.AddThemeStyleboxOverride("grabber_highlight", new StyleBoxFlat
        {
            BgColor = new Color(0.6f, 0.7f, 0.85f, 0.8f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });
    }

    /// <summary>
    ///     为操作按钮（确认/离开）应用样式。
    ///     按钮的基调色由调用方传入（accent 参数），实现不同颜色的按钮。
    ///     为什么不用一个统一的样式？
    ///     - 离开按钮是红色调，确认按钮是金色调，不同颜色传达不同语义。
    ///     - 通过 accent 参数复用同一个方法，避免重复代码。
    /// </summary>
    private static void StyleButton(Button button, Color accent)
    {
        StyleBoxFlat normal = new()
        {
            BgColor = new Color(accent.R * 0.3f, accent.G * 0.3f, accent.B * 0.3f, 0.6f),
            BorderColor = accent,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        };
        StyleBoxFlat hover = new()
        {
            BgColor = new Color(accent.R * 0.5f, accent.G * 0.5f, accent.B * 0.5f, 0.8f),
            BorderColor = accent.Lightened(0.2f),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        };
        // 按下态和禁用态复用 normal 样式，不额外处理。
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", normal);
        button.AddThemeStyleboxOverride("focus", hover);
        button.AddThemeStyleboxOverride("disabled", normal);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Hover 提示 — 暂未实现
    // ═══════════════════════════════════════════════════════════════
    //
    //  为什么留空而不是直接不调用？
    //  - CreateRelicCard 中已经调用了 AttachRelicHoverTips，如果这个方法不存在，编译会报错。
    //  - 后续实现可以用 NHoverTipSet.CreateAndShow 展示遗物的悬浮提示信息。
    //  - 参考：NHoverTipSet.CreateAndShow(holder, relic.HoverTips, ...)

    private static void AttachRelicHoverTips(Control holder, RelicModel relic)
    {
    }

    // ═══════════════════════════════════════════════════════════════
    //  交互处理
    // ═══════════════════════════════════════════════════════════════
    //
    //  设计模式：点击=切换选中状态
    //  - 点击已选中的卡片 → 取消选中（toggle 行为，允许玩家反悔）。
    //  - 点击未选中的卡片 → 选中它。
    //  - 为什么不用"点击已选中的卡片不做任何事"？
    //    因为玩家可能想取消当前选择，重新选另一个。toggle 行为更自然。

    /// <summary>处理玩家遗物点击事件（选择/取消选择要卖掉的遗物）。</summary>
    private void OnPlayerRelicSelected(RelicModel relic)
    {
        if (_choiceLocked) return;

        // 允许取消选择：点击已选中的遗物 -> 取消选中
        if (_selectedLoseRelic == relic)
            _selectedLoseRelic = null;
        else
            _selectedLoseRelic = relic;

        UpdateVisualState();
        RebuildPlayerRelics();
    }

    /// <summary>处理奖励点击事件（选择/取消选择要获得的奖励）。</summary>
    private void OnRewardSelected(RelicModel? relic, EnchantmentModel? enchant)
    {
        if (_choiceLocked || _selectedLoseRelic == null) return;

        // 先卖掉遗物才能选奖励，逻辑上约束玩家操作顺序。
        // 如果玩家还没选要卖掉的遗物，点击奖励不做任何反应。

        // 允许取消选择
        if (_selectedGainRelic == relic && _selectedGainEnchant == enchant)
        {
            _selectedGainRelic = null;
            _selectedGainEnchant = null;
        }
        else
        {
            _selectedGainRelic = relic;
            _selectedGainEnchant = enchant;
        }

        UpdateVisualState();
        RebuildRewards();
    }

    /// <summary>确认交易，锁定选择并返回结果。</summary>
    private void OnConfirm()
    {
        // 双重校验：确保选择是完整的（卖掉的遗物 + 获得的奖励）
        if (_choiceLocked || _selectedLoseRelic == null) return;
        if (_selectedGainRelic == null && _selectedGainEnchant == null) return;

        _choiceLocked = true;
        _completionSource.TrySetResult(new RelicExchangeResult(
            _selectedLoseRelic,
            _selectedGainRelic,
            _selectedGainEnchant));
    }

    /// <summary>取消交易，返回 null 表示玩家放弃。</summary>
    private void OnCancel()
    {
        _choiceLocked = true;
        _completionSource.TrySetResult(null);
    }

    /// <summary>
    ///     更新按钮状态和提示文字。
    ///     根据选择进度动态显示提示，引导玩家完成操作。
    ///     确认按钮只有在"卖掉的遗物"和"获得的奖励"都选好后才启用。
    /// </summary>
    private void UpdateVisualState()
    {
        bool canConfirm = _selectedLoseRelic != null &&
                          (_selectedGainRelic != null || _selectedGainEnchant != null);

        if (_confirmButton != null)
            _confirmButton.Disabled = !canConfirm;

        if (_statusLabel != null)
        {
            if (_selectedLoseRelic == null)
                _statusLabel.SetTextAutoSize("选择要卖掉的遗物");
            else if (_selectedGainRelic == null && _selectedGainEnchant == null)
                _statusLabel.SetTextAutoSize("选择要获得的奖励");
            else
                _statusLabel.SetTextAutoSize("确认交易");
            _statusLabel.Visible = true;
        }
    }
}