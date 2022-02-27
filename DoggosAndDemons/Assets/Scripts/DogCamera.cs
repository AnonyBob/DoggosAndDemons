using Cinemachine;
using UnityEngine;

public class DogCamera : MonoBehaviour
{
    [SerializeField]
    private CinemachineVirtualCamera _camera;

    public void AssignDog(Transform target)
    {
        _camera.Follow = target;
    }
}