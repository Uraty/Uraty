using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.Player
{
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerAttack : MonoBehaviour
    {
        [Header("Aim")]
        [SerializeField] private Camera _camera;

        [Header("Prediction Line")]
        [SerializeField] private float _lineWidth = 0.08f;
        [SerializeField] private bool _hideLineWhenNotAiming = true;

        private Vector3 _releasePoint;
        private Vector3 _targetDirection = Vector3.forward;

        // 左クリックしているか
        private bool _isAiming = false;
        private bool _isAttack = false;

        private LineRenderer _lineRenderer;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();

            _lineRenderer.positionCount = 2;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;

            _lineRenderer.enabled = false;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _camera == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
#if UNITY_EDITOR
                Debug.Log("AimStart");
#endif
                _isAiming = true;
                _isAttack = false;
            }

            if (_isAiming && mouse.leftButton.isPressed)
            {
                UpdateAim();
                UpdatePredictionLine();
            }

            if (_isAiming && mouse.leftButton.wasReleasedThisFrame)
            {
#if UNITY_EDITOR
                Debug.Log("PlayerAttack");
#endif
                _isAttack = true;
                _isAiming = false;

                if (_hideLineWhenNotAiming)
                {
                    _lineRenderer.enabled = false;
                }
            }
        }

        private void UpdateAim()
        {
            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(mouseScreenPosition);

            Plane plane = new Plane(Vector3.up, new Vector3(0.0f, transform.position.y, 0.0f));

            if (plane.Raycast(ray, out float distance))
            {
                _releasePoint = ray.GetPoint(distance);

                Vector3 direction = _releasePoint - transform.position;
                direction.y = 0.0f;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    _targetDirection = direction.normalized;
                }
            }
        }

        private void UpdatePredictionLine()
        {
            if (_lineRenderer == null)
            {
                return;
            }

            Vector3 origin = transform.position;
            Vector3 endPoint = _releasePoint;
            Vector3 direction = endPoint - origin;

            if (direction.sqrMagnitude > 0.0001f)
            {
                _targetDirection = direction.normalized;
            }

            _lineRenderer.enabled = true;
            _lineRenderer.SetPosition(0, origin);
            _lineRenderer.SetPosition(1, _releasePoint);
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
    }
}
