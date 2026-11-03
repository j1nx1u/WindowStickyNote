using System;
using System.Collections.Generic;
using System.Drawing;

namespace WindowStickyNote;

public class NoteData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public string ColorKey { get; set; } = "yellow";

    public bool IsBound { get; set; }
    public string? BoundProcessName { get; set; }
    public string? BoundWindowTitle { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }

    public int FreeX { get; set; } = 200;
    public int FreeY { get; set; } = 200;
    public int Width { get; set; } = 340;
    public int Height { get; set; } = 260;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public static class NoteColors
{
    public record Palette(Color Back, Color Header, Color Text, Color Muted, Color Display);

    private static readonly Dictionary<string, Palette> Map = new()
    {
        ["yellow"] = new Palette(
            Color.FromArgb(255, 249, 198),
            Color.FromArgb(246, 232, 168),
            Color.FromArgb(62, 58, 47),
            Color.FromArgb(150, 138, 100),
            Color.FromArgb(255, 214, 64)),
        ["blue"] = new Palette(
            Color.FromArgb(222, 240, 252),
            Color.FromArgb(198, 226, 246),
            Color.FromArgb(40, 58, 72),
            Color.FromArgb(110, 138, 156),
            Color.FromArgb(64, 156, 214)),
        ["pink"] = new Palette(
            Color.FromArgb(255, 235, 240),
            Color.FromArgb(250, 214, 226),
            Color.FromArgb(72, 46, 54),
            Color.FromArgb(168, 124, 138),
            Color.FromArgb(232, 120, 148)),
        ["mint"] = new Palette(
            Color.FromArgb(226, 248, 236),
            Color.FromArgb(200, 238, 220),
            Color.FromArgb(40, 66, 54),
            Color.FromArgb(108, 148, 128),
            Color.FromArgb(72, 184, 132)),
        ["purple"] = new Palette(
            Color.FromArgb(242, 236, 252),
            Color.FromArgb(224, 214, 246),
            Color.FromArgb(58, 48, 72),
            Color.FromArgb(138, 124, 168),
            Color.FromArgb(148, 116, 212)),
    };

    public static readonly string[] Keys = { "yellow", "blue", "pink", "mint", "purple" };

    public static Palette Get(string key) =>
        Map.TryGetValue(key, out var p) ? p : Map["yellow"];
}