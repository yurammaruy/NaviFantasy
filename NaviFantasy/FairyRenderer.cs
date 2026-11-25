using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

/// <summary>
/// Moves the fairy around the player's on-screen position, keeps her on screen, and draws her:
/// glow halo, pixie-dust sparkles, flapping wings, a bright orb, and her speech bubble.
/// </summary>
public sealed class FairyRenderer
{
    private const int Columns = 4;
    private const int Rows = 2;
    private const int FrameCount = 8;
    private const float FlapFps = 60f;       // 8 frames -> ~7.5 flaps per second
    private const float BaseSize = 160f;     // pixels at Scale = 1
    private const float ShoulderHeight = 1.45f;    // world units (yalms) above the feet: where her "home" is
    private const float SideDistanceYalms = 0.9f;  // how far to the side of the character she hovers
    private const float WanderYalms = 0.18f;       // how far she flutters around that spot
    private const float MouseMargin = 44f;         // she keeps at least this many pixels away from the mouse cursor
    private const float OrbOffsetY = 0.109f; // the orb sits a bit below the sprite's center (fraction of sprite size)
    private const int MaxParticles = 160;

    private struct Particle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float Size;     // star radius in pixels
        public float Rot;
        public float RotSpeed;
        public float Twinkle;
        public Vector3 Color;
    }

    private readonly Configuration config;
    private readonly IGameGui gameGui;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly ISharedImmediateTexture sheet;
    private readonly ISharedImmediateTexture glowTexture;
    private readonly ISharedImmediateTexture sparkleTexture;
    private readonly SpeechBubble bubble;
    private readonly List<Particle> particles = new();

    private Vector2? perchPos;       // where on screen she should hover while delivering an alert (HUD readout)
    private float perchUntil;
    private string? pendingTip;      // message that comes right after the "Hey!"
    private float pendingDelay;
    private float tipDuration;

    private Vector2 position;
    private Vector2 velocity;
    private Vector2 wander;
    private Vector2 relMin = new(-1f);   // her bounding box (and speech bubble) relative to her position
    private Vector2 relMax = new(1f);
    private float side = Random.Shared.Next(2) == 0 ? -1f : 1f; // -1 left shoulder, +1 right shoulder
    private float sideTimer = 8f;
    private Vector2 lastOrb;
    private float lastUnit = 1f;
    private float retargetTimer;
    private float spawnAccumulator;
    private float time;
    private bool initialized;

    public FairyRenderer(
        Configuration config,
        string assetsDir,
        ITextureProvider textureProvider,
        IGameGui gameGui,
        IObjectTable objectTable,
        ICondition condition)
    {
        this.config = config;
        this.gameGui = gameGui;
        this.objectTable = objectTable;
        this.condition = condition;
        sheet = textureProvider.GetFromFile(Path.Combine(assetsDir, "navi_wings_sheet.png"));
        glowTexture = textureProvider.GetFromFile(Path.Combine(assetsDir, "navi_glow.png"));
        sparkleTexture = textureProvider.GetFromFile(Path.Combine(assetsDir, "navi_sparkle.png"));
        bubble = new SpeechBubble(textureProvider, Path.Combine(assetsDir, "speech_bubble.png"));
    }

    /// <summary>Makes the fairy say something in a speech bubble, with a burst of sparkles.</summary>
    public void Say(string text)
    {
        bubble.Show(text);
        Burst();
    }

    /// <summary>"Hey!" immediately, then the actual message. Optionally she flies to the HUD readout (see SetPerch).</summary>
    public void Announce(string tip, float tipSeconds, bool perch)
    {
        const float heyLead = 0.85f;
        bubble.Show("Hey!", 1.0f);
        Burst();
        pendingTip = tip;
        pendingDelay = heyLead;
        tipDuration = tipSeconds;
        perchUntil = perch ? time + heyLead + tipSeconds + 0.6f : 0f;
    }

    /// <summary>True while she is supposed to be hovering over the HUD.</summary>
    public bool IsPerchActive => time < perchUntil;

    /// <summary>Screen position of the HUD readout to hover over (null = none).</summary>
    public void SetPerch(Vector2? position) => perchPos = position;

    /// <summary>Hides everything (used in PvP).</summary>
    public void Suspend()
    {
        initialized = false;
        particles.Clear();
        bubble.Dismiss();
        pendingTip = null;
        perchPos = null;
        perchUntil = 0f;
    }

    public void Draw(float dt)
    {
        var player = objectTable.LocalPlayer;
        if (player == null || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
        {
            initialized = false;
            particles.Clear();
            bubble.Dismiss();
            pendingTip = null;
            return;
        }

        if (!sheet.TryGetWrap(out var texture, out _) || texture == null)
            return;

        if (!gameGui.WorldToScreen(player.Position + new Vector3(0f, ShoulderHeight, 0f), out var shoulder))
            return; // behind the camera / off screen
        if (!gameGui.WorldToScreen(player.Position, out var feet))
            feet = shoulder + new Vector2(0f, 120f);

        var scale = config.Scale;
        var half = BaseSize * scale * 0.5f;
        var unit = BaseSize * scale / 48f;   // 1.0 at the default size (0.30x)
        var gs = ImGuiHelpers.GlobalScale;
        var pxPerYalm = MathF.Max(24f, MathF.Abs(feet.Y - shoulder.Y) / ShoulderHeight); // how big the character looks on screen
        var vp = ImGui.GetMainViewport();
        time += dt;

        if (pendingTip != null)
        {
            pendingDelay -= dt;
            if (pendingDelay <= 0f)
            {
                bubble.Show(pendingTip, tipDuration);
                pendingTip = null;
            }
        }

        // Pick a new spot to flutter to every second or two, and swap shoulders every now and then.
        retargetTimer -= dt;
        if (retargetTimer <= 0f)
        {
            retargetTimer = 1.2f + Random.Shared.NextSingle() * 1.6f;
            var angle = Random.Shared.NextSingle() * MathF.Tau;
            var radius = 0.7f + Random.Shared.NextSingle() * 0.9f;
            wander = new Vector2(MathF.Cos(angle) * 1.3f, MathF.Sin(angle) * 0.8f) * radius;
        }

        sideTimer -= dt;
        if (sideTimer <= 0f)
        {
            sideTimer = 8f + Random.Shared.NextSingle() * 6f;
            side = -side;
        }

        // Small constant hovering wobble.
        var bob = new Vector2(MathF.Sin(time * 3.1f) * 8f, MathF.Sin(time * 4.3f) * 10f) * MathF.Max(unit, 0.6f);

        // Where she wants to be: next to the character's shoulder, or over the HUD number she is talking about.
        Vector2 home;
        if (perchPos.HasValue && time < perchUntil)
        {
            var hover = half + 14f * unit;
            home = perchPos.Value + new Vector2(0f, -hover) + bob * 0.35f;
        }
        else
        {
            var sideDistance = MathF.Max(SideDistanceYalms * pxPerYalm, half * 1.8f + 16f * gs);
            var x = shoulder.X + side * sideDistance;
            if (x < vp.Pos.X + half || x > vp.Pos.X + vp.Size.X - half)
            {
                side = -side; // that side is off screen: use the other shoulder
                x = shoulder.X + side * sideDistance;
            }

            home = new Vector2(x, shoulder.Y - 0.15f * pxPerYalm) + wander * (WanderYalms * pxPerYalm) + bob;
        }

        // The speech bubble is rigidly attached to her (it never moves on its own), so while it is showing she stays far
        // enough from the side edges of the screen for the whole bubble to fit.
        var marginX = MathF.Max(half, MathF.Max(MathF.Abs(relMin.X), MathF.Abs(relMax.X)) + 4f * gs);
        var margins = new Vector2(marginX, half);

        var target = ClampToScreen(home, margins);

        // The mouse cursor: she AND her bubble move away together, by the shortest way out, in any direction,
        // without leaving the screen.
        var sprint = false;
        var mouse = ImGui.GetMousePos();
        if (mouse.X > -5000f && mouse.Y > -5000f)
        {
            var margin = MouseMargin * gs;
            var boxMin = relMin;
            var boxMax = relMax;

            float DistanceToMouse(Vector2 p)
            {
                var dx = MathF.Max(MathF.Max(p.X + boxMin.X - mouse.X, mouse.X - (p.X + boxMax.X)), 0f);
                var dy = MathF.Max(MathF.Max(p.Y + boxMin.Y - mouse.Y, mouse.Y - (p.Y + boxMax.Y)), 0f);
                return MathF.Sqrt(dx * dx + dy * dy);
            }

            // Threatened: she is too close right now. Otherwise only her usual spot is too close to the mouse.
            var threatened = DistanceToMouse(position) < margin;
            if (threatened || DistanceToMouse(target) < margin)
            {
                var origin = threatened ? position : target;
                var maxStep = MathF.Max(vp.Size.X, vp.Size.Y);
                var found = false;
                for (var step = 12f * gs; step <= maxStep && !found; step += 12f * gs)
                {
                    var best = target;
                    var bestScore = float.MaxValue;
                    for (var i = 0; i < 32; i++)
                    {
                        var angle = i * MathF.Tau / 32f;
                        var cand = ClampToScreen(origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * step, margins);
                        if (DistanceToMouse(cand) < margin)
                            continue;

                        var score = Vector2.Distance(cand, target); // among the shortest escapes, stay close to her usual spot
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = cand;
                            found = true;
                        }
                    }

                    if (found)
                        target = best;
                }
            }

            sprint = DistanceToMouse(position) < margin * 1.8f;
        }

        if (!initialized || Vector2.Distance(position, target) > 2500f)
        {
            position = target;
            velocity = Vector2.Zero;
            initialized = true;
            particles.Clear();
        }

        // Underdamped spring: she overshoots a little, which reads as flying. Much snappier when dodging the mouse.
        var stiffness = sprint ? 75f : 18f;
        var damping = 2f * MathF.Sqrt(stiffness) * (sprint ? 0.85f : 0.55f);
        var accel = (target - position) * stiffness - velocity * damping;
        velocity += accel * dt;
        position += velocity * dt;

        // She can never leave the screen: that guarantees her speech bubble always has room
        // (above her, or below her when she is hugging the top edge).
        var minP = vp.Pos + margins;
        var maxP = vp.Pos + vp.Size - margins;
        if (position.X < minP.X) { position.X = minP.X; velocity.X = MathF.Max(0f, velocity.X); }
        else if (position.X > maxP.X) { position.X = maxP.X; velocity.X = MathF.Min(0f, velocity.X); }
        if (position.Y < minP.Y) { position.Y = minP.Y; velocity.Y = MathF.Max(0f, velocity.Y); }
        else if (position.Y > maxP.Y) { position.Y = maxP.Y; velocity.Y = MathF.Min(0f, velocity.Y); }

        // Lean into the direction of travel.
        var tilt = Math.Clamp(velocity.X * 0.0012f / MathF.Max(scale, 0.3f), -0.45f, 0.45f);

        // Current flipbook frame.
        var frame = (int)(time * FlapFps) % FrameCount;
        var col = frame % Columns;
        var row = frame / Columns;
        var uv0 = new Vector2(col / (float)Columns, row / (float)Rows);
        var uv1 = new Vector2((col + 1) / (float)Columns, (row + 1) / (float)Rows);

        var cos = MathF.Cos(tilt);
        var sin = MathF.Sin(tilt);
        Vector2 Corner(float x, float y) => position + new Vector2(x * cos - y * sin, x * sin + y * cos);

        var size = half * 2f;
        var orb = Corner(0f, size * OrbOffsetY);
        lastOrb = orb;
        lastUnit = unit;

        UpdateParticles(dt, orb, unit);

        var dl = ImGui.GetBackgroundDrawList();
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 5.2f);
        var hasGlow = glowTexture.TryGetWrap(out var glow, out _) && glow != null;
        var hasSparkle = sparkleTexture.TryGetWrap(out var sparkle, out _) && sparkle != null;

        // 1) soft halo behind everything
        if (hasGlow)
            DrawSprite(dl, glow!.Handle, orb, size * (1.12f + 0.08f * pulse), 0f, new Vector4(1f, 1f, 1f, 0.50f + 0.18f * pulse));

        // 2) pixie dust
        if (hasSparkle)
        {
            foreach (var p in particles)
            {
                var t = p.Life / p.MaxLife; // 1 -> 0
                var twinkle = 0.65f + 0.35f * MathF.Sin(time * 14f + p.Twinkle);
                var alpha = Math.Clamp(t * 2.2f, 0f, 1f) * twinkle;
                var radius = p.Size * (0.45f + 0.55f * t);
                DrawSprite(dl, sparkle!.Handle, p.Pos, radius, p.Rot, new Vector4(p.Color, alpha));
            }
        }

        // 3) wings
        dl.AddImageQuad(
            texture.Handle,
            Corner(-half, -half), Corner(half, -half), Corner(half, half), Corner(-half, half),
            uv0, new Vector2(uv1.X, uv0.Y), uv1, new Vector2(uv0.X, uv1.Y),
            0xFFFFFFFF);

        // 4) bright orb body on top
        if (hasGlow)
            DrawSprite(dl, glow!.Handle, orb, size * (0.17f + 0.02f * pulse), 0f, new Vector4(1f, 1f, 1f, 1f));
        if (hasSparkle)
            DrawSprite(dl, sparkle!.Handle, orb, size * (0.22f + 0.05f * pulse), time * 0.6f, new Vector4(1f, 1f, 1f, 0.85f));

        bubble.Draw(position, half, dt);

        // Remember her bounding box together with her speech bubble: they are one thing as far as the mouse is concerned.
        relMin = new Vector2(-half);
        relMax = new Vector2(half);
        if (bubble.TryGetRect(out var bubbleMin, out var bubbleMax))
        {
            relMin = Vector2.Min(relMin, bubbleMin - position);
            relMax = Vector2.Max(relMax, bubbleMax - position);
        }
    }

    private static Vector2 ClampToScreen(Vector2 p, Vector2 margins)
    {
        var vp = ImGui.GetMainViewport();
        var minX = vp.Pos.X + margins.X;
        var minY = vp.Pos.Y + margins.Y;
        var maxX = MathF.Max(minX, vp.Pos.X + vp.Size.X - margins.X);
        var maxY = MathF.Max(minY, vp.Pos.Y + vp.Size.Y - margins.Y);
        return new Vector2(Math.Clamp(p.X, minX, maxX), Math.Clamp(p.Y, minY, maxY));
    }
}
