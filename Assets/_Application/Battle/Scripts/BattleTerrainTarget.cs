using UnityEngine;
using Uraty.Feature.Terrain;
namespace Uraty.Application.Battle
{
    /// <summary>
    /// Terrain オブジェクトに付ける識別用コンポーネント。
    /// 草か壁かを Bullet 判定側へ伝える。
    /// </summary>
    public sealed class BattleTerrainTarget : MonoBehaviour
    {
        [SerializeField] private TerrainKind _terrainKind = TerrainKind.Bush;

        /// <summary>
        /// この Terrain が草か壁かを返す。
        /// </summary>
        public TerrainKind TerrainKind => _terrainKind;

        /// <summary>
        /// Terrain を破壊する。
        /// </summary>
        public void Break()
        {
            Destroy(gameObject);
        }
    }
}
