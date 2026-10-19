using UnityEngine;

namespace NoSleep
{
    public enum CombatResult { Victory, Escaped, Defeat, Cancelled }
    public enum CombatAction { Strike, Guard, Flee, Aggressive, Observe, UseStimulant }

    [CreateAssetMenu(menuName = "No Sleep/Enemy", fileName = "Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string enemyId = "enemy";
        public string displayName = "낯선 존재";
        [Min(1)] public int baseHealth = 30;
        [Min(1)] public int baseAttack = 9;
        public bool retreatAllowed = true;
    }
}
