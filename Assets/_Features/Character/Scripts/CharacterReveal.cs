using System.Collections.Generic;

using UnityEngine;

using Uraty.Shared.Visibility;

namespace Uraty.Features.Character
{
    public sealed class CharacterReveal : MonoBehaviour
    {
        private readonly Vector2Int[] RevealOffsets =
        {
            new(-1,  2), new(0,  2), new(1,  2),

            new(-2,  1), new(-1,  1), new(0,  1), new(1,  1), new(2,  1),
            new(-2,  0), new(-1,  0), new(0,  0), new(1,  0), new(2,  0),
            new(-2, -1), new(-1, -1), new(0, -1), new(1, -1), new(2, -1),

            new(-1, -2), new(0, -2), new(1, -2),
        };

        [SerializeField, Min(0.001f)]
        private float _cellSize = 1.0f;

        [SerializeField, Range(0.1f, 1.0f)]
        private float _cellCheckScale = 0.9f;

        [SerializeField, Min(0.001f)]
        private float _cellCheckHeight = 2.0f;

        [SerializeField]
        private LayerMask _bushLayerMask;

        [SerializeField, Min(1)]
        private int _maxHitCount = 16;

        [SerializeField]
        private bool _isRevealEnabled = true;

        private Collider[] _hitColliders;

        private readonly HashSet<IRevealTarget> _currentTargets = new();
        private readonly HashSet<IRevealTarget> _detectedTargets = new();
        private readonly List<IRevealTarget> _targetsToRemove = new();

        private void Awake()
        {
            _hitColliders = new Collider[_maxHitCount];
        }

        private void Update()
        {
            if (!_isRevealEnabled)
            {
                return;
            }

            UpdateRevealTargets();
        }

        private void OnDisable()
        {
            ClearRevealTargets();
        }

        public void SetRevealEnabled(bool isRevealEnabled)
        {
            if (_isRevealEnabled == isRevealEnabled)
            {
                return;
            }

            _isRevealEnabled = isRevealEnabled;

            if (!_isRevealEnabled)
            {
                ClearRevealTargets();
            }
        }

        public bool ContainsWorldPosition(Vector3 worldPosition)
        {
            if (!_isRevealEnabled)
            {
                return false;
            }

            Vector3 centerCellPosition = GetCellCenterPosition(transform.position);
            Vector3 targetCellPosition = GetCellCenterPosition(worldPosition);

            Vector2Int targetOffset = new(
                Mathf.RoundToInt((targetCellPosition.x - centerCellPosition.x) / _cellSize),
                Mathf.RoundToInt((targetCellPosition.z - centerCellPosition.z) / _cellSize));

            for (int i = 0; i < RevealOffsets.Length; i++)
            {
                if (RevealOffsets[i] == targetOffset)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateRevealTargets()
        {
            _detectedTargets.Clear();

            Vector3 centerCellPosition = GetCellCenterPosition(transform.position);

            for (int i = 0; i < RevealOffsets.Length; i++)
            {
                Vector2Int offset = RevealOffsets[i];

                Vector3 checkCenter = centerCellPosition + new Vector3(
                    offset.x * _cellSize,
                    0.0f,
                    offset.y * _cellSize);

                DetectRevealTargetsAtCell(checkCenter);
            }

            _targetsToRemove.Clear();

            foreach (IRevealTarget currentTarget in _currentTargets)
            {
                if (!_detectedTargets.Contains(currentTarget))
                {
                    _targetsToRemove.Add(currentTarget);
                }
            }

            foreach (IRevealTarget target in _targetsToRemove)
            {
                target.RemoveRevealSource(this);
                _currentTargets.Remove(target);
            }

            foreach (IRevealTarget detectedTarget in _detectedTargets)
            {
                if (_currentTargets.Add(detectedTarget))
                {
                    detectedTarget.AddRevealSource(this);
                }
            }
        }

        private void DetectRevealTargetsAtCell(Vector3 cellCenter)
        {
            float halfCellSize = _cellSize * _cellCheckScale * 0.5f;

            Vector3 halfExtents = new(
                halfCellSize,
                _cellCheckHeight * 0.5f,
                halfCellSize);

            int hitCount = Physics.OverlapBoxNonAlloc(
                cellCenter,
                halfExtents,
                _hitColliders,
                Quaternion.identity,
                _bushLayerMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _hitColliders[i];

                if (hitCollider == null)
                {
                    continue;
                }

                IRevealTarget revealTarget = hitCollider.GetComponentInParent<IRevealTarget>();

                if (revealTarget == null)
                {
                    continue;
                }

                _detectedTargets.Add(revealTarget);
            }
        }

        private Vector3 GetCellCenterPosition(Vector3 worldPosition)
        {
            float x = Mathf.Round(worldPosition.x / _cellSize) * _cellSize;
            float z = Mathf.Round(worldPosition.z / _cellSize) * _cellSize;

            return new Vector3(x, worldPosition.y, z);
        }

        private void ClearRevealTargets()
        {
            foreach (IRevealTarget target in _currentTargets)
            {
                target.RemoveRevealSource(this);
            }

            _currentTargets.Clear();
            _detectedTargets.Clear();
            _targetsToRemove.Clear();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.identity;

            Vector3 centerCellPosition = GetCellCenterPosition(transform.position);

            for (int i = 0; i < RevealOffsets.Length; i++)
            {
                Vector2Int offset = RevealOffsets[i];

                Vector3 cellCenter = centerCellPosition + new Vector3(
                    offset.x * _cellSize,
                    0.0f,
                    offset.y * _cellSize);

                Gizmos.DrawWireCube(
                    cellCenter,
                    new Vector3(
                        _cellSize * _cellCheckScale,
                        0.05f,
                        _cellSize * _cellCheckScale));
            }
        }
#endif
    }
}
