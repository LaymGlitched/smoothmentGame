using UnityEngine;
using UnityEngine.Events;

namespace GameCode.Environment
{
    public class EventForce : MonoBehaviour
    {
        public Rigidbody[] rbs;
        public ForceMode forceMode = ForceMode.Force;
        public Vector3 worldForceDirection = new Vector3(0f, 10f, 0f);
        public bool isExplosionForce = false;
        public float explosionForce = 10f;
        public Transform explosionPosition;
        public float explosionRadius;

        public void CreateForce()
        {
            if(isExplosionForce)
            {
                foreach(Rigidbody rb in rbs)
                {
                    rb.AddExplosionForce(
                        explosionForce,
                        explosionPosition.position,
                        explosionRadius,
                        1,
                        forceMode
                    );
                }
            }
            else
            {
                foreach (Rigidbody rb in rbs)
                {
                    rb.AddForce(worldForceDirection, forceMode);
                }
            }
        }
    }
}
