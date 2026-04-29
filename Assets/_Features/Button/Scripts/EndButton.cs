using UnityEngine;

namespace Uraty.Feature.SceneManagement
{
    /// <summary>
    /// ボタン押下でゲーム（アプリ）を終了するためのコンポーネント。
    /// 
    /// - ビルド環境: Application.Quit()
    /// - Unity Editor: 再生停止
    /// </summary>
    public sealed class EndButton : MonoBehaviour
    {
        /// <summary>
        /// UI Button の OnClickから呼び出す。
        /// </summary>
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
