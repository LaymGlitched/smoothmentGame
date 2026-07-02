using UnityEngine;

namespace GameCode.Shared
{
    public interface ISpellBehaviour
    {
        void OnAttach(GameObject projectile);
        void OnUpdate(GameObject projectile);
        void OnHit(GameObject projectile, Collision collision);
        void OnDestroy(GameObject projectile);
    }
}
