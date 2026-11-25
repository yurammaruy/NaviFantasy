using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

/// <summary>
/// Speech bubble drawn next to the fairy. The sprite is cut into pieces (fixed corners, stretchable sides)
/// so it can grow to fit up to two lines of text, and its little arrow always points at the fairy's head.
/// If there is no room above her, the bubble is flipped and placed below her instead.
/// </summary>
public sealed class SpeechBubble
{
    // Sprite is 102x55 px. Slice boundaries, in source pixels.
    private const float SrcW = 102f, SrcH = 55f;
    private const float X0 = 5f, X1 = 13f, X2 = 41f, X3 = 52f, X4 = 91f, X5 = 99f; // left edge | corner | stretch | tail | stretch | corner | right edge
    private const float Y0 = 11f, Y1 = 19f, Y2 = 37f, Y3 = 45f, Y4 = 53f;           // top edge | corner | stretch | corner | tail tip
    private const float OrnX0 = 23f, OrnX1 = 40f, OrnY0 = 2f, OrnY1 = 11f;          // crystal ornament on top
    private const float TipInTail = 9.5f;                                            // x of the arrow's tip inside the tail slice

    private const float PixelScale = 2f;
    private const float PadX = 14f, PadY = 9f;
    private const float MaxTextWidth = 240f;
    private const float GapToFairy = 4f;
    private const float FadeIn = 0.12f, FadeOut = 0.25f;
    private const int MaxLines = 2;

    private readonly ISharedImmediateTexture sprite;
    private string text = string.Empty;
    private float remaining;
    private float elapsed;
    private bool flipped;
    private bool hasRect;
    private Vector2 rectMin;
    private Vector2 rectMax;

    public SpeechBubble(ITextureProvider textureProvider, string path)
    {
        sprite = textureProvider.GetFromFile(path);
    }

    public void Show(string message, float seconds = 2.2f)
    {
        text = message;
        remaining = seconds;
        elapsed = 0f;
    }

    public void Dismiss() => remaining = 0f;

    /// <summary>Screen rectangle of the bubble as drawn this frame (false if it is not showing).</summary>
    public bool TryGetRect(out Vector2 min, out Vector2 max)
    {
        min = rectMin;
        max = rectMax;
        return hasRect;
    }

    public void Draw(Vector2 fairyPos, float fairyHalf, float dt)
    {
        hasRect = false;
        if (remaining <= 0f || string.IsNullOrWhiteSpace(text))
            return;
        if (!sprite.TryGetWrap(out var tex, out _) || tex == null)
            return;

        elapsed += dt;
        remaining -= dt;

        var gs = ImGuiHelpers.GlobalScale;
        var ps = PixelScale * gs;

        // --- text and bubble size ---
        // Grow the bubble sideways until the text fits in at most two lines (up to ~60% of the screen); cut with "..." beyond that.
        var maxWidth = MaxTextWidth * gs;
        var widthCap = MathF.Max(maxWidth, MathF.Min(ImGui.GetMainViewport().Size.X * 0.6f, 860f * gs));
        var lines = Wrap(text, maxWidth, int.MaxValue);
        while (lines.Count > MaxLines && maxWidth < widthCap)
        {
            maxWidth = MathF.Min(widthCap, maxWidth + 40f * gs);
            lines = Wrap(text, maxWidth, int.MaxValue);
        }

        if (lines.Count > MaxLines)
            lines = Wrap(text, widthCap, MaxLines);
        var lineH = ImGui.GetTextLineHeight();
        var textW = 0f;
        foreach (var l in lines)
            textW = MathF.Max(textW, ImGui.CalcTextSize(l).X);
        var textH = lineH * lines.Count;

        var wA = (X1 - X0) * ps;
        var wC = (X3 - X2) * ps;
        var wE = (X5 - X4) * ps;
        var hTop = (Y1 - Y0) * ps;
        var hBot = (Y3 - Y2) * ps;
        var hTip = (Y4 - Y3) * ps;
        var hOrn = (OrnY1 - OrnY0) * ps;

        var bodyH = MathF.Max(textH + 2f * PadY * gs, hTop + hBot + 2f * gs);
        var hMid = bodyH - hTop - hBot;
        var totalH = hOrn + bodyH + hTip;
        var width = MathF.Max(textW + 2f * PadX * gs, 40f * ps);

        // --- where it goes on screen ---
        var vp = ImGui.GetMainViewport();
        var vpPos = vp.Pos;
        var vpSize = vp.Size;
        var gap = GapToFairy * gs;
        var headY = fairyPos.Y - fairyHalf * 0.85f;
        var feetY = fairyPos.Y + fairyHalf * 0.85f;

        // Above her head by default; below her when she is too close to the top edge (with hysteresis to avoid flicker).
        var spaceAbove = headY - gap - vpPos.Y - 4f * gs;
        if (!flipped && spaceAbove < totalH) flipped = true;
        else if (flipped && spaceAbove > totalH + 40f * gs) flipped = false;

        // Fade + a small slide toward the fairy.
        var aIn = Math.Clamp(elapsed / FadeIn, 0f, 1f);
        var aOut = Math.Clamp(remaining / FadeOut, 0f, 1f);
        var alpha = MathF.Min(aIn, aOut);
        var slide = (1f - (1f - (1f - aIn) * (1f - aIn))) * 6f * gs;

        var originY = flipped ? feetY + gap - slide : headY - gap - totalH + slide;

        // Horizontal: the bubble is centered on her (rigid). The fairy keeps away from the screen edges so it fits;
        // this clamp is only a safety net for the first frame, and the arrow then slides so it still points at her.
        var margin = 4f * gs;
        var left = Math.Clamp(fairyPos.X - width * 0.5f, vpPos.X + margin, MathF.Max(vpPos.X + margin, vpPos.X + vpSize.X - width - margin));
        var tipOffset = fairyPos.X - left;
        var stretchTotal = width - wA - wC - wE;
        var lw = Math.Clamp(tipOffset - wA - TipInTail * ps, 0f, MathF.Max(0f, stretchTotal));
        var rw = stretchTotal - lw;

        var xA0 = left;
        var xA1 = xA0 + wA;
        var xB1 = xA1 + lw;
        var xC1 = xB1 + wC;
        var xD1 = xC1 + rw;
        var xE1 = xD1 + wE;

        rectMin = new Vector2(left, originY);
        rectMax = new Vector2(left + width, originY + totalH);
        hasRect = true;

        var dl = ImGui.GetBackgroundDrawList();
        var white = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, alpha));

        float ScreenY(float layoutY) => flipped ? originY + (totalH - layoutY) : originY + layoutY;

        void Piece(float x0, float layoutY0, float x1, float layoutY1, float sx0, float sy0, float sx1, float sy1)
        {
            if (x1 - x0 <= 0.01f || layoutY1 - layoutY0 <= 0.01f) return;
            var ya = ScreenY(layoutY0);
            var yb = ScreenY(layoutY1);
            var v0 = sy0 / SrcH;
            var v1 = sy1 / SrcH;
            if (flipped) (v0, v1) = (v1, v0);
            dl.AddImage(
                tex.Handle,
                new Vector2(x0, MathF.Min(ya, yb)), new Vector2(x1, MathF.Max(ya, yb)),
                new Vector2(sx0 / SrcW, v0), new Vector2(sx1 / SrcW, v1),
                white);
        }

        // Body: 5 columns x 3 rows.
        var r1a = hOrn; var r1b = r1a + hTop;
        var r2b = r1b + hMid;
        var r3b = r2b + hBot;
        (float dx0, float dx1, float sx0, float sx1)[] cols =
        {
            (xA0, xA1, X0, X1), (xA1, xB1, X1, X2), (xB1, xC1, X2, X3), (xC1, xD1, X3, X4), (xD1, xE1, X4, X5),
        };
        foreach (var c in cols)
        {
            Piece(c.dx0, r1a, c.dx1, r1b, c.sx0, Y0, c.sx1, Y1);
            Piece(c.dx0, r1b, c.dx1, r2b, c.sx0, Y1, c.sx1, Y2);
            Piece(c.dx0, r2b, c.dx1, r3b, c.sx0, Y2, c.sx1, Y3);
        }

        // Arrow (the tail slice, hanging off the bottom edge) and the crystal ornament on the opposite side.
        Piece(xB1, r3b, xC1, totalH, X2, Y3, X3, Y4);
        var ornW = (OrnX1 - OrnX0) * ps;
        var ornX = left + MathF.Min((OrnX0 - X0) * ps, MathF.Max(0f, width - ornW));
        Piece(ornX, 0f, ornX + ornW, hOrn, OrnX0, OrnY0, OrnX1, OrnY1);

        // Text, centered in the body.
        var bodyCenterY = ScreenY(hOrn + bodyH * 0.5f);
        var textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.20f, 0.22f, alpha));
        var y = bodyCenterY - textH * 0.5f;
        foreach (var l in lines)
        {
            var lw2 = ImGui.CalcTextSize(l).X;
            dl.AddText(new Vector2(left + (width - lw2) * 0.5f, y), textColor, l);
            y += lineH;
        }
    }

    /// <summary>Greedy word wrap; anything beyond maxLines is cut with an ellipsis.</summary>
    private static List<string> Wrap(string message, float maxWidth, int maxLines)
    {
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || ImGui.CalcTextSize(candidate).X <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0) lines.Add(current);
        if (lines.Count == 0) lines.Add(string.Empty);

        if (lines.Count > maxLines)
        {
            var last = string.Join(" ", lines.GetRange(maxLines - 1, lines.Count - (maxLines - 1)));
            while (last.Length > 1 && ImGui.CalcTextSize(last + "…").X > maxWidth)
                last = last[..^1];
            lines = lines.GetRange(0, maxLines - 1);
            lines.Add(last + "…");
        }

        return lines;
    }
}
