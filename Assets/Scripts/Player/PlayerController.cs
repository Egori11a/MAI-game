using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DontLookGame.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6.5f;
        [SerializeField] private float jumpHeight = 1.95f;
        [SerializeField] private float gravity = -24f;

        [Header("Jump Assist")]
        [SerializeField] private float coyoteTime = 0.14f;
        [SerializeField] private float jumpBufferTime = 0.16f;

        [Header("Look")]
        [SerializeField] private float mouseXSensitivity = 0.16f;

        [Header("Safety")]
        [SerializeField] private float fallRespawnY = -8f;

        [Header("Audio")]
        [SerializeField] private float footstepInterval = 0.36f;
        [SerializeField] private float footstepVolume = 0.55f;
        [SerializeField] private float landingVolume = 0.72f;

        private CharacterController _characterController;
        private float _verticalVelocity;
        private Vector3 _spawnPoint;
        private bool _inputEnabled = true;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _footstepTimer;
        private bool _wasGroundedLastFrame;
        private AudioSource _audioSource;
        private AudioClip[] _footstepClips;
        private AudioClip[] _landingClips;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _spawnPoint = transform.position;

            // Stable defaults for a simple prototype character.
            _characterController.height = 1.8f;
            _characterController.radius = 0.35f;
            _characterController.center = new Vector3(0f, 0.9f, 0f);
            _characterController.stepOffset = 0.3f;
            _characterController.slopeLimit = 50f;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            _footstepClips = new[]
            {
                Resources.Load<AudioClip>("ThirdParty/KenneyImpact/Audio/footstep_grass_000"),
                Resources.Load<AudioClip>("ThirdParty/KenneyImpact/Audio/footstep_grass_001"),
                Resources.Load<AudioClip>("ThirdParty/KenneyImpact/Audio/footstep_grass_003")
            };

            _landingClips = new[]
            {
                Resources.Load<AudioClip>("ThirdParty/KenneyImpact/Audio/impactSoft_medium_000"),
                Resources.Load<AudioClip>("ThirdParty/KenneyImpact/Audio/impactSoft_medium_002"),
                Resources.Load<AudioClip>("ThirdParty/KenneyImpact/Audio/impactWood_light_000")
            };

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!_inputEnabled)
            {
                return;
            }

            RotateFromMouse();
            MoveAndJump();
            HandleRespawnIfFallen();
        }

        public void SetSpawnPoint(Vector3 spawnPoint)
        {
            _spawnPoint = spawnPoint;
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
        }

        public void TeleportTo(Vector3 worldPosition, Quaternion worldRotation)
        {
            bool controllerWasEnabled = _characterController != null && _characterController.enabled;
            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            _verticalVelocity = 0f;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _footstepTimer = 0f;
            _wasGroundedLastFrame = false;

            if (_characterController != null)
            {
                _characterController.enabled = controllerWasEnabled;
            }
        }

        private void RotateFromMouse()
        {
            float mouseX = ReadMouseDelta().x;
            transform.Rotate(Vector3.up, mouseX * mouseXSensitivity, Space.World);
        }

        private void MoveAndJump()
        {
            float deltaTime = Time.deltaTime;
            Vector2 moveInput = ReadMoveInput();
            Vector3 moveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y);
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            if (ReadJumpPressed())
            {
                _jumpBufferTimer = jumpBufferTime;
            }
            else
            {
                _jumpBufferTimer -= deltaTime;
            }

            if (_characterController.isGrounded)
            {
                _coyoteTimer = coyoteTime;
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }
            else
            {
                _coyoteTimer -= deltaTime;
            }

            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }

            _verticalVelocity += gravity * deltaTime;

            Vector3 frameVelocity = moveDirection * moveSpeed;
            frameVelocity.y = _verticalVelocity;
            _characterController.Move(frameVelocity * deltaTime);

            bool groundedNow = _characterController.isGrounded;
            if (groundedNow && !_wasGroundedLastFrame && _verticalVelocity < -6f)
            {
                PlayRandomClip(_landingClips, landingVolume);
            }

            if (groundedNow && moveInput.sqrMagnitude > 0.01f)
            {
                _footstepTimer -= deltaTime;
                if (_footstepTimer <= 0f)
                {
                    PlayRandomClip(_footstepClips, footstepVolume);
                    _footstepTimer = footstepInterval;
                }
            }
            else
            {
                _footstepTimer = 0f;
            }

            _wasGroundedLastFrame = groundedNow;
        }

        private void HandleRespawnIfFallen()
        {
            if (transform.position.y > fallRespawnY)
            {
                return;
            }

            TeleportTo(_spawnPoint, Quaternion.identity);
        }

        private void PlayRandomClip(AudioClip[] clips, float volume)
        {
            if (_audioSource == null || clips == null || clips.Length == 0)
            {
                return;
            }

            int attempts = Mathf.Min(6, clips.Length);
            for (int i = 0; i < attempts; i++)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    _audioSource.PlayOneShot(clip, volume);
                    return;
                }
            }
        }

        private static Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = 0f;
                float y = 0f;

                if (keyboard.aKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed) y += 1f;

                return new Vector2(x, y);
            }

            return Vector2.zero;
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }

        private static Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
        }

        private static bool ReadJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
#else
            return Input.GetButtonDown("Jump");
#endif
        }
    }
}

