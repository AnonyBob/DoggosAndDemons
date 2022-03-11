using Mirror;
using UnityEngine;

namespace DefaultNamespace
{
    public class DogCameraHook : NetworkBehaviour
    {
        [SerializeField]
        private Transform _followTarget;
        
        private void Start()
        {
            if (isLocalPlayer)
            {
                var dogCamera = FindObjectOfType<DogCamera>();
                if (dogCamera != null)
                {
                    FindObjectOfType<DogCamera>().AssignDog(_followTarget);
                }
            }
        }
    }
}