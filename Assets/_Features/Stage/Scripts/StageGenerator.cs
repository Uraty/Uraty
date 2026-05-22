using UnityEngine;
using Unity.AI.Navigation;


namespace Uraty.Features.Stage
{
    public class StageGenerator : MonoBehaviour
    {
        [SerializeField]
        private NavMeshSurface navMeshSurface;

        public void GenerateStage()
        {

            // NavMesh再生成
            navMeshSurface.BuildNavMesh();
        }
    }
}
