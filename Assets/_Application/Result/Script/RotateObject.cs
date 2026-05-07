using UnityEngine;

namespace Uraty.Features.Result
{
    public class RotateObject : MonoBehaviour
    {
        [Header("回転速度")]
        [SerializeField] private float RotateSpeed = 10.0f;

        // Update is called once per frame
        void Update()
        {
            transform.Rotate(
                0.0f,
                RotateSpeed * Time.deltaTime,
                0.0f);
        }
    }
}
