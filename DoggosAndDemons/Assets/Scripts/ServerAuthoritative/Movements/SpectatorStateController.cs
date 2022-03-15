using System;
using Mirror;
using ServerAuthorative;
using UnityEngine;

namespace ServerAuthoritative.Movements
{
    public class SpectatorStateController : NetworkBehaviour
    {
        [SerializeField, Range(0, 1)]
        private float _predicationRatio = 0.9f;
        
        [SerializeField]
        private Rigidbody2D _body;
        
        private SpectatorMovementState? _lastReceivedState;

        //Velocity from the previous simulate.
        private Vector2 _lastVelocity;
        
        //Angular velocity from the previous simulate.
        private float _lastAngularVelocity;

        //Baseline for the velocity magnitude. We can use this to determine how different the velocity was.
        private float? _velocityBaseline;
        
        //Baseline for the angular velocity magnitude. We can use this to determine how different the velocity was.
        private float? _angularVelocityBaseline;
        
        private void OnEnable()
        {
            if (!NetworkServer.active)
            {
                SubscribeToEvents(true);
            }
            else
            {
                TickManager.OnUpdate += OnUpdate;
            }
        }

        private void OnDisable()
        {
            SubscribeToEvents(false);
            TickManager.OnUpdate -= OnUpdate;
        }

        private void SubscribeToEvents(bool subscribe)
        {
            if (subscribe)
            {
                SpectatorRollbackManager.OnRollbackStart += OnRollbackStart;
                TickManager.OnPostSimulate += OnPostSimulate;
            }
            else
            {
                SpectatorRollbackManager.OnRollbackStart -= OnRollbackStart;
                TickManager.OnPostSimulate -= OnPostSimulate;
            }
        }

        private void OnRollbackStart()
        {
            if (hasAuthority || !_lastReceivedState.HasValue)
            {
                return;
            }
            
            _body.position = _lastReceivedState.Value.Position;
            _body.rotation = _lastReceivedState.Value.Rotation;
            _body.velocity = _lastReceivedState.Value.Velocity;
            _body.angularVelocity = _lastReceivedState.Value.AngularVelocity;
            
            //Set the prediction defaults.
            _velocityBaseline = null;
            _angularVelocityBaseline = null;

            _lastVelocity = _body.velocity;
            _lastAngularVelocity = _body.angularVelocity;
        }

        private void OnUpdate()
        {
            if (isServer)
            {
                SendStates();
            }
        }
        
        private void OnPostSimulate(float deltaTime, bool isReplay)
        {
            //This is where we apply our predictions.
            if (hasAuthority)
            {
                return;
            }

            if (_predicationRatio == 0)
            {
                return;
            }

            PredictVelocity();
            PredictAngularVelocity();

            _lastVelocity = _body.velocity;
            _lastAngularVelocity = _body.angularVelocity;
        }

        private void PredictVelocity()
        {
            var velocityDifference = 0f;
            var directionDifference = 0f;

            //If we have a baseline established then we can use the difference in the last velocity and the current
            //velocity to calculate how much direction we've changed.
            directionDifference = _velocityBaseline.HasValue
                ? Vector2.SqrMagnitude(_lastVelocity.normalized - _body.velocity.normalized)
                : 0f;
            
            //If the direction has changed too much then reset the baseline.
            if (directionDifference > 0.01f)
            {
                _velocityBaseline = null;
            }
            else
            {
                //If our direction hasn't changed significantly then we can use the difference in velocity to
                //establish the initial baseline. But if a baseline did exist then we only want to update it
                //if the difference is different than the original baseline by 10%. If that is the case then the velocity
                //has changed too much for the existing baseline to continue being useful so we need to reset.
                velocityDifference = (_lastVelocity - _body.velocity).magnitude;
                if (_velocityBaseline == null)
                {
                    if(velocityDifference > 0)
                        _velocityBaseline = velocityDifference;
                }
                else if (velocityDifference > (_velocityBaseline * 1.1f) || velocityDifference < (_velocityBaseline * 0.9f))
                {
                    _velocityBaseline = null;
                }
                else //The difference is close enough to the established baseline that we can use predictions
                {
                    _body.velocity = Vector2.Lerp(_body.velocity, _lastVelocity, _predicationRatio);
                }
            }
        }

        private void PredictAngularVelocity()
        {
            var velocityDifference = 0f;
            var directionDifference = 0f;
            
            //If we have a baseline established then we can use the difference in the last velocity and the current
            //velocity to calculate how much direction we've changed.
            directionDifference = _angularVelocityBaseline.HasValue
                ? Mathf.Abs(Mathf.Sign(_lastAngularVelocity) - Mathf.Sign(_body.angularVelocity))
                : 0f;

            if (directionDifference > 0.01f)
            {
                _angularVelocityBaseline = null;
            }
            else
            {
                velocityDifference = Mathf.Abs(_lastAngularVelocity - _body.angularVelocity);
                if (_angularVelocityBaseline == null)
                {
                    if(velocityDifference > 0)
                        _angularVelocityBaseline = velocityDifference;
                }
                else if (velocityDifference > (_angularVelocityBaseline * 1.1f) || velocityDifference < (_angularVelocityBaseline * 0.9f))
                {
                    _angularVelocityBaseline = null;
                }
                else //The difference is close enough to the established baseline that we can use predictions
                {
                    _body.angularVelocity = Mathf.Lerp(_body.angularVelocity, _lastAngularVelocity, _predicationRatio);
                }
            }
        }

        [Server]
        private void SendStates()
        {
            var state = new SpectatorMovementState()
            {
                Position = _body.position,
                Rotation = _body.rotation,
                AngularVelocity = _body.angularVelocity,
                Velocity = _body.velocity
            };

            RpcSendState(state);
        }

        [ClientRpc(channel = 1, includeOwner = false)]
        private void RpcSendState(SpectatorMovementState state)
        {
            if (isServer)
                return;

            _lastReceivedState = state;
        }
    }
}