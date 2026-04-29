using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uraty.Features.Scenes
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
        [SerializeField] private SceneId _id;

        public SceneId Id => _id;

        [Tooltip("Build Settings に登録したシーンのフルパス")]
        [SerializeField] private string _scenePath;

        public string ScenePath => _scenePath;
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
            return entry != null && !string.IsNullOrWhiteSpace(entry.ScenePath)
                ? entry.ScenePath
                : throw new Exception($"SceneId {id} の設定がありません。");
        }
    }
}
