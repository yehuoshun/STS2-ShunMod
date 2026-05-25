namespace STS2_ShunMod.Core;

/// <summary>
///     Mod 资源路径工具 — 所有图片引用统一走这里，避免硬编码。
/// </summary>
public static class ShunImageHelper
{
    private const string Root = "res://STS2-ShunMod/images";

    // ── 卡牌 ──

    public static string CardPortrait(string color, string name)
    {
        return $"{Root}/packed/card_portraits/{color}/{name}.png";
    }

    // ── 遗物 ──

    public static string RelicBigIcon(string iconBaseName)
    {
        return $"{Root}/relics/{iconBaseName}/{iconBaseName}.png";
    }

    // ── 事件 ──

    public static string EventImage(string id)
    {
        return $"{Root}/events/{id}.png";
    }
}