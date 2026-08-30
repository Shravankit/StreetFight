namespace StreetFight.Enum
{
    public enum AttackInputType
    {
        Punch,
        Kick,
        HeavyPunch,
        HeavyKick,
        Grab,
        Special
    }

    public enum AttackCategory
    {
        Light,
        Heavy,
        Special,
        Grab,
        Counter
    }

    public enum CombatState
    {
        Idle,
        Attacking,
        Blocking,
        Dodging,
        Stunned
    }
}
