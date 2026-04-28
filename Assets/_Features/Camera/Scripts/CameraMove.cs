using UnityEngine;

namespace Uraty.Features.Camera
{
    public class CameraMove : MonoBehaviour
    {
        [Header("追従対象")]
        [SerializeField] private GameObject targetObj;

        [Header("位置調整")]
        [SerializeField] private Vector3 Offset;


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if (targetObj != null)
            {
                // Offsetが0なら自動計算
                if (Offset == Vector3.zero)
                {
                    Offset = transform.position - targetObj.transform.position;
                }
            }
        }

        private void Update()
        {

        }

        // Update is called once per frame
        void LateUpdate()
        {
            if (targetObj == null)
                return;

            transform.position = targetObj.transform.position + Offset;
            transform.LookAt(targetObj.transform);
        }
    }
}
