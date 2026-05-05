using UnityEngine;

namespace Uraty.Systems.Camera
{
    public class CameraMove : MonoBehaviour
    {
        [Header("追従対象")]
        [SerializeField] private GameObject _target;

        [Header("位置調整")]
        [SerializeField] private Vector3 _offset;

        // Update is called once per frame
        void LateUpdate()
        {
            if (_target == null)
                return;

            transform.position = _target.transform.position + _offset;
            transform.LookAt(_target.transform);
        }

        public void SetTarget(GameObject targetObj)
        {
            _target = targetObj;
        }
    }
}
