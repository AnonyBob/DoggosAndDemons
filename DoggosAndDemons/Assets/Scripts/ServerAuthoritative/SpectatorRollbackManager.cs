using System;
using DefaultNamespace;
using UnityEngine;

namespace ServerAuthorative
{
    public class SpectatorRollbackManager : MonoSingleton<SpectatorRollbackManager>
    {
        public static event Action OnRollbackStart
        {
            add => Instance._onRollbackStart += value;
            remove => Instance._onRollbackStart -= value;
        }

        public static event Action OnRollbackEnd
        {
            add => Instance._onRollbackEnd += value;
            remove => Instance._onRollbackEnd -= value;
        }
        
        private event Action _onRollbackStart;
        private event Action _onRollbackEnd;

        protected override void Initialize()
        {
            DontDestroyOnLoad(gameObject);
        }

        public static void StartRollback()
        {
            Instance._onRollbackStart?.Invoke();
            Physics.SyncTransforms();
            Physics2D.SyncTransforms();
        }

        public static void EndRollback()
        {
            Instance._onRollbackEnd?.Invoke();
        }
    }
}