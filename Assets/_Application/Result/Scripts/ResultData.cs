using UnityEngine;

using Uraty.Features.Character;

namespace Uraty.Application.Result
{
    [CreateAssetMenu(fileName = "ResultData", menuName = "Scriptable Objects/ResultData")]
    public class ResultData : ScriptableObject
    {
        [SerializeField] private RoleType _roleType;

        public RoleType RoleType => _roleType;

        public void SetRoleType(RoleType roleType)
        {
            _roleType = roleType;
        }
    }
}
