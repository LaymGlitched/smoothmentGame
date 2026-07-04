using UnityEngine;

namespace GameCode.Environment
{
    public class MistBehaviour : KillTrigger
    {
        public float WorldSpaceMistYLevel;
        public GameObject Player;

        void Update()
        {
            Vector3 position = new Vector3(
                Player.transform.position.x,
                WorldSpaceMistYLevel,
                Player.transform.position.z
            );
            this.transform.position = position;
        }
    }
}
