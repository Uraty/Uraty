using UnityEngine;

namespace Uraty.Feature.Terrain
{
    public class TerrainStatus : MonoBehaviour
    {
        // Terrain の状態を管理するクラス。
        [SerializeField] private TerrainKind _terrainKind;
        // TerrainSpawner などのコンポーネントを参照するためのフィールド。
        [SerializeField] private bool IsPlayerPassable;
        // 例えば、壁や水などの Terrain はプレイヤーが通過できないようにするためのフィールド。
        [SerializeField] private bool IsDestructible;
        // 例えば、壁などの Terrain は破壊可能にするためのフィールド。
        [SerializeField] private bool IsBulletPassable;

        public TerrainKind TerrainKind => _terrainKind;
    }
}
