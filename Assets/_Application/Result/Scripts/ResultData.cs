using UnityEngine;

namespace Uraty.Application.Reslut
{
    [CreateAssetMenu(fileName = "ResultData", menuName = "Scriptable Objects/ResultData")]
    public class ResultData : ScriptableObject
    {
        [SerializeField] private string _playerName;
        [SerializeField] public Object _role;

        public void SetData(string playerName, Object role)
        {
            _playerName = playerName;
            if (role != null)
            {
                _role = role;
            }
        }

        public string GetPlayerName()
        {
            return _playerName;
        }

        public Object GetPlayerObject()
        {
            return _role;
        }
    }
}
