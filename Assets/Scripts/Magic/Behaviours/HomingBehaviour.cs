using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class HomingBehaviour : SpellBehaviourBase
    {
        public float TurnSpeed = 10f;
        public float DetectionRadius = 20f;
        public LayerMask TargetMask = -1;

        private Transform target;

        public override void OnUpdate(GameObject projectileObject)
        {
            var projectile = GetProjectile(projectileObject);
            if (projectile == null)
                return;

            if (target == null)
            {
                FindTarget(projectile);
                return;
            }

            // Calculate direction to target
            Vector3 directionToTarget = (
                target.position - projectile.transform.position
            ).normalized;

            // Smoothly rotate towards target
            Vector3 newDirection = Vector3.RotateTowards(
                projectile.Direction,
                directionToTarget,
                TurnSpeed * Time.deltaTime,
                0f
            );

            projectile.Direction = newDirection;

            // Update rigidbody velocity
            var rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = newDirection * projectile.Speed;
            }

            // Update rotation
            projectile.transform.forward = newDirection;
        }

        private void FindTarget(SpellProjectile projectile)
        {
            Collider[] colliders = Physics.OverlapSphere(
                projectile.transform.position,
                DetectionRadius,
                TargetMask
            );

            float closestDistance = float.MaxValue;
            foreach (var collider in colliders)
            {
                if (collider.gameObject == projectile.Caster)
                    continue;

                float distance = Vector3.Distance(
                    projectile.transform.position,
                    collider.transform.position
                );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    target = collider.transform;
                }
            }
        }
    }
}
