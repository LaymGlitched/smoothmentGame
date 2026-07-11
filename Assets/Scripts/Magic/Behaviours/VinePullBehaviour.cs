using GameCode.Shared;
using UnityEngine;

namespace GameCode.Magic
{
    public class VinePullBehaviour : SpellBehaviourBase
    {
        public float PullForceCaster;
        public float PullForceTarget;
        public float UpwardAssist;
        public Rigidbody CasterRb;

        private bool hasHit = false;

        public override void OnHit(GameObject projectile, Collision collision)
        {
            if (hasHit) return;
            hasHit = true;

            GameObject target = collision.gameObject;
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            var damageable = target.GetComponent<IDamageable>();
            
            Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : projectile.transform.position;

            if (damageable != null || targetRb != null)
            {
                // Hit an enemy or dynamic physics object -> Pull them to caster
                if (targetRb != null && CasterRb != null)
                {
                    Vector3 pullDir = (CasterRb.transform.position - target.transform.position).normalized;
                    targetRb.AddForce(pullDir * PullForceTarget + Vector3.up * UpwardAssist, ForceMode.Impulse);
                }
            }
            else
            {
                // Hit static geometry -> Pull caster to hit point
                if (CasterRb != null)
                {
                    Vector3 pullDir = (hitPoint - CasterRb.transform.position).normalized;
                    
                    // Reset vertical velocity before pulling for snappier feel if we are falling
                    Vector3 vel = CasterRb.linearVelocity;
                    if (vel.y < 0) vel.y = 0;
                    CasterRb.linearVelocity = vel;
                    
                    CasterRb.AddForce(pullDir * PullForceCaster + Vector3.up * UpwardAssist, ForceMode.Impulse);
                }
            }
        }
    }
}
