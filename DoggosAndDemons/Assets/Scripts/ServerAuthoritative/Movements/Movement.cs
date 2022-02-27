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
            if (!hasAuthority && !isServer)
            {
                CancelVelocity(false);
            }

            if (hasAuthority)
            {
                ClientProcessInput();
            }
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
            
            ProcessInputs(latestInput);
            CmdSendInputs(latestInput);
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

            _receivedInput = input;
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