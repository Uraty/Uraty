using System.Collections.Generic;

using UnityEngine;

using Uraty.Shared.Visibility;

namespace Uraty.Features.Character
{
    public sealed class CharacterReveal : MonoBehaviour
    {
        [SerializeField, Min(0.0f)]
        private float _revealRadius = 2.0f;

        [SerializeField]
        private LayerMask _bushLayerMask;

        [SerializeField, Min(1)]
        private int _maxHitCount = 64;

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

        private void UpdateRevealTargets()
        {
            _detectedTargets.Clear();

            Vector3 origin = transform.position;
            float revealRadiusSqr = _revealRadius * _revealRadius;

            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                _revealRadius,
                _hitColliders,
                _bushLayerMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _hitColliders[i];

                if (hitCollider == null)
                {
                    continue;
                }

                if (!IsInsideHorizontalCircle(origin, hitCollider, revealRadiusSqr))
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

        private static bool IsInsideHorizontalCircle(
            Vector3 origin,
            Collider hitCollider,
            float revealRadiusSqr)
        {
            Vector3 closestPoint = hitCollider.ClosestPoint(origin);

            float diffX = closestPoint.x - origin.x;
            float diffZ = closestPoint.z - origin.z;

            return diffX * diffX + diffZ * diffZ <= revealRadiusSqr;
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
            Gizmos.DrawWireSphere(transform.position, _revealRadius);
        }
#endif
    }
}
