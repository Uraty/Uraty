using UnityEngine;

namespace Uraty.Application.Reslut
{
    [CreateAssetMenu(fileName = "ResultData", menuName = "Scriptable Objects/ResultData")]
    public class ResultData : ScriptableObject
    {
        [SerializeField] private string _playerName;
        [SerializeField] private Object _roleType;

        public string PlayerName => _playerName;
        public Object Role => _roleType;
        public void SetData(string playerName, Object role)
        {
            _playerName = playerName;
            _roleType = role;
        }
    }
}
