using UnityEngine;

namespace ServerAuthoritative.Movements
{
    public struct MovementServerState
    {
        public uint TickNumber;
        
        public Vector2 Position;
        public float Rotation;

        public Vector2 Velocity;
        public float AngularVelocity;
    }
}