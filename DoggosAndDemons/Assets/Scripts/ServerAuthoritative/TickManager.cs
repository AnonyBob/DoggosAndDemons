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

        public static event Action<float, bool> OnPresimulate
        {
            add => Instance._onPreSimulate += value;
            remove => Instance._onPreSimulate -= value;
        }

        public static event Action<float, bool> OnPostSimulate
        {
            add => Instance._onPostSimulate += value;
            remove => Instance._onPostSimulate -= value;
        }

        public static void UpdateTimingStep(sbyte timingStepChange)
        {
            Instance._tickRate = Mathf.Clamp(Instance._tickRate + (timingStepChange * TICK_STEP_CHANGE),
                TICK_SPEED_RANGE[0], TICK_SPEED_RANGE[1]);
        }

        public static void Simulate(float deltaTime, bool isReplay)
        {
            Instance._onPreSimulate?.Invoke(deltaTime, isReplay);
            Physics.Simulate(deltaTime);
            
            Instance._onPostSimulate?.Invoke(deltaTime, isReplay);
        }

        private uint _tickNumber;
        
        private event Action _onPreUpdate;
        private event Action _onUpdate;
        private event Action _onPostUpdate;
        private event Action<float, bool> _onPreSimulate;
        private event Action<float, bool> _onPostSimulate;

        private float _timeBetweenTicks = 0;
        
        private float _tickRate = 0.02f;
        private PhysicsScene2D _physics;

        private const float TICK_STEP_CHANGE = 0.0002f; //What we change our tick rate by to increase speed of sim. 1/2% of 0.02f
        private const float TICK_RECOVER_RATE = 0.0025f; //How quickly we try to return to default tick rate: 0.02
        private static readonly float[] TICK_SPEED_RANGE = new float[] { 0.027f, 0.013f }; //35% up or down.

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
                
                Simulate(Time.fixedDeltaTime, false);
                _onPostUpdate?.Invoke();
            }

            _tickRate = Mathf.MoveTowards(_tickRate, Time.fixedDeltaTime, TICK_RECOVER_RATE);
        }
    }
}