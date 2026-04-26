using UnityEngine;

namespace LastPatrol.Data
{
    public enum EnemyType { Humanoid, Drone, Curator, Hunter, Ambush }

    [CreateAssetMenu(fileName = "ED_NewEnemy", menuName = "LastPatrol/Enemy Data")]
    public class EnemyDataSO : ScriptableObject
    {
        public string enemyId;
        public EnemyType type = EnemyType.Humanoid;

        [Header("Health")]
        public float maxHP = 30f;

        [Header("Engagement")]
        public float losRange = 12f;
        public float aimTimeSeconds = 1.2f;
        public float cooldownSeconds = 1.5f;

        [Header("Bullet")]
        public float bulletSpeed = 18f;
        public float damageVsCop = 25f;
        public float damageVsRobot = 15f;
    }
}
