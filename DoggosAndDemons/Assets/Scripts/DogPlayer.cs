using System;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class DogPlayer : NetworkBehaviour
{
    [SerializeField] 
    private Rigidbody2D _body;

    [SerializeField] 
    private Animator _animator;

    [SerializeField] 
    private float _speed;
    
    [SerializeField]
    private float _maxVerticalRotation = 15f;

    [SerializeField]
    private float _barkTime = 1.2f;

    [SerializeField]
    private ParticleSystem _barkParticles;

    [SerializeField, Header("Sprinting")] 
    private float _sprintMax = 10;

    [SerializeField]
    private float _sprintMultiplier = 1.5f;
    
    [SerializeField]
    private float _sprintIncrease = 1.2f;

    [SerializeField]
    private float _sprintDecrease = 1.8f;

    private DoggosAndDemons _inputs;
    private float _currentSprintAmount;
    private Vector2 _direction;
    
    private bool _holdingSprint = false;
    private bool _isBarking = false;
    private float _barkTimer = 0;

    private void OnEnable()
    {
        _inputs = new DoggosAndDemons();
        _inputs.Player.Enable();
        
        _currentSprintAmount = _sprintMax;
    }

    private void OnDisable()
    {
        _inputs.Player.Disable();
    }

    private void Start()
    {
        if (isLocalPlayer)
        {
            FindObjectOfType<DogCamera>().AssignDog(transform);
        }
    }

    private void Update()
    {
        var facingDirection = _animator.transform.localScale.x;
        if (facingDirection > 0 && _body.velocity.x < 0)
        {
            _animator.transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (facingDirection < 0 && _body.velocity.x > 0)
        {
            _animator.transform.localScale = new Vector3(1, 1, 1);
        }
        
        if (!isLocalPlayer)
        {
            return;
        }

        if (_inputs.Player.Bark.triggered && !_isBarking)
        {
            PerformBark();
        }

        _holdingSprint = _inputs.Player.Sprint.ReadValue<float>() > 0;
        _direction = _inputs.Player.Move.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        var speedToApply = _speed;
        if (_isBarking)
        {
            _barkTimer -= Time.fixedDeltaTime;
            if (_barkTimer <= 0)
            {
                _isBarking = false;
            }

            speedToApply = 0;
        }
        
        if (isLocalPlayer)
        {
            if (_holdingSprint)
            {
                if (!_isBarking)
                {
                    _currentSprintAmount -= _sprintDecrease * Time.fixedDeltaTime;
                    if (_currentSprintAmount <= 0)
                    {
                        _currentSprintAmount = 0;
                    }
                    else
                    {
                        speedToApply = _speed * _sprintMultiplier;
                    }
                }
               
            }
            else if(_currentSprintAmount < _sprintMax)
            {
                _currentSprintAmount += _sprintIncrease * Time.fixedDeltaTime;
                if (_currentSprintAmount > _sprintMax)
                {
                    _currentSprintAmount = _sprintMax;
                }
            }

            _body.velocity = _direction.normalized * speedToApply * Time.fixedDeltaTime;
        }
        
        _animator.SetFloat("Speed", _body.velocity.magnitude);
        CalculateRotation();
    }

    private void PerformBark()
    {
        _animator.SetTrigger("Bark");
        _isBarking = true;
        _barkTimer = _barkTime;
        CmdPerformBark();
    }

    private void CalculateRotation()
    {
        var rotationAmount = 0f;
        if (_body.velocity.y > 0.1f)
        {
            rotationAmount = _maxVerticalRotation;
        }
        else if (_body.velocity.y < -0.1f)
        {
            rotationAmount = -_maxVerticalRotation;
        }

        rotationAmount *= Mathf.Sign(_animator.transform.localScale.x);
        _animator.transform.rotation = Quaternion.Euler(0, 0, rotationAmount);
    }

    [Command]
    private void CmdPerformBark()
    {
        RpcPerformBark();
    }

    [ClientRpc]
    private void RpcPerformBark()
    {
        if (!isLocalPlayer)
        {
            _animator.SetTrigger("Bark");
            
            _isBarking = true;
            _barkTimer = _barkTime;
        }

        ShowBarkParticles();
    }

    private async void ShowBarkParticles()
    {
        if (_barkTime > _barkTime / 2)
        {
            await Task.Delay(TimeSpan.FromSeconds(_barkTime - (_barkTime / 2)));
        }

        if (_barkParticles != null)
        {
            _barkParticles.Play();    
        }
    }
}
