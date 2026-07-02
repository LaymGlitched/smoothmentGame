using UnityEngine;

namespace GameCode.Shared
{
    public interface IDamageable
    {
        void TakeDamage(float damage, DamageType damageType);
    }
}
