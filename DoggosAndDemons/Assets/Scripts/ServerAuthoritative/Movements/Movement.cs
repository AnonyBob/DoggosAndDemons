using System;
using System.Collections.Generic;
using Mirror;
using ServerAuthorative;
using UnityEngine;

namespace ServerAuthoritative.Movements
{
    public class Movement : NetworkBehaviour
    {
        [SerializeField]
        private float _moveRate;
        
        private Rigidbody2D _body;
        private DoggosAndDemons _inputHandler;

        private readonly List<MovementClientInput> _clientInputs = new List<MovementClientInput>();
        private readonly List<MovementClientInput> _receivedInputs = new List<MovementClientInput>();
        
        private MovementServerState? _receivedState;
        
        private const int MAX_RECEIVED_INPUTS = 10;

        private void Awake()
        {
            TickManager.OnUpdate += TickManager_PerformUpdate;
            _body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            _inputHandler = new DoggosAndDemons();
            _inputHandler.Player.Enable();
        }

        private void OnDestroy()
        {
            TickManager.OnUpdate -= TickManager_PerformUpdate;
        }

        private void TickManager_PerformUpdate()
        {
            if (hasAuthority)
            {
                ProcessReceivedStates(); 
                ClientProcessInput();
            }

            if (isServer)
            {
                ServerProcessReceivedInputs();
            }
        }

        private void ProcessInputs(MovementClientInput input)
        {
            var movement = new Vector2(input.Horizontal * _moveRate, input.Vertical * _moveRate);
            _body.AddForce(movement, ForceMode2D.Force);
        }

        [Command]
        private void CmdSendInputs(MovementClientInput input)
        {
            //Don't send input to the server if you are the server.
            if (isClient && hasAuthority)
            {
                return;
            }
            
            _receivedInputs.Add(input);
            if (_receivedInputs.Count > MAX_RECEIVED_INPUTS)
            {
                _receivedInputs.RemoveAt(0);
            }
        }

        [Server]
        private void ServerProcessReceivedInputs()
        {
            //Don't process received inputs on the local server object.
            if (isClient && hasAuthority)
            {
                return;
            }
            
            sbyte timingStepChange = 0;
            
            //If the client hasn't sent an input then we need to accelerate its simulation.
            if (_receivedInputs.Count == 0)
            {
                timingStepChange = -1;
            }
            //If the client has sent too many inputs then we need to slow down its simulation.
            else if (_receivedInputs.Count > 1)
            {
                timingStepChange = 1;
            }

            if (_receivedInputs.Count > 0)
            {
                var receivedInput = _receivedInputs[0];
                _receivedInputs.RemoveAt(0);
                
                ProcessInputs(receivedInput);
                var state = new MovementServerState()
                {
                    TickNumber = receivedInput.TickNumber,
                    Position = _body.position,
                    Rotation = _body.rotation,
                    AngularVelocity = _body.angularVelocity,
                    Velocity = _body.velocity,
                    TimingStepChange = timingStepChange
                };
                
                TargetSendState(connectionToClient, state);
            }
            else if(timingStepChange != 0)
            {
                TargetSendTimeStepChange(connectionToClient, timingStepChange);
            }
        }

        [TargetRpc]
        private void TargetSendState(NetworkConnection connection, MovementServerState state)
        {
            //Don't process old states.
            if (_receivedState != null && _receivedState.Value.TickNumber > state.TickNumber)
            {
                return;
            }

            _receivedState = state;
        }

        [TargetRpc]
        private void TargetSendTimeStepChange(NetworkConnection connection, sbyte stepChange)
        {
            TickManager.UpdateTimingStep(stepChange);
        }
        
        [Client]
        private void ClientProcessInput()
        {
            var movement = _inputHandler.Player.Move.ReadValue<Vector2>();
            var latestInput = new MovementClientInput()
            {
                Horizontal = movement.x,
                Vertical = movement.y,
                TickNumber = TickManager.TickNumber,
            };
            
            _clientInputs.Add(latestInput);
            if (!isServer)
            {
                ProcessInputs(latestInput);
            }
            
            CmdSendInputs(latestInput);
        }

        [Client]
        private void ProcessReceivedStates()
        {
            if (_receivedState == null)
            {
                return;
            }

            var currentState = _receivedState.Value;
            _receivedState = null;
            
            TickManager.UpdateTimingStep(currentState.TimingStepChange);
            var index = _clientInputs.FindIndex(x => x.TickNumber == currentState.TickNumber);
            if (index >= 0)
            {
                _clientInputs.RemoveRange(0, index);
            }

            _body.position = currentState.Position;
            _body.rotation = currentState.Rotation;
            _body.velocity = currentState.Velocity;
            _body.angularVelocity = currentState.AngularVelocity;
            Physics2D.SyncTransforms();

            foreach (var input in _clientInputs)
            {
                ProcessInputs(input);
                TickManager.Physics.Simulate(TickManager.TickRate);
            }
        }
        
        [Client]
        private void CancelVelocity(bool useForces)
        {
            if (useForces)
            {
                _body.AddForce(-_body.velocity, ForceMode2D.Impulse);
                _body.AddTorque(-_body.angularVelocity, ForceMode2D.Impulse);
            }
            else
            {
                _body.velocity = Vector2.zero;
                _body.angularVelocity = 0;
            }
        }
    }
}