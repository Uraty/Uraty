using UnityEngine;

namespace Uraty.Shared.Terrain
{
    /// <summary>
    /// Player の通行判定に必要な情報。
    /// </summary>
    public readonly struct PlayerPassContext
    {
        public PlayerPassContext(Transform playerTransform, Vector3 targetPosition)
        {
            PlayerTransform = playerTransform;
            TargetPosition = targetPosition;
        }

        public Transform PlayerTransform
        {
            get;
        }

        public Vector3 TargetPosition
        {
            get;
        }
    }
}
