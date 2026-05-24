using UnityEngine;

namespace SandBlast
{
    public class PlayerCamera : MonoBehaviour
    {
        public Transform Target;
        public float     SmoothTime = 0.15f;

        Vector3 _velocity;

        void LateUpdate()
        {
            if (Target == null) return;

            Vector3 goal = new Vector3(Target.position.x, Target.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, SmoothTime);
        }
    }
}
