using UnityEngine;

namespace Uraty.Features.Result
{
    public class RotateObject : MonoBehaviour
    {
        [Header("回転速度")]
        [SerializeField] private float _rotateSpeed = 10.0f;

        private void Update()
        {
            transform.Rotate(
                0.0f,
                _rotateSpeed * Time.deltaTime,
                0.0f);
        }
    }
}
