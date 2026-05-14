using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_ShunMod.Ui;

/// <summary>
/// 紧凑药水抽屉 —— 隐藏原版横向药水条，替换为按钮 + 弹出网格。
/// 参照 STS2Plus CompactRelicDrawer 模式。
/// </summary>
internal sealed class CompactPotionDrawer : Control
{
    private const string NodeName = "STS2ShunCompactPotionDrawer";
    private const float BtnW = 116f, BtnH = 64f;
    private const float GridItemSize = 76f, GridSpacing = 10f;
    private const float PopupMargin = 16f, PopupTopGap = 10f;

    private static readonly Color Bg = new(0.11f, 0.08f, 0.18f, 0.96f);
    private static readonly Color Bdr = new(0.35f, 0.55f, 0.95f, 0.96f);
    private static readonly Color Hov = new(0.16f, 0.12f, 0.24f, 0.98f);
    private static readonly Color Prs = new(0.08f, 0.06f, 0.16f, 0.98f);
    private static readonly Color PnlBg = new(0.08f, 0.06f, 0.16f, 0.97f);
    private static readonly Color PnlBdr = new(0.35f, 0.55f, 0.85f, 1f);
    private static readonly Color BackdropCol = new(0f, 0f, 0f, 0.16f);
    private static readonly Color Accent = new(0.38f, 0.68f, 1f, 1f);
    private static readonly Color CountCol = new(1f, 0.95f, 0.84f, 1f);

    private static readonly Dictionary<NGlobalUi, CompactPotionDrawer> Instances = new();

    private static readonly FieldInfo HoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_holders");
    private static readonly FieldInfo PotionHoldersField =
        AccessTools.Field(typeof(NPotionContainer), "_potionHolders");

    private NGlobalUi? _globalUi;
    private NPotionContainer? _container;
    private Control? _potionHoldersNode;
    private Player? _player;
    private Button? _btn;
    private Label? _countLabel;
    private ColorRect? _backdrop;
    private PanelContainer? _panel;
    private GridContainer? _grid;
    private Button? _closeBtn;
    private readonly List<NPotionHolder> _holders = new();
    private bool _open;
    private int _cols = 3;

    // ── attach ────────────────────────────────────────────────

    public static void Attach(NGlobalUi globalUi, RunState runState)
    {
        if (!GodotObject.IsInstanceValid(globalUi)) return;

        PruneInvalid();
        if (!Instances.TryGetValue(globalUi, out var d) || !GodotObject.IsInstanceValid(d))
        {
            d = new CompactPotionDrawer { Name = NodeName };
            Instances[globalUi] = d;
            globalUi.AddChild(d, false, InternalMode.Disabled);
            globalUi.MoveChild(d, -1);
        }
        d.Bind(globalUi, runState);
    }

    private static void PruneInvalid()
    {
        var dead = Instances.Where(kv => !GodotObject.IsInstanceValid(kv.Key) || !GodotObject.IsInstanceValid(kv.Value)).Select(kv => kv.Key).ToList();
        foreach (var k in dead) Instances.Remove(k);
    }

    private void Bind(NGlobalUi globalUi, RunState runState)
    {
        if (_player != null)
        {
            _player.PotionProcured -= OnPotionProcured;
            _player.UsedPotionRemoved -= OnUsedPotionRemoved;
            _player.PotionDiscarded -= OnPotionDiscarded;
            _player.MaxPotionCountChanged -= OnMaxPotionChanged;
            _player.RelicObtained -= OnRelicsUpdated;
            _player.RelicRemoved -= OnRelicsUpdated;
        }

        _globalUi = globalUi;
        _container = FindPotionContainer(globalUi);
        _player = LocalContext.GetMe(runState);

        if (_container != null)
            _potionHoldersNode = (Control?)PotionHoldersField?.GetValue(_container);

        if (_player != null)
        {
            _player.PotionProcured += OnPotionProcured;
            _player.UsedPotionRemoved += OnUsedPotionRemoved;
            _player.PotionDiscarded += OnPotionDiscarded;
            _player.MaxPotionCountChanged += OnMaxPotionChanged;
            _player.RelicObtained += OnRelicsUpdated;
            _player.RelicRemoved += OnRelicsUpdated;
        }

        HideContainer();
        SyncPosition();
        Refresh(rebuild: true);
        Visible = true;
        SetProcess(true);
        SetProcessInput(true);
    }

    private void HideContainer()
    {
        if (_container == null || !GodotObject.IsInstanceValid(_container)) return;
        _container.Visible = false;
        _container.MouseFilter = MouseFilterEnum.Ignore;
        _container.FocusMode = FocusModeEnum.None;
    }

    private void RestoreContainer()
    {
        if (_container == null || !GodotObject.IsInstanceValid(_container)) return;
        _container.Visible = true;
        _container.MouseFilter = MouseFilterEnum.Stop;
        _container.FocusMode = FocusModeEnum.All;
    }

    // ── lifecycle ─────────────────────────────────────────────

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        SizeFlagsHorizontal = SizeFlags.Fill;
        SizeFlagsVertical = SizeFlags.Fill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ZIndex = 180;

        _btn = CreateButton();
        AddChild(_btn, false, InternalMode.Disabled);

        _backdrop = new ColorRect
        {
            Name = "Backdrop", Visible = false, Color = BackdropCol,
            MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.None
        };
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _backdrop.GuiInput += OnBackdropInput;
        AddChild(_backdrop, false, InternalMode.Disabled);

        _panel = CreatePopup();
        _panel.Visible = false;
        AddChild(_panel, false, InternalMode.Disabled);
    }

    public override void _ExitTree()
    {
        if (_player != null)
        {
            _player.PotionProcured -= OnPotionProcured;
            _player.UsedPotionRemoved -= OnUsedPotionRemoved;
            _player.PotionDiscarded -= OnPotionDiscarded;
            _player.MaxPotionCountChanged -= OnMaxPotionChanged;
            _player.RelicObtained -= OnRelicsUpdated;
            _player.RelicRemoved -= OnRelicsUpdated;
        }
        if (_globalUi != null) Instances.Remove(_globalUi);
        _player = null;
        _container = null;
        _globalUi = null;
    }

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
        if (_open && _btn != null) LayoutPopup();
    }

    // ── button ────────────────────────────────────────────────

    private Button CreateButton()
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
        b.AddThemeStyleboxOverride("normal", SBox(Bg, Bdr, 2));
        b.AddThemeStyleboxOverride("hover", SBox(Hov, Bdr.Lightened(0.08f), 2));
        b.AddThemeStyleboxOverride("pressed", SBox(Prs, Bdr.Lightened(0.12f), 2));
        b.AddThemeStyleboxOverride("focus", FocusSBox());
        b.AddThemeStyleboxOverride("disabled", SBox(Bg.Darkened(0.12f), Bdr, 2));
        b.Pressed += Toggle;

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        margin.MouseFilter = MouseFilterEnum.Ignore;
        b.AddChild(margin, false, InternalMode.Disabled);

        var hbox = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        hbox.SizeFlagsHorizontal = SizeFlags.Fill;
        hbox.SizeFlagsVertical = SizeFlags.Fill;
        margin.AddChild(hbox, false, InternalMode.Disabled);

        hbox.AddChild(new Label { Text = "\U0001F9EA", MouseFilter = MouseFilterEnum.Ignore }, false, InternalMode.Disabled);

        _countLabel = new Label
        {
            Text = "0", MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _countLabel.AddThemeColorOverride("font_color", CountCol);
        _countLabel.AddThemeFontSizeOverride("font_size", 18);
        _countLabel.SizeFlagsHorizontal = SizeFlags.Fill;
        hbox.AddChild(_countLabel, false, InternalMode.Disabled);

        return b;
    }

    // ── popup ─────────────────────────────────────────────────

    private PanelContainer CreatePopup()
    {
        var p = new PanelContainer
        {
            Name = "PotionPopup",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        p.AddThemeStyleboxOverride("panel", SBox(PnlBg, PnlBdr, 2, 8));

        var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        p.AddChild(vbox, false, InternalMode.Disabled);

        // 顶部栏：标题 + 关闭按钮
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        top.SizeFlagsHorizontal = SizeFlags.Fill;
        var title = new Label { Text = "Potions", MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Left };
        title.AddThemeColorOverride("font_color", Accent);
        title.AddThemeFontSizeOverride("font_size", 16);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(title, false, InternalMode.Disabled);
        _closeBtn = new Button { Text = "✕", FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
        _closeBtn.AddThemeFontSizeOverride("font_size", 16);
        _closeBtn.Pressed += Close;
        top.AddChild(_closeBtn, false, InternalMode.Disabled);
        vbox.AddChild(top, false, InternalMode.Disabled);

        // 分隔线
        var sep = new HSeparator();
        vbox.AddChild(sep, false, InternalMode.Disabled);

        // 网格
        _grid = new GridContainer { Name = "PotionGrid", Columns = _cols, MouseFilter = MouseFilterEnum.Ignore };
        _grid.AddThemeConstantOverride("h_separation", (int)GridSpacing);
        _grid.AddThemeConstantOverride("v_separation", (int)GridSpacing);
        vbox.AddChild(_grid, false, InternalMode.Disabled);

        return p;
    }

    private void Toggle() { if (_open) Close(); else Open(); }

    private void Open()
    {
        if (_open || _container == null) return;
        MoveHoldersToGrid();
        _open = true;
        if (_backdrop != null) _backdrop.Visible = true;
        if (_panel != null) _panel.Visible = true;
        LayoutPopup();
        if (_closeBtn != null) _closeBtn.GrabFocus();
    }

    private void Close()
    {
        if (!_open) return;
        _open = false;
        if (_backdrop != null) _backdrop.Visible = false;
        if (_panel != null) _panel.Visible = false;
        ReturnHolders();
        if (_btn != null) _btn.GrabFocus();
    }

    private void LayoutPopup()
    {
        if (_panel == null || _btn == null) return;
        var sr = GetViewportRect();
        var br = _btn.GetGlobalRect();

        var cnt = _holders.Count;
        _cols = cnt <= 2 ? 2 : cnt <= 6 ? 3 : cnt <= 12 ? 4 : 5;
        if (_grid != null) _grid.Columns = _cols;

        var rows = (cnt + _cols - 1) / Mathf.Max(_cols, 1);
        var pw = _cols * (GridItemSize + GridSpacing) + PopupMargin * 2;
        var ph = rows * (GridItemSize + GridSpacing) + PopupMargin * 2 + 40; // +40 for title bar
        pw = Mathf.Min(pw, sr.Size.X - 16f);
        ph = Mathf.Min(ph, sr.Size.Y * 0.55f);

        _panel.CustomMinimumSize = new Vector2(pw, ph);

        var below = sr.Size.Y - br.Position.Y - br.Size.Y - 6f;
        var py = below >= ph + 6f ? br.Position.Y + br.Size.Y + 6f
                                  : Mathf.Max(6f, br.Position.Y - ph - 6f);
        var px = Mathf.Max(6f, Mathf.Min(br.Position.X + br.Size.X * 0.5f - pw * 0.5f, sr.Size.X - pw - 6f));
        _panel.Position = new Vector2(px, py);
        _panel.ZIndex = 200;
    }

    // ── holders ───────────────────────────────────────────────

    private void MoveHoldersToGrid()
    {
        if (_grid == null || _potionHoldersNode == null) return;
        _holders.Clear();
        if (HoldersField?.GetValue(_container) is not List<NPotionHolder> list) return;

        foreach (var c in _grid.GetChildren().ToList())
            if (c != _closeBtn) _grid.RemoveChild(c);

        foreach (var h in list)
        {
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            _holders.Add(h);
            var p = h.GetParent();
            p?.RemoveChild(h);
            _grid.AddChild(h);
        }
    }

    private void ReturnHolders()
    {
        foreach (var h in _holders)
        {
            if (h == null || !GodotObject.IsInstanceValid(h)) continue;
            var p = h.GetParent();
            if (p != _potionHoldersNode)
            {
                p?.RemoveChild(h);
                _potionHoldersNode?.AddChild(h);
            }
        }
        _container?.CallDeferred("UpdateNavigation");
    }

    // ── refresh ───────────────────────────────────────────────

    private void Refresh(bool rebuild)
    {
        if (_countLabel == null || _container == null) return;

        var list = HoldersField?.GetValue(_container) as List<NPotionHolder>;
        var cnt = list?.Count(h => h.HasPotion) ?? 0;
        _countLabel.Text = cnt.ToString();
        if (_btn != null) _btn.Disabled = cnt == 0;

        if (rebuild && _open) MoveHoldersToGrid();
    }

    private void SyncPosition()
    {
        if (_btn != null && _container != null && GodotObject.IsInstanceValid(_container))
            _btn.Position = _container.Position;
    }

    // ── player events ─────────────────────────────────────────

    private void OnPotionProcured(PotionModel _) => Refresh(rebuild: false);
    private void OnUsedPotionRemoved(PotionModel _) => Refresh(rebuild: false);
    private void OnPotionDiscarded(PotionModel _) => Refresh(rebuild: false);
    private void OnMaxPotionChanged(int _) => Refresh(rebuild: false);

    private void OnRelicsUpdated(RelicModel _) => SyncPosition();

    private void OnBackdropInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            Close();
    }

    // ── helpers ───────────────────────────────────────────────

    private static NPotionContainer? FindPotionContainer(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is NPotionContainer pc && GodotObject.IsInstanceValid(pc))
                return pc;
            var found = FindPotionContainer(child);
            if (found != null) return found;
        }
        return null;
    }

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
