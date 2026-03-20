using UnityEngine;
using UnityEngine.InputSystem;

namespace Uraty.Feature.SceneManagement
{

    // デバッグ用のシーン切り替えクラス。キーボードの数字キーでシーンを切り替える。
    public sealed class SceneDebugInput : MonoBehaviour
    {
        [SerializeField] private SceneFlowManager flow;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || flow == null)
            {
                return;
            }

            if (kb[Key.Digit1].wasPressedThisFrame)
            {
                flow.RequestChange(SceneId.Title);
            }
            else if (kb[Key.Digit2].wasPressedThisFrame)
            {
                flow.RequestChange(SceneId.Lobby);
            }
            else if (kb[Key.Digit3].wasPressedThisFrame)
            {
                flow.RequestChange(SceneId.CharacterMake);
            }
            else if (kb[Key.Digit4].wasPressedThisFrame)
            {
                flow.RequestChange(SceneId.RoleSelect);
            }
            else if (kb[Key.Digit5].wasPressedThisFrame)
            {
                flow.RequestChange(SceneId.Battle);
            }
            else if (kb[Key.Digit6].wasPressedThisFrame)
            {
                flow.RequestChange(SceneId.Result);
            }
        }
    }
}
