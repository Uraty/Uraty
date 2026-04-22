using UnityEngine;

namespace Uraty.Feature.Player
{
    /// <summary>
    /// Bulletから見た「プレイヤーとして扱える対象」の公開窓口
    /// Application側はこのインターフェースだけを見て
    /// ダメージ・回復・Team判定を行う。
    /// </summary>
    public interface IPlayerBulletTarget
    {
        /// <summary>
        /// この対象そのもののGameObject
        /// 発射本人との比較に使う
        /// </summary>
        GameObject CharacterObject
        {
            get;
        }

        /// <summary>
        /// チーム識別子
        /// 味方か敵かの判定に使用する。
        /// </summary>
        int TeamId
        {
            get;
        }

        /// <summary>
        /// このキャラクターが持つRoleDefinition
        /// 発射した玉野性質判定に利用する。
        /// </summary>
        RoleDefinition RoleDefinition
        {
            get;
        }

        /// <summary>
        /// 回復を受ける。
        /// 実際のHP処理は実装先のクラス側で行う。
        /// </summary>
        /// <param name="damageAmount"></param>
        void ApplyDamage(int damageAmount);

        /// <summary>
        /// ダメージを受ける。
        /// 実際のHP処理は実装先のクラス側で行う。
        /// </summary>
        /// <param name="recoveryAmount"></param>
        void RecoverHp(int recoveryAmount);
    }
}
