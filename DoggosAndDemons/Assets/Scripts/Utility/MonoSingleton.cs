using UnityEngine;

namespace DefaultNamespace
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<T>();
                    if (_instance == null)
                    {
                        var go = new GameObject(typeof(T).Name, typeof(T));
                        DontDestroyOnLoad(go);

                        _instance = go.GetComponent<T>();
                    }
                    _instance.Initialize();
                }

                return _instance;
            }
        }

        private static T _instance;
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)this;
                _instance.Initialize();
            }
            else
            {
                Destroy(this);
            }
        }

        protected virtual void Initialize()
        {
            //Override for local usages.
        }
    }
}