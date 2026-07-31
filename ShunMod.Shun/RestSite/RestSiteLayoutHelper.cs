using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShunMod.Shun.RestSite;

internal static class RestSiteLayoutHelper
{
    private const float BaseLayerHeight = 162.879f;
    private const float BaseContainerWidth = 799f;
    private const float MaxLayoutWidth = 1720f;
    private const float ButtonWidth = 247f;
    private const int MaxColumnsPerRow = 10;
    private const int MinHSeparation = 20;
    private const int VSeparation = 20;
    private const float MinButtonScale = 0.52f;
    private const float OriginalOffsetTop = -285f;
    private const float OriginalDescriptionTop = -27f;
    private const float OriginalDescriptionBottom = 366f;
    private const float SectionGap = 36f;

    private static readonly FieldInfo ChoicesContainerField =
        AccessTools.Field(typeof(NRestSiteRoom), "_choicesContainer");

    private static readonly FieldInfo OriginalDescriptionYPosField =
        AccessTools.Field(typeof(NRestSiteRoom), "_originalDescriptionYPos");

    internal static void EnsureFlowContainer(NRestSiteRoom room)
    {
        var raw = ChoicesContainerField.GetValue(room);
        if (raw is not Control control)
            return;

        if (control is GridContainer)
            return;

        if (control.GetChildCount() > 0)
            return;

        var parent = control.GetParent();
        if (parent == null)
            return;

        var index = control.GetIndex();

        var grid = new GridContainer
        {
            Name = control.Name,
            MouseFilter = control.MouseFilter,
            LayoutMode = control.LayoutMode,
            AnchorsPreset = control.AnchorsPreset,
            AnchorLeft = control.AnchorLeft,
            AnchorTop = control.AnchorTop,
            AnchorRight = control.AnchorRight,
            AnchorBottom = control.AnchorBottom,
            OffsetLeft = -399.5f,
            OffsetTop = -285f,
            OffsetRight = 399.5f,
            OffsetBottom = -122.121f,
            GrowHorizontal = control.GrowHorizontal,
            GrowVertical = control.GrowVertical,
            Columns = 1,
            CustomMinimumSize = new Vector2(799f, 162.879f)
        };

        grid.AddThemeConstantOverride("h_separation", 100);
        grid.AddThemeConstantOverride("v_separation", 20);

        parent.RemoveChild(control);
        parent.AddChild(grid);
        parent.MoveChild(grid, index);
        control.QueueFree();

        ChoicesContainerField.SetValue(room, grid);
    }

    internal static void AdjustLayout(NRestSiteRoom room)
    {
        var container = ChoicesContainerField.GetValue(room) as GridContainer;
        if (container == null)
            return;

        var childCount = container.GetChildCount();
        if (childCount == 0)
        {
            container.Scale = Vector2.One;
            container.PivotOffset = Vector2.Zero;
            ResetDescriptionPosition(room);
            return;
        }

        var hSep = ChooseHSeparation(childCount);
        container.AddThemeConstantOverride("h_separation", hSep);
        container.AddThemeConstantOverride("v_separation", VSeparation);

        var plan = BuildLayoutPlan(childCount, hSep);
        container.Columns = plan.Columns;
        container.Scale = Vector2.One;
        container.PivotOffset = Vector2.Zero;

        Callable.From(() => ApplyDeferredLayout(room, container, plan))
            .CallDeferred();
    }

    private static int ChooseHSeparation(int optionCount)
    {
        int sep;
        if (optionCount > 6)
        {
            sep = optionCount <= 8 ? 32 : 24;
        }
        else
        {
            sep = optionCount <= 4 ? 100 : 48;
        }

        for (var i = sep; i >= MinHSeparation; i -= 4)
        {
            if (optionCount <= MaxColumnsPerRow)
                return i;
        }

        return MinHSeparation;
    }

    private static LayoutPlan BuildLayoutPlan(int count, int hSeparation)
    {
        var columns = 1;
        var rows = count;
        var scale = 1f;
        var unscaledWidth = BaseContainerWidth;

        for (var i = Math.Min(count, MaxColumnsPerRow); i >= 1; i--)
        {
            var totalWidth = i * ButtonWidth + (i - 1) * hSeparation;
            var s = Math.Min(1f, MaxLayoutWidth / totalWidth);

            if (s >= MinButtonScale || i <= 1)
            {
                columns = i;
                rows = (int)Math.Ceiling((double)count / i);
                scale = s;
                unscaledWidth = totalWidth;
                break;
            }
        }

        return new LayoutPlan(
            columns, rows,
            unscaledWidth, unscaledWidth * scale,
            new Vector2(scale, scale));
    }

    private static void ApplyDeferredLayout(NRestSiteRoom room, GridContainer container, LayoutPlan plan)
    {
        if (!GodotObject.IsInstanceValid(container))
            return;

        var minHeight = Math.Max(
            container.GetMinimumSize().Y,
            plan.Rows * BaseLayerHeight + (plan.Rows - 1) * VSeparation);

        container.CustomMinimumSize = new Vector2(plan.UnscaledWidth, minHeight);
        container.PivotOffset = new Vector2(plan.UnscaledWidth / 2f, 0f);
        container.Scale = plan.Scale;

        var layoutWidth = plan.UnscaledWidth * plan.Scale.X;
        var layoutHeight = minHeight * plan.Scale.Y;

        container.OffsetLeft = -layoutWidth / 2f;
        container.OffsetRight = layoutWidth / 2f;
        container.OffsetTop = OriginalOffsetTop;
        container.OffsetBottom = OriginalOffsetTop + layoutHeight;

        PositionDescription(room, container.OffsetBottom);
    }

    private static void PositionDescription(NRestSiteRoom room, float choicesBottom)
    {
        var desc = room.GetNode<Control>("%Description");
        var top = Math.Max(OriginalDescriptionTop, choicesBottom + SectionGap);
        var bottom = top + 393f;

        desc.OffsetTop = top;
        desc.OffsetBottom = bottom;

        OriginalDescriptionYPosField.SetValue(room, desc.Position.Y);
    }

    private static void ResetDescriptionPosition(NRestSiteRoom room)
    {
        var desc = room.GetNode<Control>("%Description");
        desc.OffsetTop = OriginalDescriptionTop;
        desc.OffsetBottom = OriginalDescriptionBottom;

        OriginalDescriptionYPosField.SetValue(room, desc.Position.Y);
    }

    private readonly struct LayoutPlan
    {
        public int Columns { get; }
        public int Rows { get; }
        public float UnscaledWidth { get; }
        public Vector2 Scale { get; }

        public LayoutPlan(int columns, int rows, float unscaledWidth, float layoutWidth, Vector2 scale)
        {
            Columns = columns;
            Rows = rows;
            UnscaledWidth = unscaledWidth;
            _ = layoutWidth;
            Scale = scale;
        }
    }
}