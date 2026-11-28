using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace NaviFantasy;

/// <summary>A small top-down radar (north is up): you, the enemies and the attack areas that are coming.</summary>
public sealed class RadarWindow : Window
{
    private readonly Configuration config;
    private readonly BossRadar radar;
    private readonly BossMemory memory;

    public RadarWindow(Configuration config, BossRadar radar, BossMemory memory)
        : base("Radar###NaviRadar", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        this.config = config;
        this.radar = radar;
        this.memory = memory;
    }

    private static uint Col(float r, float g, float b, float a) => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

    public override void Draw()
    {
        var size = Math.Clamp(config.RadarSizePixels, 120, 500);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));

        var dl = ImGui.GetWindowDrawList();
        var center = origin + new Vector2(size * 0.5f);
        var radius = size * 0.5f - 2f;
        var range = Math.Max(10, config.RadarRangeMeters);

        dl.AddCircleFilled(center, radius, Col(0.05f, 0.07f, 0.10f, 0.78f), 64);
        dl.AddCircle(center, radius * 0.5f, Col(0.4f, 0.6f, 0.7f, 0.25f), 48, 1f);
        dl.AddLine(center - new Vector2(radius, 0f), center + new Vector2(radius, 0f), Col(0.4f, 0.6f, 0.7f, 0.2f), 1f);
        dl.AddLine(center - new Vector2(0f, radius), center + new Vector2(0f, radius), Col(0.4f, 0.6f, 0.7f, 0.2f), 1f);
        dl.AddCircle(center, radius, Col(0.5f, 0.8f, 0.9f, 0.9f), 64, 2f);
        dl.AddText(new Vector2(center.X - 4f, origin.Y + 2f), Col(0.8f, 0.9f, 1f, 0.9f), "N");

        if (!radar.HasPlayer)
            return;

        Vector2 ToScreen(Vector2 world) => center + (world - radar.PlayerPos) / range * radius;
        float Scale(float meters) => meters / range * radius;

        // ---- attack areas
        if (config.RadarShowAreas)
        {
            foreach (var c in radar.Casts)
            {
                if (c.Total < 1f)
                    continue;

                var danger = c.PlayerInside;
                var fill = danger ? Col(1f, 0.15f, 0.15f, 0.45f) : Col(1f, 0.6f, 0.1f, 0.28f);
                var edge = danger ? Col(1f, 0.3f, 0.3f, 1f) : Col(1f, 0.7f, 0.2f, 0.95f);

                if (c.IsCircle)
                {
                    dl.AddCircleFilled(ToScreen(c.Center), Scale(c.Radius), fill, 48);
                    dl.AddCircle(ToScreen(c.Center), Scale(c.Radius), edge, 48, 2f);
                }
                else if (c.IsRect)
                {
                    var side = new Vector2(-c.RectForward.Y, c.RectForward.X) * c.RectHalfWidth;
                    var fwd = c.RectForward * c.RectLength;
                    var p1 = ToScreen(c.RectOrigin + side);
                    var p2 = ToScreen(c.RectOrigin + side + fwd);
                    var p3 = ToScreen(c.RectOrigin - side + fwd);
                    var p4 = ToScreen(c.RectOrigin - side);
                    dl.AddQuadFilled(p1, p2, p3, p4, fill);
                    dl.AddLine(p1, p2, edge, 2f);
                    dl.AddLine(p2, p3, edge, 2f);
                    dl.AddLine(p3, p4, edge, 2f);
                    dl.AddLine(p4, p1, edge, 2f);
                }
                else if (c.IsCone)
                {
                    // Cone angle is unknown: drawn as a rough 120 degree wedge, in yellow so it is not mistaken for a sure area.
                    var tip = ToScreen(c.CasterPos);
                    var coneFill = Col(1f, 0.9f, 0.2f, 0.18f);
                    var coneEdge = Col(1f, 0.9f, 0.2f, 0.7f);
                    const int steps = 8;
                    const float half = MathF.PI / 3f;
                    Vector2 Edge(float a)
                    {
                        var yaw = c.CasterYaw + a;
                        return ToScreen(c.CasterPos + new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)) * c.Range);
                    }

                    var previous = Edge(-half);
                    dl.AddLine(tip, previous, coneEdge, 1.5f);
                    for (var i = 1; i <= steps; i++)
                    {
                        var next = Edge(-half + 2f * half * i / steps);
                        dl.AddTriangleFilled(tip, previous, next, coneFill);
                        previous = next;
                    }

                    dl.AddLine(tip, previous, coneEdge, 1.5f);
                }
            }
        }

        // ---- where you were safe the last time you met this attack (learned)
        if (config.RadarShowSafeSpots)
        {
            foreach (var spot in memory.SafeSpots)
            {
                var p = ToScreen(spot);
                if (Vector2.Distance(p, center) > radius)
                    continue;

                dl.AddCircleFilled(p, 5f, Col(0.2f, 1f, 0.4f, 0.35f), 16);
                dl.AddCircle(p, 9f, Col(0.3f, 1f, 0.5f, 1f), 24, 2f);
            }
        }

        // ---- enemies
        foreach (var h in radar.Hostiles)
        {
            var p = ToScreen(h.Pos);
            if (Vector2.Distance(p, center) > radius)
                continue;

            dl.AddCircleFilled(p, h.Casting ? 5f : 3f, h.Casting ? Col(1f, 0.25f, 0.25f, 1f) : Col(0.7f, 0.7f, 0.7f, 0.85f), 16);
        }

        // ---- what they are casting
        var shown = 0;
        foreach (var c in radar.Casts)
        {
            if (shown >= 3)
                break;

            var p = ToScreen(c.CasterPos);
            if (Vector2.Distance(p, center) > radius)
                continue;

            var label = string.IsNullOrWhiteSpace(c.ActionName) ? $"{c.Remaining:0.0}s" : $"{c.ActionName} {c.Remaining:0.0}s";
            dl.AddText(p + new Vector2(7f, -7f), Col(1f, 0.85f, 0.85f, 1f), label);
            shown++;
        }

        // ---- you
        var yaw = radar.PlayerYaw;
        dl.AddLine(center, center + new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)) * 12f, Col(0.4f, 1f, 1f, 1f), 2f);
        dl.AddCircleFilled(center, 4.5f, Col(0.4f, 1f, 1f, 1f), 16);
    }
}
