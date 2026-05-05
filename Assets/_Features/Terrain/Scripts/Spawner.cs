using UnityEngine;

using Uraty.Shared.Team;

namespace Uraty.Features.Terrain
{
    public class Spawner : MonoBehaviour
    {
        [SerializeField] private TeamId _teamId = TeamId.None;

        public TeamId TeamId => _teamId;
    }
}
