using UnityEngine;

namespace DontLookGame.Gameplay
{
    public class VisibilityDependentObject : MonoBehaviour
    {
        [Header("Visibility Dot Thresholds")]
        [Tooltip("Object appears when dot is below this value (outside view).")]
        [Range(-1f, 1f)]
        [SerializeField] private float showThreshold = 0.38f;

        [Tooltip("Object hides when dot is above this value (inside view). Must be higher than Show Threshold.")]
        [Range(-1f, 1f)]
        [SerializeField] private float hideThreshold = 0.72f;

        [Tooltip("If true, this object starts visible until the first visibility evaluation.")]
        [SerializeField] private bool startVisible = true;

        [Header("Stability")]
        [Tooltip("How long the object may remain visible while being looked at, to make navigation playable.")]
        [Min(0f)]
        [SerializeField] private float hideDelay = 0.42f;

        [Tooltip("How long the object should stay out of view before appearing again.")]
        [Min(0f)]
        [SerializeField] private float showDelay = 0.05f;

        [Tooltip("Minimum visible time after appearing. Prevents immediate re-hide near threshold boundaries.")]
        [Min(0f)]
        [SerializeField] private float minVisibleTime = 0.28f;

        [Tooltip("Keep colliders active while hidden to avoid unfair jump failures.")]
        [SerializeField] private bool keepCollidersWhenHidden = true;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool _isVisible;
        private float _timeInsideView;
        private float _timeOutsideView;
        private float _lastEvalTime;
        private float _visibleLockTimer;

        public bool IsVisible => _isVisible;
        public float ShowThreshold => showThreshold;
        public float HideThreshold => hideThreshold;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            SetVisible(startVisible);
            _lastEvalTime = Time.time;
            _visibleLockTimer = startVisible ? minVisibleTime : 0f;
        }

        private void OnEnable()
        {
            VisibilitySystem.Instance?.Register(this);
        }

        private void Start()
        {
            VisibilitySystem.Instance?.Register(this);

            // Force one immediate evaluation to avoid incorrect initial state.
            var cam = Camera.main;
            if (cam != null)
            {
                EvaluateVisibility(cam.transform, true);
            }
        }

        private void OnDisable()
        {
            VisibilitySystem.Instance?.Unregister(this);
        }

        private void OnValidate()
        {
            if (hideThreshold <= showThreshold)
            {
                hideThreshold = Mathf.Clamp(showThreshold + 0.01f, -1f, 1f);
            }
        }

        public void EvaluateVisibility(Transform cameraTransform, bool forceApply = false)
        {
            if (cameraTransform == null)
            {
                return;
            }

            float now = Time.time;
            float deltaTime = Mathf.Max(0f, now - _lastEvalTime);
            _lastEvalTime = now;
            _visibleLockTimer = Mathf.Max(0f, _visibleLockTimer - deltaTime);

            Vector3 cameraToObject = transform.position - cameraTransform.position;
            if (cameraToObject.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 directionToObject = cameraToObject.normalized;
            Vector3 cameraForward = cameraTransform.forward.normalized;
            float dot = Vector3.Dot(cameraForward, directionToObject);

            if (forceApply)
            {
                _timeInsideView = 0f;
                _timeOutsideView = 0f;
                SetVisible(dot < hideThreshold);
                return;
            }

            if (_isVisible)
            {
                if (dot > hideThreshold)
                {
                    if (_visibleLockTimer > 0f)
                    {
                        return;
                    }

                    _timeInsideView += deltaTime;
                    _timeOutsideView = 0f;
                    if (_timeInsideView >= hideDelay)
                    {
                        SetVisible(false);
                        _timeInsideView = 0f;
                    }
                }
                else
                {
                    _timeInsideView = 0f;
                }
            }
            else
            {
                if (dot < showThreshold)
                {
                    _timeOutsideView += deltaTime;
                    _timeInsideView = 0f;
                    if (_timeOutsideView >= showDelay)
                    {
                        SetVisible(true);
                        _timeOutsideView = 0f;
                    }
                }
                else
                {
                    _timeOutsideView = 0f;
                }
            }
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;

            if (visible)
            {
                _visibleLockTimer = minVisibleTime;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = visible || keepCollidersWhenHidden;
                }
            }
        }
    }
}

