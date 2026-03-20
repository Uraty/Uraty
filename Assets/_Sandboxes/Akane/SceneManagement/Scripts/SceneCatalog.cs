using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uraty.Feature.SceneManagement
{

    public enum SceneId
    {
        Title,
        Lobby,
        CharacterMake,
        RoleSelect,
        Battle,
        Result
    }

    // シーンIDとシーンのパスを紐づけるクラス
    [Serializable]
    public sealed class SceneEntry
    {
        private SceneId _id;

        public SceneId Id => _id;

        [Tooltip("Build Settings に登録したシーンのフルパス")]
        public string ScenePath;
    }

    // シーンIDとシーンのパスを紐づける ScriptableObject
    // SceneFlowManager から参照される
    [CreateAssetMenu(menuName = "Game/Scene Catalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        [SerializeField] private List<SceneEntry> scenes = new List<SceneEntry>();

        public string GetPath(SceneId id)
        {
            SceneEntry entry = scenes.Find(x => x.Id == id);
            // シーンIDに対応するエントリーが見つからない、もしくはシーンパスが空白の場合は例外を投げる
            if (entry == null || string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                throw new Exception($"SceneId {id} の設定がありません。");
            }

            return entry.ScenePath;
        }
    }
}
