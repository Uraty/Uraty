using UnityEngine;

namespace Uraty.Feature.Terrain
{
    public class TerrainSpawner : MonoBehaviour
    {
        [SerializeField] private int _teamId;
        [SerializeField] private bool _isInUse;

        public int TeamId => _teamId;
        public bool IsInUse => _isInUse;

        public void SetTeamId(int teamId)
        {
            _teamId = teamId;
        }

        public void SetIsInUse(bool isInUse)
        {
            _isInUse = isInUse;
        }
    }
}
