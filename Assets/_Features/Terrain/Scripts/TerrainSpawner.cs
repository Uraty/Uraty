using UnityEngine;

namespace Uraty.Feature.Terrain
{
    public class TerrainSpawner : MonoBehaviour
    {
        // TerrainSpawner は、Terrain を生成するためのクラス。
        [SerializeField] private int _teamId;
        // 例えば、チームごとに異なる Terrain を生成するためのフィールド。
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
