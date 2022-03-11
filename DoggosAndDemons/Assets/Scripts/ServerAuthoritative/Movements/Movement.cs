using System;
using System.Collections.Generic;
using Mirror;
using ServerAuthorative;
using UnityEngine;

namespace ServerAuthoritative.Movements
{
    public class Movement : NetworkBehaviour
    {
        public class CapturedInput
        {
            public bool SprintHeld = false;
            public bool BarkPressed = false;
        }
        
        [SerializeField, Header("Normal")]
        private float _normalMaxSpeed = 2f;

        [SerializeField]
        private float _normalAcceleration = 20;
        
        [SerializeField, Header("Sprint")]
        private float _sprintingMaxSpeed = 2.5f;

        [SerializeField]
        private float _sprintingAcceleration = 30;

        [SerializeField, Header("Drag")]
        private float _stoppingDrag = 2;

        [SerializeField]
        private float _movingDrag = 0;
        
        private Rigidbody2D _body;
        private DoggosAndDemons _inputHandler;

        private readonly List<MovementClientInput> _clientInputs = new List<MovementClientInput>();
        
        private readonly List<MovementClientInput> _receivedInputs = new List<MovementClientInput>();
        private uint _lastInputTickReceived = 0;
        
        private MovementServerState? _receivedState;
        private CapturedInput _capturedInput = new CapturedInput();
        
        private const int MAX_RECEIVED_INPUTS = 10;
        private const int NUMBER_INPUTS_TO_SEND = 3;

        private void Awake()
        {
            TickManager.OnUpdate += TickManager_PerformUpdate;
            _body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            _inputHandler = new DoggosAndDemons();
            _inputHandler.Player.Enable();

            if (!hasAuthority)
            {
                _body.drag = _stoppingDrag;
            }
        }

        private void OnDestroy()
        {
            TickManager.OnUpdate -= TickManager_PerformUpdate;
        }

        private void Update()
        {
            if (hasAuthority)
            {
                _capturedInput.BarkPressed = _inputHandler.Player.Bark.triggered;
                _capturedInput.SprintHeld = _inputHandler.Player.Sprint.ReadValue<float>() > 0;
            }
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
            var direction = new Vector2(input.Horizontal, input.Vertical).normalized;
            if (direction.magnitude == 0)
            {
                _body.drag = _stoppingDrag;
                return;
            }
            
            _body.drag = _movingDrag;
            var maxSpeed = _normalMaxSpeed;
            var acceleration = _normalAcceleration;
            if ((ActionCodes)input.Actions == ActionCodes.Sprint)
            {
                maxSpeed = _sprintingMaxSpeed;
                acceleration = _sprintingAcceleration;
            }

            _body.AddForce(direction * acceleration, ForceMode2D.Force);

            var velocityMagnitude = _body.velocity.magnitude;
            if (velocityMagnitude > maxSpeed)
            {
                var diff = maxSpeed - velocityMagnitude;
                _body.AddForce(_body.velocity.normalized * (diff / Time.fixedDeltaTime), ForceMode2D.Force);
            }
        }

        [Command(channel = 1)]
        private void CmdSendInputs(MovementClientInput[] inputs)
        {
            if (inputs == null || inputs.Length == 0)
                return;
            
            //Don't send input to the server if you are the server.
            if (isClient && hasAuthority)
            {
                return;
            }
            
            for (var i = 0; i < inputs.Length; ++i)
            {
                if (inputs[i].TickNumber > _lastInputTickReceived)
                {
                    _receivedInputs.Add(inputs[i]);
                }
            }

            if (_receivedInputs.Count > 0)
            {
                _lastInputTickReceived = _receivedInputs[_receivedInputs.Count - 1].TickNumber;    
            }
            
            while (_receivedInputs.Count > MAX_RECEIVED_INPUTS)
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

        [TargetRpc(channel = 1)]
        private void TargetSendState(NetworkConnection connection, MovementServerState state)
        {
            //Don't process old states.
            if (_receivedState != null && _receivedState.Value.TickNumber > state.TickNumber)
            {
                return;
            }

            _receivedState = state;
        }

        [TargetRpc(channel = 1)]
        private void TargetSendTimeStepChange(NetworkConnection connection, sbyte stepChange)
        {
            TickManager.UpdateTimingStep(stepChange);
        }
        
        [Client]
        private void ClientProcessInput()
        {
            var movement = _inputHandler.Player.Move.ReadValue<Vector2>();
            var actions = ActionCodes.None;

            if (_capturedInput.BarkPressed)
            {
                actions |= ActionCodes.Bark;
                _capturedInput.BarkPressed = false;
            }

            if (_capturedInput.SprintHeld)
            {
                actions |= ActionCodes.Sprint;
            }
            
            var latestInput = new MovementClientInput()
            {
                Horizontal = movement.x,
                Vertical = movement.y,
                TickNumber = TickManager.TickNumber,
                Actions = (byte)actions
            };
            
            _clientInputs.Add(latestInput);
            ProcessInputs(latestInput);

            var amountToSend = Mathf.Min(_clientInputs.Count, 1 + MAX_RECEIVED_INPUTS);
            var inputsToSend = new MovementClientInput[amountToSend];

            for (var i = 0; i < amountToSend; ++i)
            {
                //Send the latest inputs only.
                inputsToSend[amountToSend - 1 - i] = _clientInputs[_clientInputs.Count - 1 - i];
            }

            CmdSendInputs(inputsToSend);
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
                TickManager.Physics.Simulate(Time.fixedDeltaTime);
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