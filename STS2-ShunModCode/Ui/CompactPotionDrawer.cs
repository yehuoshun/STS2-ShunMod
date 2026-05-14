using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_ShunMod.Ui;

/// <summary>
/// 紧凑药水抽屉 — 隐藏原版横向药水条，替换为按钮 + 弹出网格面板。
/// 解决药水过多时挤出屏幕外的问题。
/// 基于 NPotionContainer / NPotionHolder 反编译代码编写。
/// </summary>
internal sealed class CompactPotionDrawer : Control
{
    private const string NodeName = "STS2ShunCompactPotionDrawer";
    private const float BtnW = 100f, BtnH = 64f;
    private const float GridItemSize = 74f, GridGap = 6f, PopupPad = 10f;

    private static readonly Color Bg = new(0.09f, 0.11f, 0.18f, 0.94f);
    private static readonly Color Border = new(0.35f, 0.55f, 0.95f, 0.92f);
    private static readonly Color Hover = new(0.12f, 0.15f, 0.24f, 0.97f);
    private static readonly Color Pressed = new(0.06f, 0.08f, 0.14f, 0.97f);
    private static readonly Color PanelBg = new(0.06f, 0.08f, 0.14f, 0.97f);
    private static readonly Color PanelOut = new(0.38f, 0.52f, 0.82f, 0.96f);
    private static readonly Color Backdrop = new(0f, 0f, 0f, 0.14f);
    private static readonly Color CountCol = new(0.38f, 0.68f, 1f, 1f);

    private static readonly FieldInfo HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");
    private static readonly FieldInfo PotionHoldersNodeField =
        AccessTools.Field(typeof(NPotionContainer), "_potionHolders");

    // ── state ─────────────────────────────────────────────────

    private NPotionContainer? _container;
    private Control? _potionHoldersNode; // the Godot Control where holders live
    private Button? _btn;
    private Label? _countLabel;
    private ColorRect? _backdrop;
    private PanelContainer? _panel;
    private GridContainer? _grid;
    private List<NPotionHolder> _holders = new();
    private bool _open;
    private int _cols = 3;

    private bool _initialized;

    // ── attach ────────────────────────────────────────────────

    public static void Attach(NPotionContainer container)
    {
        if (!GodotObject.IsInstanceValid(container)) return;

        var globalUi = NRun.Instance?.GlobalUi;
        if (globalUi == null) return;

        var existing = globalUi.GetNodeOrNull<CompactPotionDrawer>(NodeName);
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            existing.Rebind(container);
            return;
        }

        var d = new CompactPotionDrawer { Name = NodeName };
        globalUi.AddChild(d, false, InternalMode.Disabled);
        globalUi.MoveChild(d, -1);
        // 确保 UI 初始化（_Ready 在 C# new + AddChild 时不一定触发）
        d.InitUI();
        d.Rebind(container);
    }

    private void Rebind(NPotionContainer container)
    {
        _container = container;
        _potionHoldersNode =
            (Control?)PotionHoldersNodeField?.GetValue(container);
        // 隐藏原版横向药水条
        container.Visible = false;
        SyncPosition();
        Refresh();
        Visible = true;
    }

    // ── lifecycle ─────────────────────────────────────────────

    /// <summary>
    /// 显式初始化 UI 子节点。
    /// 在 C# new + AddChild 方式创建节点时，Godot 的 _Ready 可能不触发，
    /// 因此 Attach() 中会主动调用此方法。_Ready 作为后备。
    /// </summary>
    private void InitUI()
    {
        if (_initialized) return;
        _initialized = true;

        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 175;

        _btn = MakeButton();
        AddChild(_btn, false, InternalMode.Disabled);

        _backdrop = new ColorRect
        {
            Name = "Backdrop", Visible = false, Color = Backdrop,
            MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.None
        };
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _backdrop.GuiInput += (e) => { if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) Close(); };
        AddChild(_backdrop, false, InternalMode.Disabled);

        _panel = MakePanel();
        _panel.Visible = false;
        AddChild(_panel, false, InternalMode.Disabled);
    }

    public override void _Ready() => InitUI();

    public override void _Input(InputEvent e)
    {
        if (_open && (e.IsActionPressed("ui_cancel") || e.IsActionPressed("ui_back")))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_open && _btn != null) LayoutPanel();
    }

    // ── button ────────────────────────────────────────────────

    private Button MakeButton()
    {
        var b = new Button
        {
            Name = "PotionDrawerBtn",
            CustomMinimumSize = new Vector2(BtnW, BtnH),
            Size = new Vector2(BtnW, BtnH),
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            Text = ""
        };
        b.AddThemeStyleboxOverride("normal", SBox(Bg, Border, 2));
        b.AddThemeStyleboxOverride("hover", SBox(Hover, Border.Lightened(0.1f), 2));
        b.AddThemeStyleboxOverride("pressed", SBox(Pressed, Border.Lightened(0.15f), 2));
        b.AddThemeStyleboxOverride("focus", FocusSBox());
        b.Pressed += Toggle;

        var m = new MarginContainer();
        m.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        m.AddThemeConstantOverride("margin_left", 8);
        m.AddThemeConstantOverride("margin_right", 8);
        m.AddThemeConstantOverride("margin_top", 4);
        m.AddThemeConstantOverride("margin_bottom", 4);
        m.MouseFilter = MouseFilterEnum.Ignore;
        b.AddChild(m, false, InternalMode.Disabled);

        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        h.SizeFlagsHorizontal = SizeFlags.Fill;
        m.AddChild(h, false, InternalMode.Disabled);

        h.AddChild(new Label { Text = "\U0001F9EA", MouseFilter = MouseFilterEnum.Ignore }, false, InternalMode.Disabled);

        _countLabel = new Label
        {
            Text = "0", MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _countLabel.AddThemeColorOverride("font_color", CountCol);
        _countLabel.AddThemeFontSizeOverride("font_size", 18);
        _countLabel.SizeFlagsHorizontal = SizeFlags.Fill;
        h.AddChild(_countLabel, false, InternalMode.Disabled);

        return b;
    }

    // ── panel ─────────────────────────────────────────────────

    private PanelContainer MakePanel()
    {
        var p = new PanelContainer
        {
            Name = "PotionPopup",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        p.AddThemeStyleboxOverride("panel", SBox(PanelBg, PanelOut, 2, 8));

        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        p.AddChild(v, false, InternalMode.Disabled);

        _grid = new GridContainer { Name = "PotionGrid", MouseFilter = MouseFilterEnum.Ignore, Columns = _cols };
        _grid.AddThemeConstantOverride("h_separation", (int)GridGap);
        _grid.AddThemeConstantOverride("v_separation", (int)GridGap);
        v.AddChild(_grid, false, InternalMode.Disabled);

        return p;
    }

    // ── toggle / layout ───────────────────────────────────────

    private void Toggle() { if (_open) Close(); else Open(); }

    private void Open()
    {
        if (_open || _container == null) return;

        RebuildGrid();
        _open = true;
        if (_backdrop != null) _backdrop.Visible = true;
        if (_panel != null) _panel.Visible = true;
        LayoutPanel();
    }

    private void Close()
    {
        if (!_open) return;
        _open = false;
        if (_backdrop != null) _backdrop.Visible = false;
        if (_panel != null) _panel.Visible = false;
        // move holders back to original container
        foreach (var h in _holders)
        {
            var p = h.GetParent();
            if (p != null && p != _potionHoldersNode)
            {
                p.RemoveChild(h);
                _potionHoldersNode?.AddChild(h);
            }
        }
        if (_container != null)
            _container.CallDeferred("UpdateNavigation");
    }

    private void LayoutPanel()
    {
        if (_panel == null || _btn == null) return;
        var sr = GetViewportRect();
        var br = _btn.GetGlobalRect();

        var cnt = _holders.Count;
        _cols = cnt <= 2 ? 2 : cnt <= 6 ? 3 : cnt <= 12 ? 4 : 5;
        if (_grid != null) _grid.Columns = _cols;

        var rows = (cnt + _cols - 1) / Mathf.Max(_cols, 1);
        var pw = _cols * (GridItemSize + GridGap) + PopupPad * 2;
        var ph = rows * (GridItemSize + GridGap) + PopupPad * 2;
        pw = Mathf.Min(pw, sr.Size.X - 16f);
        ph = Mathf.Min(ph, sr.Size.Y * 0.55f);

        _panel.CustomMinimumSize = new Vector2(pw, ph);

        var below = sr.Size.Y - br.Position.Y - br.Size.Y - 6f;
        var py = below >= ph + 6f ? br.Position.Y + br.Size.Y + 6f
                                  : Mathf.Max(6f, br.Position.Y - ph - 6f);
        var px = Mathf.Max(6f, Mathf.Min(
            br.Position.X + br.Size.X * 0.5f - pw * 0.5f,
            sr.Size.X - pw - 6f));

        _panel.Position = new Vector2(px, py);
    }

    // ── grid / refresh ────────────────────────────────────────

    private void RebuildGrid()
    {
        if (_grid == null || _potionHoldersNode == null) return;
        foreach (var c in _grid.GetChildren().ToList()) _grid.RemoveChild(c);

        _holders.Clear();
        if (HoldersField?.GetValue(_container) is List<NPotionHolder> list)
        {
            foreach (var h in list)
            {
                if (h == null || !GodotObject.IsInstanceValid(h)) continue;
                _holders.Add(h);
                var p = h.GetParent();
                p?.RemoveChild(h);
                _grid.AddChild(h);
            }
        }
    }

    private void Refresh()
    {
        if (_countLabel == null || _container == null) return;

        int cnt = 0;
        if (HoldersField?.GetValue(_container) is List<NPotionHolder> list)
            cnt = list.Count;

        _countLabel.Text = cnt.ToString();
        if (_btn != null) _btn.Disabled = cnt == 0;

        if (_open) RebuildGrid();
    }

    void SyncPosition()
    {
        if (_btn != null && _container != null)
            _btn.Position = _container.Position;
    }

    // ── helpers ───────────────────────────────────────────────

    private static StyleBoxFlat SBox(Color bg, Color border, int bw, int r = 8)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, ShadowSize = 0 };
        s.SetBorderWidthAll(bw);
        s.SetCornerRadiusAll(r);
        return s;
    }

    private static StyleBoxFlat FocusSBox()
    {
        var s = SBox(new(1, 1, 1, 0.02f), new(0.4f, 0.65f, 1f, 0.95f), 2);
        s.ExpandMarginLeft = s.ExpandMarginRight = s.ExpandMarginTop = s.ExpandMarginBottom = 1;
        return s;
    }
}
