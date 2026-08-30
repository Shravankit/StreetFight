using UnityEngine;

namespace StreetFight.Code.Interfaces
{
    /// <summary>Implement this on anything CombatController's hit detection should be able to damage.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, GameObject source);
    }
}