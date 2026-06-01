namespace STS2_ShunMod.Core;

/// <summary>
///     Mod 资源路径工具 — 所有图片引用统一走这里，避免硬编码。
/// </summary>
public static class ShunImageHelper
{
    private const string Root = "res://STS2-ShunMod/images";

    // ── 卡牌 ──

    public static string CardPortrait(string color, string name) =>
        $"{Root}/cards/shunCards/{color}/{name}.png";

    // ── 遗物 ──

    public static string RelicPackedIcon(string iconBaseName) =>
        $"{Root}/relics/shunRelics/{iconBaseName}/{iconBaseName}.png";

    public static string RelicOutlineIcon(string iconBaseName) =>
        $"{Root}/relics/shunRelics/{iconBaseName}/{iconBaseName}_outline.png";

    public static string RelicBigIcon(string iconBaseName) =>
        $"{Root}/relics/shunRelics/{iconBaseName}/{iconBaseName}.png";

    // ── 事件 ──

    public static string EventImage(string id) =>
        $"{Root}/events/shunEvents/{id}.png";
}