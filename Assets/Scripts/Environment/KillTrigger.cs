using GameCode.PlayerScripts;
using GameCode.Shared;
using Nanodogs.API.NanoHealth;
using Nanodogs.UniversalScripts;

namespace GameCode.Environment
{
    public class KillTrigger : NanoTrigger
    {
        public bool DoDamageInstead;
        public int Damage;

        public DamageType damageType = Shared.DamageType.Holy;

        void OnTriggerEnter(UnityEngine.Collider other)
        {
            Health health = other.GetComponent<Health>();
            if (!DoDamageInstead)
            {
                health.TakeDamage(health.MaxHealth, damageType);
            }
            else
            {
                health.TakeDamage(Damage, damageType);
            }
        }
    }
}
