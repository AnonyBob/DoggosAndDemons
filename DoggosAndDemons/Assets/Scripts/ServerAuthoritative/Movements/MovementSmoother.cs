using System;
using Mirror;
using ServerAuthorative;
using UnityEngine;

namespace ServerAuthoritative.Movements
{
    public class MovementSmoother : NetworkBehaviour
    {
    #region Lerp Method
        // [SerializeField]
        // private Transform _smoothTarget;
        //
        // private bool _subscribed = false;
        //
        // //Position and rotation before simulation is performed.
        // private Vector3 _prePosition;
        // private Quaternion _preRotation;
        //
        // private Vector3 _targetPosition;
        // private Quaternion _targetRotation;
        //
        // private float _timePassedSinceLastFixedFrame = 0;
        //
        // //Multiplier that is used to adjust the delta time so that we can get back to original position before physics
        // private const float FRAME_TIME_MULTIPLIER = 0.75f;
        //
        // public override void OnStartAuthority()
        // {
        //     base.OnStartAuthority();
        //     ChangeSubscribeUpdates(true);
        // }
        //
        // public override void OnStopAuthority()
        // {
        //     base.OnStopAuthority();
        //     ChangeSubscribeUpdates(false);
        // }
        //
        // private void OnEnable()
        // {
        //     if(hasAuthority)
        //         ChangeSubscribeUpdates(true);
        // }
        //
        // private void OnDisable()
        // {
        //     ChangeSubscribeUpdates(false);
        // }
        //
        // private void Update()
        // {
        //     if(hasAuthority)
        //         ApplySmooth();
        // }
        //
        // private void ApplySmooth()
        // {
        //     //Get the percent difference between the previous and next fixed frame based on the tick rate.
        //     _timePassedSinceLastFixedFrame += (Time.deltaTime * FRAME_TIME_MULTIPLIER);
        //     var percent = Mathf.InverseLerp(0, TickManager.TickRate, _timePassedSinceLastFixedFrame);
        //
        //     _smoothTarget.position = Vector3.Lerp(_prePosition, _smoothTarget.root.position, percent);
        //     _smoothTarget.rotation = Quaternion.Slerp(_preRotation, _smoothTarget.root.rotation, percent);
        // }
        //
        // private void ChangeSubscribeUpdates(bool subscribe)
        // {
        //     if (subscribe == _subscribed)
        //     {
        //         return;
        //     }
        //
        //     if (subscribe)
        //     {
        //         TickManager.OnPreUpdate += TickManagerOnOnPreUpdate;
        //         TickManager.OnPostUpdate += TickManagerOnOnPostUpdate;
        //     }
        //     else
        //     {
        //         TickManager.OnPreUpdate -= TickManagerOnOnPreUpdate;
        //         TickManager.OnPostUpdate -= TickManagerOnOnPostUpdate;
        //     }
        //
        //     _subscribed = subscribe;
        // }
        //
        // private void TickManagerOnOnPreUpdate()
        // {
        //     _smoothTarget.position = _smoothTarget.root.position;
        //     _smoothTarget.rotation = _smoothTarget.root.rotation;
        //     
        //     _prePosition = _smoothTarget.position;
        //     _preRotation = _smoothTarget.rotation;
        // }
        //
        // private void TickManagerOnOnPostUpdate()
        // {
        //     _timePassedSinceLastFixedFrame = 0;
        //     _smoothTarget.position = _prePosition;
        //     _smoothTarget.rotation = _preRotation;
        // }
    #endregion
    
        [SerializeField]
        private Transform _smoothTarget;

        [SerializeField]
        private float _smoothRate = 20f;
        
        private bool _subscribed = false;
        
        //Position and rotation before simulation is performed.
        private Vector3 _prePosition;
        private Quaternion _preRotation;

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            ChangeSubscribeUpdates(true);
        }

        public override void OnStopAuthority()
        {
            base.OnStopAuthority();
            ChangeSubscribeUpdates(false);
        }

        private void OnEnable()
        {
            if(hasAuthority)
                ChangeSubscribeUpdates(true);
        }

        private void OnDisable()
        {
            ChangeSubscribeUpdates(false);
        }

        private void Update()
        {
            if(hasAuthority)
                ApplySmooth();
        }

        private void ApplySmooth()
        {
            //Smooth back to the target position and rotation over time. If not at zero and identity then we need
            //store that offset.
            var distance = Mathf.Max(0.01f, Vector3.Distance(_smoothTarget.localPosition, Vector3.zero));
            _smoothTarget.localPosition = Vector3.MoveTowards(_smoothTarget.localPosition, Vector3.zero,
                distance * _smoothRate * Time.deltaTime);
            
            distance = Mathf.Max(1, Quaternion.Angle(_smoothTarget.localRotation, Quaternion.identity));
            _smoothTarget.localRotation = Quaternion.RotateTowards(_smoothTarget.localRotation, Quaternion.identity, 
                distance * _smoothRate * Time.deltaTime);
        }

        private void ChangeSubscribeUpdates(bool subscribe)
        {
            if (subscribe == _subscribed)
            {
                return;
            }

            if (subscribe)
            {
                TickManager.OnPreUpdate += TickManagerOnOnPreUpdate;
                TickManager.OnPostUpdate += TickManagerOnOnPostUpdate;
            }
            else
            {
                TickManager.OnPreUpdate -= TickManagerOnOnPreUpdate;
                TickManager.OnPostUpdate -= TickManagerOnOnPostUpdate;
            }

            _subscribed = subscribe;
        }
        
        private void TickManagerOnOnPreUpdate()
        {
            _prePosition = _smoothTarget.position;
            _preRotation = _smoothTarget.rotation;
        }
        
        private void TickManagerOnOnPostUpdate()
        {
            _smoothTarget.position = _prePosition;
            _smoothTarget.rotation = _preRotation;
        }
    }
}