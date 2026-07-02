using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public abstract class SpellBehaviourBase : ISpellBehaviour
    {
        public virtual void OnAttach(GameObject projectile) { }

        public virtual void OnUpdate(GameObject projectile) { }

        public virtual void OnHit(GameObject projectile, Collision collision) { }

        public virtual void OnDestroy(GameObject projectile) { }

        // Helper method to get the SpellProjectile component
        protected SpellProjectile GetProjectile(GameObject obj)
        {
            return obj.GetComponent<SpellProjectile>();
        }
    }
}
