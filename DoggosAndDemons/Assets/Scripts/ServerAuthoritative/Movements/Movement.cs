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
        
        private MovementClientInput? _receivedInput;
        private MovementServerState? _receivedState;
        
        private const int MAX_PREDICTIONS = 20;
        private int _remainingServerPredications = 0;

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
            if (input.TickNumber < _receivedInput?.TickNumber)
            {
                return;
            }

            _remainingServerPredications = MAX_PREDICTIONS;
            _receivedInput = input;
        }

        [Server]
        private void ServerProcessReceivedInputs()
        {
            if (_receivedInput == null || _remainingServerPredications <= 0)
            {
                return;
            }

            var isNewInput = _remainingServerPredications == MAX_PREDICTIONS;
            ProcessInputs(_receivedInput.Value);
            _remainingServerPredications--;

            if (isNewInput)
            {
                var state = new MovementServerState()
                {
                    TickNumber = _receivedInput.Value.TickNumber,
                    Position = _body.position,
                    Rotation = _body.rotation,
                    AngularVelocity = _body.angularVelocity,
                    Velocity = _body.velocity
                };
                TargetSendState(connectionToClient, state);
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