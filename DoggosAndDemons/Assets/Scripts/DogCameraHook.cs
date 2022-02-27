using Mirror;

namespace DefaultNamespace
{
    public class DogCameraHook : NetworkBehaviour
    {
        private void Start()
        {
            if (isLocalPlayer)
            {
                FindObjectOfType<DogCamera>().AssignDog(transform);
            }
        }
    }
}