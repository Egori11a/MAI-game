using UnityEngine;

namespace DontLookGame.Gameplay
{
    [RequireComponent(typeof(VisibilityDependentObject))]
    public class MovingGhostPlatform : MonoBehaviour
    {
        [Header("Motion")]
        [SerializeField] private Vector3 localMoveOffset = new Vector3(1f, 0f, 0f);
        [SerializeField] private float oscillationSpeed = 1f;
        [SerializeField] private float phaseOffset;

        private Transform _cachedTransform;
        private Vector3 _baseLocalPosition;
        private bool _initialized;

        public void Configure(Vector3 moveOffset, float speed, float phase)
        {
            localMoveOffset = moveOffset;
            oscillationSpeed = Mathf.Max(0.01f, speed);
            phaseOffset = phase;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            _baseLocalPosition = _cachedTransform.localPosition;
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                _cachedTransform = transform;
                _baseLocalPosition = _cachedTransform.localPosition;
                _initialized = true;
            }
        }

        private void Update()
        {
            float wave = Mathf.Sin((Time.time + phaseOffset) * oscillationSpeed);
            _cachedTransform.localPosition = _baseLocalPosition + localMoveOffset * wave;
        }
    }
}

