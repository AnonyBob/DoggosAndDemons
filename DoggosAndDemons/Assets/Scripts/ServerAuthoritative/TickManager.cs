using System;
using DefaultNamespace;
using UnityEngine;

namespace ServerAuthorative
{
    public class TickManager : MonoSingleton<TickManager>
    {
        public static uint TickNumber => Instance._tickNumber;

        public static float TickRate => Instance._tickRate;

        public static PhysicsScene2D Physics => Instance._physics;
        
        public static event Action OnPreUpdate
        {
            add => Instance._onPreUpdate += value;
            remove => Instance._onPreUpdate -= value;
        }
        
        public static event Action OnUpdate
        {
            add => Instance._onUpdate += value;
            remove => Instance._onUpdate -= value;
        }
        
        public static event Action OnPostUpdate
        {
            add => Instance._onPostUpdate += value;
            remove => Instance._onPostUpdate -= value;
        }
        
        private uint _tickNumber;
        
        private event Action _onPreUpdate;
        private event Action _onUpdate;
        private event Action _onPostUpdate;

        private float _timeBetweenTicks = 0;
        private float _tickRate = 0.02f;
        private PhysicsScene2D _physics;

        protected override void Initialize()
        {
            _physics = gameObject.scene.GetPhysicsScene2D();
        }

        private void Update()
        {
            _timeBetweenTicks += Time.deltaTime;
            while (_timeBetweenTicks >= _tickRate)
            {
                _timeBetweenTicks -= _tickRate;
                if (_tickNumber == uint.MaxValue)
                {
                    _tickNumber = 0;
                }
                _tickNumber++;
                
                _onPreUpdate?.Invoke();
                _onUpdate?.Invoke();
                
                _physics.Simulate(_tickRate);
                _onPostUpdate?.Invoke();
            }
        }
    }
}