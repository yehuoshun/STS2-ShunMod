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
///     玩家选择要卖掉的遗物，再从随机列出的奖励中选一个获得。
///     实现 IOverlayScreen + IScreenContext 以集成到游戏屏幕栈。
/// </summary>
internal sealed partial class ShunModRelicExchangeScreen : Control, IOverlayScreen, IScreenContext
{
    // ═══════════════════════════════════════════════════════════
    //  常量
    // ═══════════════════════════════════════════════════════════

    private static readonly Vector2 ScreenSize = new(1180f, 780f);
    private static readonly Vector2 CardSize = new(320f, 440f);
    private static readonly int CardSeparation = 20;
    private static readonly Color DimColor = new(0.02f, 0.025f, 0.035f, 0.56f);
    private static readonly Color PanelBg = new(0.04f, 0.05f, 0.08f, 0.4f);
    private static readonly Color PanelBorder = new(0.48f, 0.55f, 0.66f, 0.35f);
    private static readonly Color AccentGold = new(0.94f, 0.76f, 0.35f);
    private static readonly Color TextPrimary = new(0.96f, 0.97f, 0.99f, 0.98f);
    private static readonly Color TextBody = new(0.9f, 0.93f, 0.97f, 0.92f);
    private static readonly Color CardBg = new(0.08f, 0.1f, 0.14f, 0.74f);
    private static readonly Color CardBgHover = new(0.1f, 0.12f, 0.18f, 0.84f);
    private static readonly Color CardBgDisabled = new(0.08f, 0.09f, 0.12f, 0.62f);

    private static readonly HashSet<RelicRarity> TradeableRarities =
        [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop, RelicRarity.None];

    // ═══════════════════════════════════════════════════════════
    //  字段
    // ═══════════════════════════════════════════════════════════

    private readonly Player _player;
    private readonly TaskCompletionSource<RelicExchangeResult?> _completionSource = new();

    private readonly List<RelicModel> _gainOptions = [];
    private readonly List<EnchantmentModel> _enchantOptions = [];

    private RelicModel? _selectedLoseRelic;
    private RelicModel? _selectedGainRelic;
    private EnchantmentModel? _selectedGainEnchant;
    private bool _isEnchantMode;
    private bool _choiceLocked;

    // UI 节点缓存
    private HBoxContainer? _playerRelicsRow;
    private HBoxContainer? _rewardRow;
    private Button? _confirmButton;
    private Button? _cancelButton;
    private MegaLabel? _statusLabel;
    private HBoxContainer? _modeToggleRow;

    // ═══════════════════════════════════════════════════════════
    //  属性
    // ═══════════════════════════════════════════════════════════

    public NetScreenType ScreenType => NetScreenType.Rewards;
    public bool UseSharedBackstop => true;
    public Control? DefaultFocusedControl => null;

    // ═══════════════════════════════════════════════════════════
    //  构造
    // ═══════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════
    //  IOverlayScreen 接口实现
    // ═══════════════════════════════════════════════════════════

    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed() { }
    public void AfterOverlayShown() { }
    public void AfterOverlayHidden() { }

    // ═══════════════════════════════════════════════════════════
    //  Roll 逻辑
    // ═══════════════════════════════════════════════════════════

    private void RollGainOptions()
    {
        _gainOptions.Clear();
        _enchantOptions.Clear();

        var player = _player;
        if (player == null) return;

        // 滚 3 个遗物选项
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
        int enchantCount = 2;
        var enchants = GetEnchantPool();
        var rolled = new HashSet<string>();
        for (int i = 0; i < enchants.Count && _enchantOptions.Count < enchantCount; i++)
        {
            var enchant = enchants[i];
            if (rolled.Add(enchant.Id.Entry))
                _enchantOptions.Add(enchant);
        }
    }

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

    private static List<EnchantmentModel> GetEnchantPool()
    {
        try
        {
            // 通过反射从程序集获取所有非抽象 EnchantmentModel 子类
            var enchantTypes = typeof(EnchantmentModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(EnchantmentModel).IsAssignableFrom(t))
                .ToList();

            var result = new List<EnchantmentModel>();
            foreach (var type in enchantTypes)
            {
                try
                {
                    var enchant = (EnchantmentModel)Activator.CreateInstance(type)!;
                    if (!EnchantBlacklist.Contains(enchant.Id.Entry))
                        result.Add(enchant);
                }
                catch { /* 跳过无法实例化的附魔 */ }
            }

            return result.OrderBy(_ => Random.Shared.Next()).Take(5).ToList();
        }
        catch (Exception ex)
        {
            Log.Warn($"[ShunMod_Shun] RelicExchange.GetEnchantPool: {ex.Message}");
            return [];
        }
    }

    private static readonly HashSet<string> EnchantBlacklist = new()
    {
        "Adroit", "PerfectFit", "RoyallyApproved", "SlumberingEssence",
        "Sown", "Spiral", "Steady", "TezcatarasEmber", "Vigorous",
        "Swift", "Glam", "Clone", "Goopy", "Momentum", "Inky",
    };

    private static bool IsTradeable(RelicModel r) => TradeableRarities.Contains(r.Rarity);

    // ═══════════════════════════════════════════════════════════
    //  UI 构建
    // ═══════════════════════════════════════════════════════════

    private void BuildUi()
    {
        // 遮罩层
        ColorRect backdrop = new()
        {
            Name = "DimOverlay",
            Color = DimColor,
            MouseFilter = MouseFilterEnum.Stop
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        // 居中容器
        CenterContainer screenCenter = new()
        {
            Name = "ScreenCenter",
            MouseFilter = MouseFilterEnum.Ignore
        };
        screenCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(screenCenter);

        // 内容面板
        PanelContainer contentPanel = new()
        {
            Name = "ContentPanel",
            CustomMinimumSize = ScreenSize,
            MouseFilter = MouseFilterEnum.Ignore
        };
        contentPanel.AddThemeStyleboxOverride("panel", CreateContentPanelStyle());
        screenCenter.AddChild(contentPanel);

        // 外边距
        MarginContainer contentMargin = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        contentMargin.AddThemeConstantOverride("margin_left", 30);
        contentMargin.AddThemeConstantOverride("margin_right", 30);
        contentMargin.AddThemeConstantOverride("margin_top", 28);
        contentMargin.AddThemeConstantOverride("margin_bottom", 28);
        contentPanel.AddChild(contentMargin);

        // 总根 VBox
        VBoxContainer root = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 16);
        contentMargin.AddChild(root);

        // 标题
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

        // 上半部分：你的遗物
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

        // 玩家遗物滚动行
        ScrollContainer relicScroll = new()
        {
            Name = "PlayerRelicsScroll",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, CardSize.Y + 40f)
        };
        root.AddChild(relicScroll);

        _playerRelicsRow = new HBoxContainer()
        {
            Name = "PlayerRelicsRow",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _playerRelicsRow.AddThemeConstantOverride("separation", CardSeparation);
        relicScroll.AddChild(_playerRelicsRow);

        // 下半部分：可选奖励
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

        // 奖励滚动行
        ScrollContainer rewardScroll = new()
        {
            Name = "RewardScroll",
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, CardSize.Y + 40f)
        };
        root.AddChild(rewardScroll);

        _rewardRow = new HBoxContainer()
        {
            Name = "RewardRow",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _rewardRow.AddThemeConstantOverride("separation", CardSeparation);
        rewardScroll.AddChild(_rewardRow);

        // 状态提示
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

        // 按钮行
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
        StyleButton(_cancelButton, new Color(0.6f, 0.3f, 0.3f));
        _cancelButton.Pressed += OnCancel;
        buttonRow.AddChild(_cancelButton);

        _confirmButton = new Button()
        {
            Text = "确认交易",
            CustomMinimumSize = new Vector2(200f, 48f),
            Disabled = true
        };
        StyleButton(_confirmButton, AccentGold);
        _confirmButton.Pressed += OnConfirm;
        buttonRow.AddChild(_confirmButton);

        // 填充卡片
        RebuildPlayerRelics();
        RebuildRewards();
    }

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

        // 遗物奖励
        foreach (var relic in _gainOptions)
        {
            bool isSelected = relic == _selectedGainRelic;
            var card = CreateRelicCard(relic, isPlayerRelic: false, isSelected: isSelected);
            _rewardRow.AddChild(card);
        }

        // 附魔奖励
        foreach (var enchant in _enchantOptions)
        {
            bool isSelected = enchant == _selectedGainEnchant;
            var card = CreateEnchantCard(enchant, isSelected);
            _rewardRow.AddChild(card);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  卡片创建
    // ═══════════════════════════════════════════════════════════

    private Control CreateRelicCard(RelicModel relic, bool isPlayerRelic, bool isSelected)
    {
        Control slot = new()
        {
            Name = $"RelicCard_{relic.Id.Entry}",
            CustomMinimumSize = CardSize,
            MouseFilter = MouseFilterEnum.Ignore
        };

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

        MarginContainer margin = new();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        button.AddChild(margin);

        VBoxContainer content = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        // 图标
        CenterContainer iconBox = new()
        {
            CustomMinimumSize = new Vector2(0f, 120f),
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
                CustomMinimumSize = new Vector2(100f, 100f),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };
            iconBox.AddChild(tex);
            AttachRelicHoverTips(tex, relic);
        }

        // 名称
        MegaLabel nameLabel = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 22,
            MinFontSize = 15
        };
        nameLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        nameLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        nameLabel.AddThemeConstantOverride("outline_size", 1);
        nameLabel.Modulate = TextPrimary;
        nameLabel.SetTextAutoSize(relic.Title.GetFormattedText());
        content.AddChild(nameLabel);

        // 描述
        MegaRichTextLabel desc = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxFontSize = 16,
            MinFontSize = 12,
            BbcodeEnabled = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        desc.AddThemeColorOverride("default_color", new Color(0.9f, 0.93f, 0.97f, 0.92f));
        desc.AddThemeColorOverride("default_color", TextBody);
        desc.SetTextAutoSize(relic.DynamicDescription.GetFormattedText());
        content.AddChild(desc);

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

    private Control CreateEnchantCard(EnchantmentModel enchant, bool isSelected)
    {
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
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        button.AddChild(margin);

        VBoxContainer content = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        // 附魔图标（用缺省背景）
        CenterContainer iconBox = new()
        {
            CustomMinimumSize = new Vector2(0f, 120f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(iconBox);

        MegaLabel enchantLabel = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxFontSize = 36,
            MinFontSize = 24
        };
        enchantLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 0.99f, 0.98f));
        enchantLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.4f));
        enchantLabel.AddThemeConstantOverride("outline_size", 1);
        enchantLabel.Modulate = new Color(0.6f, 0.9f, 0.6f);
        enchantLabel.SetTextAutoSize("⚡");
        iconBox.AddChild(enchantLabel);

        // 附魔名称
        MegaLabel nameLabel = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxFontSize = 22,
            MinFontSize = 15
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
            MaxFontSize = 16,
            MinFontSize = 12,
            BbcodeEnabled = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        desc.AddThemeColorOverride("default_color", new Color(0.9f, 0.93f, 0.97f, 0.92f));
        desc.AddThemeColorOverride("default_color", TextBody);
        desc.SetTextAutoSize(enchant.Description.GetFormattedText());
        content.AddChild(desc);

        button.Pressed += () => OnRewardSelected(null, enchant);
        slot.AddChild(button);
        return slot;
    }

    // ═══════════════════════════════════════════════════════════
    //  样式
    // ═══════════════════════════════════════════════════════════

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
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", normal);
        button.AddThemeStyleboxOverride("focus", hover);
        button.AddThemeStyleboxOverride("disabled", normal);
    }

    // ═══════════════════════════════════════════════════════════
    //  Hover 提示
    // ═══════════════════════════════════════════════════════════

    private static void AttachRelicHoverTips(Control holder, RelicModel relic)
    {
        // 暂不实现悬浮提示，等后续公共化工具
        // 参考：NHoverTipSet.CreateAndShow(holder, relic.HoverTips, ...)
    }

    // ═══════════════════════════════════════════════════════════
    //  交互处理
    // ═══════════════════════════════════════════════════════════

    private void OnPlayerRelicSelected(RelicModel relic)
    {
        if (_choiceLocked) return;

        // 允许取消选择
        if (_selectedLoseRelic == relic)
            _selectedLoseRelic = null;
        else
            _selectedLoseRelic = relic;

        UpdateVisualState();
        RebuildPlayerRelics();
    }

    private void OnRewardSelected(RelicModel? relic, EnchantmentModel? enchant)
    {
        if (_choiceLocked || _selectedLoseRelic == null) return;

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

    private void OnConfirm()
    {
        if (_choiceLocked || _selectedLoseRelic == null) return;
        if (_selectedGainRelic == null && _selectedGainEnchant == null) return;

        _choiceLocked = true;
        _completionSource.TrySetResult(new RelicExchangeResult(
            _selectedLoseRelic,
            _selectedGainRelic,
            _selectedGainEnchant));
    }

    private void OnCancel()
    {
        _choiceLocked = true;
        _completionSource.TrySetResult(null);
    }

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