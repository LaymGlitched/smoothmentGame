using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class RicochetBehaviour : SpellBehaviourBase
    {
        public int MaxBounces = 3;
        public LayerMask BounceMask = -1;

        private int bounceCount;

        public override void OnHit(GameObject projectileObject, Collision collision)
        {
            var projectile = GetProjectile(projectileObject);
            if (projectile == null)
                return;

            if (bounceCount >= MaxBounces)
                return;

            // Check if this surface should be bounced off
            int layerMask = 1 << collision.gameObject.layer;
            if ((layerMask & BounceMask) == 0)
                return;

            // Calculate reflection
            Vector3 incomingDirection = projectile.Direction;
            Vector3 normal = collision.contacts[0].normal;
            Vector3 newDirection = Vector3.Reflect(incomingDirection, normal);

            // Apply new direction
            projectile.Direction = newDirection;

            var rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = newDirection * projectile.Speed;
            }

            // Update rotation
            projectile.transform.forward = newDirection;

            bounceCount++;

            // IMPORTANT: Prevent the projectile from being destroyed
            projectile.PreventDestruction();

            Debug.Log($"Ricochet! Bounce {bounceCount}/{MaxBounces}");
        }
    }
}
