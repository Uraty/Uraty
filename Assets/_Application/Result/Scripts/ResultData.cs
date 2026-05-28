using UnityEngine;

namespace Uraty.Application.Reslut
{
    [CreateAssetMenu(fileName = "ResultData", menuName = "Scriptable Objects/ResultData")]
    public class ResultData : ScriptableObject
    {
        [SerializeField] private string _playerName;
        [SerializeField] public Object _role;

        public string PlayerName => _playerName;
        public Object Role => _role;
        public void SetData(string playerName, Object role)
        {
            _playerName = playerName;
            _role = role;
        }
    }
}
