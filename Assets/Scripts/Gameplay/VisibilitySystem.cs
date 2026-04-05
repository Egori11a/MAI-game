using System.Collections.Generic;
using UnityEngine;

namespace DontLookGame.Gameplay
{
    public class VisibilitySystem : MonoBehaviour
    {
        [Header("Update Budget")]
        [Tooltip("Time between visibility update batches.")]
        [Min(0.01f)]
        [SerializeField] private float checkInterval = 0.05f;

        [Tooltip("How many objects to evaluate per batch.")]
        [Min(1)]
        [SerializeField] private int checksPerBatch = 4;

        private readonly List<VisibilityDependentObject> _objects = new List<VisibilityDependentObject>();
        private int _nextIndex;
        private float _timer;

        public static VisibilitySystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Register(VisibilityDependentObject visibilityObject)
        {
            if (visibilityObject == null || _objects.Contains(visibilityObject))
            {
                return;
            }

            _objects.Add(visibilityObject);
        }

        public void Unregister(VisibilityDependentObject visibilityObject)
        {
            if (visibilityObject == null)
            {
                return;
            }

            int index = _objects.IndexOf(visibilityObject);
            if (index < 0)
            {
                return;
            }

            _objects.RemoveAt(index);
            if (_nextIndex > index)
            {
                _nextIndex--;
            }

            if (_nextIndex >= _objects.Count)
            {
                _nextIndex = 0;
            }
        }

        private void Update()
        {
            if (_objects.Count == 0)
            {
                return;
            }

            Camera activeCamera = Camera.main;
            if (activeCamera == null)
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer < checkInterval)
            {
                return;
            }

            _timer = 0f;
            int checks = Mathf.Min(checksPerBatch, _objects.Count);

            for (int i = 0; i < checks; i++)
            {
                if (_nextIndex >= _objects.Count)
                {
                    _nextIndex = 0;
                }

                VisibilityDependentObject visibilityObject = _objects[_nextIndex];
                _nextIndex++;

                if (visibilityObject == null)
                {
                    continue;
                }

                visibilityObject.EvaluateVisibility(activeCamera.transform);
            }

            if (_nextIndex >= _objects.Count)
            {
                _nextIndex = 0;
            }
        }
    }
}

