using System.Collections;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace STS2ShunMod.STS2_ShunModCode.Settings;

/// <summary>
/// 设置界面 UI 注入器。
/// 学 ModConfig 架构：零 Harmony、纯 Godot 信号 + Duplicate 模板。
/// </summary>
internal static class SettingsUI
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    // 追踪所有注入实例（主菜单 + 暂停菜单各一个 NSettingsTabManager）
    private static readonly List<WeakReference<VBoxContainer>> _allContainers = new();
    private static readonly List<WeakReference<NSettingsTab>> _allTabs = new();

    // 双向绑定
    private static readonly Dictionary<string, List<LiveBinding>> _liveBindings = new();

    // 颜色
    private static readonly Color TextColor = new(0.9f, 0.85f, 0.75f);
    private static readonly Color AccentColor = new("D4C88E");

    private static ConfigEntry[] _entries = Array.Empty<ConfigEntry>();

    private sealed class LiveBinding
    {
        public Func<object, bool> Apply { get; }
        public LiveBinding(Func<object, bool> apply) => Apply = apply;
    }

    private sealed class UiUpdateGuard
    {
        public bool Suppress { get; set; }
    }

    // ─── 入口 ────────────────────────────────────────────────────

    internal static void Initialize(ConfigEntry[] entries)
    {
        _entries = entries;
        SettingsManager.ValueChanged += OnSettingChanged;

        var tree = (SceneTree)Engine.GetMainLoop();
        tree.NodeAdded += OnNodeAdded;
    }

    private static void OnNodeAdded(Node node)
    {
        if (node is not NSettingsTabManager) return;
        if (node.GetNodeOrNull("ShunMod") != null) return;

        node.Connect("ready",
            Callable.From(() => InjectTab((NSettingsTabManager)node)),
            (uint)GodotObject.ConnectFlags.OneShot);
    }

    // ─── 注入标签页 ──────────────────────────────────────────────

    private static void InjectTab(NSettingsTabManager tabManager)
    {
        try
        {
            var tabsField = typeof(NSettingsTabManager).GetField("_tabs", PrivateInstance);
            if (tabsField == null) return;

            var tabs = tabsField.GetValue(tabManager) as IDictionary;
            if (tabs == null || tabs.Count == 0) return;

            NSettingsTab? firstTab = null;
            NSettingsPanel? firstPanel = null;
            foreach (DictionaryEntry entry in tabs)
            {
                firstTab = entry.Key as NSettingsTab;
                firstPanel = entry.Value as NSettingsPanel;
                break;
            }

            if (firstTab == null || firstPanel == null) return;

            // 1. Duplicate Tab
            var myTab = (NSettingsTab)firstTab.Duplicate();
            myTab.Name = "ShunMod";
            myTab.SetLabel("ShunMod");
            tabManager.AddChild(myTab);
            myTab.Deselect();
            PositionTab(tabs, myTab);

            // 2. Duplicate Panel → 清空 → 作为容器
            var myPanel = (NSettingsPanel)firstPanel.Duplicate();
            myPanel.Name = "ShunModSettings";
            myPanel.Visible = false;

            var contentName = firstPanel.Content?.Name;
            VBoxContainer? contentContainer = null;

            foreach (var child in myPanel.GetChildren().ToArray())
            {
                bool keepAsContent =
                    child is VBoxContainer vboxCandidate &&
                    ((contentName != null && child.Name == contentName) ||
                     (contentName == null && contentContainer == null));

                if (keepAsContent && child is VBoxContainer vbox)
                {
                    contentContainer = vbox;
                    foreach (var inner in vbox.GetChildren().ToArray())
                    {
                        vbox.RemoveChild(inner);
                        inner.Free();
                    }
                }
                else
                {
                    myPanel.RemoveChild(child);
                    child.Free();
                }
            }

            firstPanel.GetParent().AddChild(myPanel);

            if (contentContainer == null)
                contentContainer = myPanel.Content;

            _allContainers.Add(new WeakReference<VBoxContainer>(contentContainer));

            // 3. 注册到 _tabs
            tabs.Add(myTab, myPanel);

            // 4. 连接点击
            myTab.Connect(
                NClickableControl.SignalName.Released,
                Callable.From<NButton>(_ =>
                {
                    try { tabManager.Call("SwitchTabTo", myTab); }
                    catch { /* ignore */ }
                }));

            // 5. 追踪 tab（语言切换时更新标签）
            _allTabs.Add(new WeakReference<NSettingsTab>(myTab));

            // 6. 限制面板高度（防止撑破 ScrollContainer）
            try
            {
                float maxHeight = firstPanel.Size.Y;
                if (maxHeight < 100)
                    maxHeight = myPanel.GetParent<Control>().Size.Y * 0.85f;
                myPanel.Size = new Vector2(myPanel.Size.X, maxHeight);
            }
            catch { /* ignore */ }

            // 7. 填充 UI
            PopulateInto(contentContainer);
        }
        catch (Exception e)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[ShunMod] Settings UI injection failed: {e}");
        }
    }

    private static void PositionTab(IDictionary tabs, NSettingsTab myTab)
    {
        var existingTabs = new List<NSettingsTab>();
        foreach (DictionaryEntry entry in tabs)
            existingTabs.Add((NSettingsTab)entry.Key);

        if (existingTabs.Count < 2) return;

        float spacing = existingTabs[1].Position.X - existingTabs[0].Position.X;
        var lastTab = existingTabs.Last();

        myTab.Position = new Vector2(lastTab.Position.X + spacing, lastTab.Position.Y);
        myTab.Size = existingTabs[0].Size;

        var tabManager = myTab.GetParent<Control>();
        float rightEdge = myTab.Position.X + myTab.Size.X;
        if (rightEdge > tabManager.Size.X && tabManager.Size.X > 0)
        {
            int totalTabs = existingTabs.Count + 1;
            float tabWidth = existingTabs[0].Size.X;
            float newSpacing = tabManager.Size.X / totalTabs;
            float startX = (newSpacing - tabWidth) / 2f;

            for (int i = 0; i < existingTabs.Count; i++)
                existingTabs[i].Position = new Vector2(startX + newSpacing * i, existingTabs[i].Position.Y);

            myTab.Position = new Vector2(startX + newSpacing * existingTabs.Count, existingTabs[0].Position.Y);
        }
    }

    // ─── 填充 UI ─────────────────────────────────────────────────

    private static void PopulateInto(VBoxContainer container)
    {
        _liveBindings.Clear();

        foreach (var entry in _entries)
        {
            switch (entry.Type)
            {
                case ConfigEntryType.Toggle:
                    AddToggle(container, entry);
                    break;
                case ConfigEntryType.Slider:
                    AddSlider(container, entry);
                    break;
                case ConfigEntryType.Dropdown:
                    AddDropdown(container, entry);
                    break;
            }
        }
    }

    // ─── Toggle ──────────────────────────────────────────────────

    private static void AddToggle(VBoxContainer parent, ConfigEntry entry)
    {
        var hbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 45) };
        hbox.AddThemeConstantOverride("separation", 20);

        var label = new Label
        {
            Text = $"  {entry.Label}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeFontSizeOverride("font_size", 20);

        var tickbox = new CheckBox
        {
            ButtonPressed = SettingsManager.GetValue(entry.Key, (bool)entry.DefaultValue),
            FocusMode = Control.FocusModeEnum.All,
        };

        var guard = new UiUpdateGuard();
        var tickboxRef = new WeakReference<CheckBox>(tickbox);

        RegisterBinding(entry.Key, new LiveBinding(value =>
        {
            if (!TryGetTarget(tickboxRef, out var tb)) return false;
            guard.Suppress = true;
            tb.ButtonPressed = Convert.ToBoolean(value);
            guard.Suppress = false;
            return true;
        }));

        tickbox.Toggled += pressed =>
        {
            if (guard.Suppress) return;
            SettingsManager.SetValue(entry.Key, pressed);
            entry.OnChanged?.Invoke(pressed);
        };

        hbox.AddChild(label);
        hbox.AddChild(tickbox);
        parent.AddChild(hbox);
    }

    // ─── Slider ──────────────────────────────────────────────────

    private static void AddSlider(VBoxContainer parent, ConfigEntry entry)
    {
        var hbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 45) };
        hbox.AddThemeConstantOverride("separation", 20);

        var label = new Label
        {
            Text = $"  {entry.Label}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeFontSizeOverride("font_size", 20);

        var currentValue = SettingsManager.GetValue(entry.Key, (float)entry.DefaultValue);
        var slider = new HSlider
        {
            MinValue = entry.Min,
            MaxValue = entry.Max,
            Step = entry.Step,
            Value = currentValue,
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(150, 0),
        };

        var valueLabel = new Label
        {
            Text = currentValue.ToString(entry.Format),
            CustomMinimumSize = new Vector2(60, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        valueLabel.AddThemeColorOverride("font_color", AccentColor);
        valueLabel.AddThemeFontSizeOverride("font_size", 16);

        var guard = new UiUpdateGuard();
        var sliderRef = new WeakReference<HSlider>(slider);
        var labelRef = new WeakReference<Label>(valueLabel);

        RegisterBinding(entry.Key, new LiveBinding(value =>
        {
            if (!TryGetTarget(sliderRef, out var s)) return false;
            guard.Suppress = true;
            s.Value = Convert.ToSingle(value);
            guard.Suppress = false;
            if (TryGetTarget(labelRef, out var l))
                l.Text = s.Value.ToString(entry.Format);
            return true;
        }));

        slider.ValueChanged += v =>
        {
            valueLabel.Text = v.ToString(entry.Format);
            if (guard.Suppress) return;
            SettingsManager.SetValue(entry.Key, v);
            entry.OnChanged?.Invoke(v);
        };

        hbox.AddChild(label);
        hbox.AddChild(slider);
        hbox.AddChild(valueLabel);
        parent.AddChild(hbox);
    }

    // ─── Dropdown ────────────────────────────────────────────────

    private static void AddDropdown(VBoxContainer parent, ConfigEntry entry)
    {
        var hbox = new HBoxContainer { CustomMinimumSize = new Vector2(0, 45) };
        hbox.AddThemeConstantOverride("separation", 20);

        var label = new Label
        {
            Text = $"  {entry.Label}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeFontSizeOverride("font_size", 20);

        var dropdown = new OptionButton { FocusMode = Control.FocusModeEnum.All };

        foreach (var opt in entry.Options)
            dropdown.AddItem(opt);

        var current = SettingsManager.GetValue(entry.Key, (string)entry.DefaultValue);
        for (int i = 0; i < entry.Options.Length; i++)
        {
            if (entry.Options[i] == current)
            {
                dropdown.Select(i);
                break;
            }
        }

        dropdown.ItemSelected += index =>
        {
            SettingsManager.SetValue(entry.Key, entry.Options[index]);
            entry.OnChanged?.Invoke(entry.Options[index]);
        };

        hbox.AddChild(label);
        hbox.AddChild(dropdown);
        parent.AddChild(hbox);
    }

    // ─── 双向绑定 ────────────────────────────────────────────────

    private static void RegisterBinding(string key, LiveBinding binding)
    {
        if (!_liveBindings.ContainsKey(key))
            _liveBindings[key] = new List<LiveBinding>();
        _liveBindings[key].Add(binding);
    }

    private static void OnSettingChanged(string key, object value)
    {
        if (!_liveBindings.TryGetValue(key, out var list)) return;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].Apply(value))
                list.RemoveAt(i);
        }
    }

    private static bool TryGetTarget<T>(WeakReference<T> reference, out T target) where T : GodotObject
    {
        if (reference.TryGetTarget(out var t) && GodotObject.IsInstanceValid(t))
        {
            target = t;
            return true;
        }
        target = null!;
        return false;
    }
}
