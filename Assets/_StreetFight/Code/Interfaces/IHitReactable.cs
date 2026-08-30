using StreetFight.ScriptableObjects;
using UnityEngine;

namespace StreetFight.Code.Interfaces
{
    public interface IHitReactable
    {
        void ReactToHit(AttackDataSO attack, GameObject attacker);
    }
}