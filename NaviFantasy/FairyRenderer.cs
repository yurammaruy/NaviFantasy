using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace NaviFantasy;

/// <summary>Moves the fairy around the player's on-screen position and draws her with ImGui.</summary>
public sealed class FairyRenderer
{
    private const int Columns = 4;
    private const int Rows = 2;
    private const int FrameCount = 8;
    private const float FlapFps = 60f;       // 8 frames -> ~7.5 flaps per second
    private const float BaseSize = 160f;     // pixels at Scale = 1
    private const float OrbitRadius = 110f;  // pixels at Scale = 1
    private const float HeightAbovePlayer = 2.3f; // world units (yalms) above the player's feet

    private readonly Configuration config;
    private readonly IGameGui gameGui;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly ISharedImmediateTexture sheet;

    private Vector2 position;
    private Vector2 velocity;
    private Vector2 offset;
    private float retargetTimer;
    private float time;
    private bool initialized;

    public FairyRenderer(
        Configuration config,
        string sheetPath,
        ITextureProvider textureProvider,
        IGameGui gameGui,
        IObjectTable objectTable,
        ICondition condition)
    {
        this.config = config;
        this.gameGui = gameGui;
        this.objectTable = objectTable;
        this.condition = condition;
        sheet = textureProvider.GetFromFile(sheetPath);
    }

    public void Draw(float dt)
    {
        var player = objectTable.LocalPlayer;
        if (player == null || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
        {
            initialized = false;
            return;
        }

        if (!sheet.TryGetWrap(out var texture, out _) || texture == null)
            return;

        if (!gameGui.WorldToScreen(player.Position + new Vector3(0f, HeightAbovePlayer, 0f), out var anchor))
            return; // behind the camera / off screen

        var scale = config.Scale;
        time += dt;

        // Pick a new spot to flutter to every second or two.
        retargetTimer -= dt;
        if (retargetTimer <= 0f)
        {
            retargetTimer = 1.2f + Random.Shared.NextSingle() * 1.6f;
            var angle = Random.Shared.NextSingle() * MathF.Tau;
            var radius = (0.7f + Random.Shared.NextSingle() * 0.9f) * OrbitRadius;
            offset = new Vector2(MathF.Cos(angle) * radius * 1.3f, MathF.Sin(angle) * radius * 0.8f - OrbitRadius * 0.25f);
        }

        // Small constant hovering wobble.
        var bob = new Vector2(MathF.Sin(time * 3.1f) * 8f, MathF.Sin(time * 4.3f) * 10f);
        var target = anchor + (offset + bob) * scale;

        if (!initialized || Vector2.Distance(position, target) > 2500f)
        {
            position = target;
            velocity = Vector2.Zero;
            initialized = true;
        }

        // Underdamped spring: she overshoots a little, which reads as flying.
        const float stiffness = 18f;
        var damping = 2f * MathF.Sqrt(stiffness) * 0.55f;
        var accel = (target - position) * stiffness - velocity * damping;
        velocity += accel * dt;
        position += velocity * dt;

        // Lean into the direction of travel.
        var tilt = Math.Clamp(velocity.X * 0.0012f / MathF.Max(scale, 0.3f), -0.45f, 0.45f);

        // Current flipbook frame.
        var frame = (int)(time * FlapFps) % FrameCount;
        var col = frame % Columns;
        var row = frame / Columns;
        var uv0 = new Vector2(col / (float)Columns, row / (float)Rows);
        var uv1 = new Vector2((col + 1) / (float)Columns, (row + 1) / (float)Rows);

        var half = BaseSize * scale * 0.5f;
        var cos = MathF.Cos(tilt);
        var sin = MathF.Sin(tilt);
        Vector2 Corner(float x, float y) => position + new Vector2(x * cos - y * sin, x * sin + y * cos);

        ImGui.GetBackgroundDrawList().AddImageQuad(
            texture.Handle,
            Corner(-half, -half), Corner(half, -half), Corner(half, half), Corner(-half, half),
            uv0, new Vector2(uv1.X, uv0.Y), uv1, new Vector2(uv0.X, uv1.Y),
            0xFFFFFFFF);
    }
}
