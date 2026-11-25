namespace NaviFantasy;

public enum AlertKind
{
    OwnHp,
    OwnMp,
    AllyNeedsHeal,
    AlliesDamaged,
    BossCast,
    DangerZone,
    Test,
}

/// <summary>Which HUD readout the fairy should fly to while delivering the message.</summary>
public enum HudPerch
{
    None,
    Hp,
    Mp,
}

public sealed record Alert(AlertKind Kind, string Text, int Priority, HudPerch Perch, float Duration);
