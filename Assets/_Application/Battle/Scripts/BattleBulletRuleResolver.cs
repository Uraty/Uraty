using UnityEngine;

using Uraty.Feature.Player;

namespace Uraty.Application.Battle
{
    /// <summary>
    /// Bullet が何に当たったかを解釈し、
    /// ダメージ・回復・草破壊・壁破壊を適用する Application 側の仲介クラス。
    /// </summary>
    [RequireComponent(typeof(PlayerBullet))]
    public sealed class BattleBulletRuleResolver : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private PlayerBullet _playerBullet;

        [Header("Layer")]
        [SerializeField] private LayerMask _terrainLayerMask;

        [Header("Value")]
        [SerializeField] private int _damageAmount = 1;
        [SerializeField] private int _recoveryAmount = 1;

        private void Awake()
        {
            // 未設定なら同一オブジェクト上から取得する。
            if (_playerBullet == null)
            {
                _playerBullet = GetComponent<PlayerBullet>();
            }
        }

        /// <summary>
        /// Trigger 衝突時の入口。
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            HandleHit(other);
        }

        /// <summary>
        /// Collision 衝突時の入口。
        /// Trigger ではない Collider を使う場合にも対応する。
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            HandleHit(collision.collider);
        }

        /// <summary>
        /// 衝突相手を見て、どの処理を行うべきかを振り分ける。
        /// </summary>
        private void HandleHit(Collider hitCollider)
        {
            GameObject hitObject = hitCollider.gameObject;

            // 発射者本人には反応しない。
            if (_playerBullet.IsOwner(hitObject))
            {
                return;
            }

            RoleDefinition ownerRoleDefinition = GetOwnerRoleDefinition();
            if (ownerRoleDefinition == null)
            {
                _playerBullet.RequestDestroy();
                return;
            }

            // まず Terrain 判定を行う。
            if (TryHandleTerrainHit(hitCollider, ownerRoleDefinition))
            {
                return;
            }

            // 次に Player 判定を行う。
            if (TryHandlePlayerHit(hitCollider, ownerRoleDefinition))
            {
                return;
            }

            // どちらにも当てはまらない場合は弾だけ消す。
            _playerBullet.RequestDestroy();
        }

        /// <summary>
        /// Terrain へのヒット処理。
        /// 草なら草破壊可否、壁なら壁破壊可否を RoleDefinition から読む。
        /// </summary>
        private bool TryHandleTerrainHit(
            Collider hitCollider,
            RoleDefinition ownerRoleDefinition)
        {
            GameObject hitObject = hitCollider.gameObject;
            if (!ContainsLayer(_terrainLayerMask, hitObject.layer))
            {
                return false;
            }

            BattleTerrainTarget terrainTarget =
                hitCollider.GetComponentInParent<BattleTerrainTarget>();

            // Terrain Layer にいるのに識別コンポーネントが無い場合は、
            // 例外扱いを避けるため弾だけ消す。
            if (terrainTarget == null)
            {
                _playerBullet.RequestDestroy();
                return true;
            }

            switch (terrainTarget.TerrainKind)
            {
                case BattleTerrainKind.Bush:
                    if (ownerRoleDefinition.CanBreakGrass(_playerBullet.AttackKind))
                    {
                        terrainTarget.Break();
                    }

                    _playerBullet.RequestDestroy();
                    return true;

                case BattleTerrainKind.Wall:
                    if (ownerRoleDefinition.CanBreakWalls(_playerBullet.AttackKind))
                    {
                        terrainTarget.Break();
                    }

                    _playerBullet.RequestDestroy();
                    return true;

                default:
                    _playerBullet.RequestDestroy();
                    return true;
            }
        }

        /// <summary>
        /// Player へのヒット処理。
        /// 回復弾なら味方を回復し、通常弾なら敵へダメージを与える。
        /// </summary>
        private bool TryHandlePlayerHit(
            Collider hitCollider,
            RoleDefinition ownerRoleDefinition)
        {
            IPlayerBulletTarget hitPlayerTarget =
                hitCollider.GetComponentInParent(typeof(IPlayerBulletTarget))
                as IPlayerBulletTarget;

            if (hitPlayerTarget == null)
            {
                return false;
            }

            // 念のため、発射者本人へのヒットは無視する。
            if (hitPlayerTarget.CharacterObject == _playerBullet.OwnerObject)
            {
                return true;
            }

            bool isSameTeam = hitPlayerTarget.TeamId == _playerBullet.OwnerTeamId;
            bool isRecoveryBullet = ownerRoleDefinition.IsRecovery(_playerBullet.AttackKind);

            // 回復弾なら味方だけ回復する。
            if (isRecoveryBullet)
            {
                if (isSameTeam)
                {
                    hitPlayerTarget.RecoverHp(_recoveryAmount);
                }

                _playerBullet.RequestDestroy();
                return true;
            }

            // 通常弾で味方に当たった場合は処理しない。
            if (isSameTeam)
            {
                return true;
            }

            // 通常弾で敵に当たった場合はダメージを与える。
            hitPlayerTarget.ApplyDamage(_damageAmount);
            _playerBullet.RequestDestroy();
            return true;
        }

        /// <summary>
        /// 発射者の RoleDefinition を取得する。
        /// </summary>
        private RoleDefinition GetOwnerRoleDefinition()
        {
            if (_playerBullet.OwnerObject == null)
            {
                return null;
            }

            IPlayerBulletTarget ownerTarget =
                _playerBullet.OwnerObject.GetComponent(typeof(IPlayerBulletTarget))
                as IPlayerBulletTarget;

            return ownerTarget != null ? ownerTarget.RoleDefinition : null;
        }

        /// <summary>
        /// 指定 Layer が LayerMask に含まれているかを判定する。
        /// </summary>
        private static bool ContainsLayer(LayerMask layerMask, int layer)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }
    }
}
