using UnityEngine;
using UnityEngine.UI;

namespace Uraty.Application.Lobby
{
    /// <summary>
    /// キャラ選択画面内のSE再生をロビー側AudioControllerへ依頼するクラス。
    /// </summary>
    public sealed class CharacterSelectAudioController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Button[] _buttonPressedTargets;
        [SerializeField] private Button[] _roleSelectedTargets;

        private LobbyAudioController _lobbyAudioController;

        private void Awake()
        {
            _lobbyAudioController = FindFirstObjectByType<LobbyAudioController>();

            if (_lobbyAudioController == null)
            {
                Debug.LogWarning("LobbyAudioController が見つかりません。");
            }
        }

        private void OnEnable()
        {
            AddButtonListeners();
        }

        private void OnDisable()
        {
            RemoveButtonListeners();
        }

        private void AddButtonListeners()
        {
            if (_buttonPressedTargets != null)
            {
                foreach (Button button in _buttonPressedTargets)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    button.onClick.AddListener(PlayButtonPressedSe);
                }
            }

            if (_roleSelectedTargets != null)
            {
                foreach (Button button in _roleSelectedTargets)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    button.onClick.AddListener(PlayRoleSelectedSe);
                }
            }
        }

        private void RemoveButtonListeners()
        {
            if (_buttonPressedTargets != null)
            {
                foreach (Button button in _buttonPressedTargets)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    button.onClick.RemoveListener(PlayButtonPressedSe);
                }
            }

            if (_roleSelectedTargets != null)
            {
                foreach (Button button in _roleSelectedTargets)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    button.onClick.RemoveListener(PlayRoleSelectedSe);
                }
            }
        }

        private void PlayButtonPressedSe()
        {
            _lobbyAudioController?.PlayButtonPressedSe();
        }

        private void PlayRoleSelectedSe()
        {
            _lobbyAudioController?.PlayRoleSelectedSe();
        }
    }
}
