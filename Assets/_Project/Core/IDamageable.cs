namespace LastPatrol.Core
{
    public enum DamageSource
    {
        Enemy,
        Robot,
        Environment
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount, DamageSource source);
    }
}
