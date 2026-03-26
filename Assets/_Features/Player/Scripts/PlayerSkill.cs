using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Player
{
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerSkill : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.0001f;

        [Header("Aim")]
        [SerializeField] private Camera _camera;

        [Header("Attack")]
        [SerializeField] private float _attackRange = 5.0f;

        [Header("Prediction Line")]
        [SerializeField] private float _lineWidth = 0.08f;
        [SerializeField] private bool _hideLineWhenNotAiming = true;

        private Vector3 _aimPoint;
        private Vector3 _targetDirection = Vector3.forward;

        private bool _isAiming = false;
        private bool _isAttack = false;
        private bool _hasValidAimPoint = false;

        private LineRenderer _lineRenderer;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();

            _lineRenderer.positionCount = 2;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;

            InitializePredictionLine();
        }

        private void Update()
        {
            // Attackはそのフレームだけ有効にする
            _isAttack = false;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || _camera == null || keyboard == null)
            {
                return;
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
#if UNITY_EDITOR
                Debug.Log("AimStart");
#endif
                _isAiming = true;
                _isAttack = false;
            }

            if (_isAiming && keyboard.qKey.isPressed)
            {
                bool hasAimPoint = TryUpdateAim();
                if (hasAimPoint)
                {
                    UpdatePredictionLine();
                }
                else
                {
                    ResetPredictionLine();
                }
            }

            if (_isAiming && keyboard.qKey.wasReleasedThisFrame)
            {
#if UNITY_EDITOR
                Debug.Log("PlayerSkill");
#endif
                _isAttack = _hasValidAimPoint;
                _isAiming = false;

                if (_hideLineWhenNotAiming || !_hasValidAimPoint)
                {
                    ResetPredictionLine();
                }
            }
        }

        private void InitializePredictionLine()
        {
            Vector3 origin = transform.position;
            _aimPoint = origin;

            _lineRenderer.SetPosition(0, origin);
            _lineRenderer.SetPosition(1, origin);
            _lineRenderer.enabled = false;
        }

        private bool TryUpdateAim()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                _hasValidAimPoint = false;
                return false;
            }

            Vector2 mouseScreenPosition = mouse.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(mouseScreenPosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0.0f, transform.position.y, 0.0f));

            if (!plane.Raycast(ray, out float distance))
            {
                _hasValidAimPoint = false;
                _aimPoint = transform.position;
                return false;
            }

            _aimPoint = ray.GetPoint(distance);

            Vector3 direction = _aimPoint - transform.position;
            direction.y = 0.0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                _hasValidAimPoint = false;
                return false;
            }

            _targetDirection = direction.normalized;
            _hasValidAimPoint = true;
            return true;
        }

        private void UpdatePredictionLine()
        {
            if (_lineRenderer == null)
            {
                return;
            }

            Vector3 origin = transform.position;
            Vector3 endPoint = GetLineEndPoint(origin);

            _lineRenderer.enabled = true;
            _lineRenderer.SetPosition(0, origin);
            _lineRenderer.SetPosition(1, endPoint);
        }

        private Vector3 GetLineEndPoint(Vector3 origin)
        {
            return origin + (_targetDirection * _attackRange);
        }

        private void ResetPredictionLine()
        {
            Vector3 origin = transform.position;

            _aimPoint = origin;
            _hasValidAimPoint = false;

            _lineRenderer.SetPosition(0, origin);
            _lineRenderer.SetPosition(1, origin);
            _lineRenderer.enabled = false;
        }
        public bool IsAiming()
        {
            return _isAiming;
        }

        public bool IsAttack()
        {
            return _isAttack;
        }

        public Vector3 GetTargetDirection()
        {
            return _targetDirection;
        }

        public float GetAttackRange()
        {
            return _attackRange;
        }
    }
}
