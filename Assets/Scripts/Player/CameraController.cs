using Unity.Cinemachine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DontLookGame.Player
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private float lookPivotHeight = 1.55f;

        [Header("Mouse Look")]
        [SerializeField] private float pitchSensitivity = 0.07f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 75f;

        [Header("Cinemachine Rig")]
        [SerializeField] private float cameraDistance = 5f;
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.45f, 0.35f, 0f);

        private Transform _lookPivot;
        private float _pitch;
        private CinemachineCamera _cinemachineCamera;

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            SetupRig();
        }

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Start()
        {
            SetupRig();
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            EnsureLookPivot();

            float mouseY = ReadMouseDelta().y;
            _pitch = Mathf.Clamp(_pitch - mouseY * pitchSensitivity, minPitch, maxPitch);
            _lookPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void SetupRig()
        {
            if (followTarget == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    followTarget = player.transform;
                }
            }

            if (followTarget == null)
            {
                return;
            }

            EnsureLookPivot();

            CinemachineBrain brain = GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = gameObject.AddComponent<CinemachineBrain>();
                brain.ShowDebugText = false;
                brain.ShowCameraFrustum = false;
            }

            if (_cinemachineCamera == null)
            {
                GameObject cameraRig = new GameObject("CM_PlayerFollowCamera");
                _cinemachineCamera = cameraRig.AddComponent<CinemachineCamera>();
            }

            _cinemachineCamera.Priority = 100;
            _cinemachineCamera.Follow = _lookPivot;
            _cinemachineCamera.LookAt = _lookPivot;

            CinemachineThirdPersonFollow follow = _cinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();
            if (follow == null)
            {
                follow = _cinemachineCamera.gameObject.AddComponent<CinemachineThirdPersonFollow>();
            }

            follow.ShoulderOffset = shoulderOffset;
            follow.VerticalArmLength = 0.3f;
            follow.CameraDistance = cameraDistance;
            follow.CameraSide = 0.5f;
            follow.Damping = new Vector3(0.1f, 0.2f, 0.1f);

            CinemachineRotationComposer composer = _cinemachineCamera.GetComponent<CinemachineRotationComposer>();
            if (composer == null)
            {
                composer = _cinemachineCamera.gameObject.AddComponent<CinemachineRotationComposer>();
            }

            composer.TargetOffset = Vector3.zero;
            composer.Damping = new Vector2(0.2f, 0.2f);

            // Keep our rig authoritative in scenes that already contain tutorial cameras.
            CinemachineCamera[] allCinemachineCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            for (int i = 0; i < allCinemachineCameras.Length; i++)
            {
                if (allCinemachineCameras[i] != _cinemachineCamera)
                {
                    allCinemachineCameras[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureLookPivot()
        {
            if (_lookPivot != null)
            {
                return;
            }

            Transform existing = followTarget.Find("CameraLookPivot");
            if (existing != null)
            {
                _lookPivot = existing;
            }
            else
            {
                GameObject pivot = new GameObject("CameraLookPivot");
                _lookPivot = pivot.transform;
                _lookPivot.SetParent(followTarget);
                _lookPivot.localPosition = new Vector3(0f, lookPivotHeight, 0f);
                _lookPivot.localRotation = Quaternion.identity;
            }

            _pitch = _lookPivot.localEulerAngles.x;
            if (_pitch > 180f)
            {
                _pitch -= 360f;
            }
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
    }
}

