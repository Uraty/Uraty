using UnityEngine;

namespace Uraty.Feature.Player
{
    /// <summary>
    /// プレイヤーが発射する弾の基礎挙動のみを担当するクラス。
    /// このクラスでは「回復弾か」「草壊せるか」などのゲームルールは持たない。
    /// それらの意味付けはApplication側で解決する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerBullet : MonoBehaviour
    {
        private const float MinLifeTimeSeconds = 0.01f;
        private const float MinMoveSpeedMetersPerSecond = 0.01f;

        [Header("Life Time")]
        [SerializeField] private float _lifeTimeSeconds = 3.0f;

        [Header("Move")]
        [SerializeField] private float _defaultMoveSpeedMetersPerSecond = 10.0f;

        private Rigidbody _rigidbody;
        private GameObject _ownerObject;
        private int _ownerTeamId;
        private PlayerBulletAttackKind _attackKind;

        /// <summary>
        /// 発射した本人のGameObject
        /// 自分自身への衝突を無視するために公開する。
        /// </summary>
        public GameObject OwnerObject => _ownerObject;
        /// <summary>
        /// 発射者のTeamId
        /// 味方・敵判定に使用する。
        /// </summary>
        public int OwnerTeamId => _ownerTeamId;
        /// <summary>
        /// この球の攻撃種別を返す
        /// </summary>
        public PlayerBulletAttackKind AttackKind => _attackKind;

        private void Awake()
        {
            // 自身に必要な参照のキャッシュだけを行う。
            _rigidbody = GetComponent<Rigidbody>();

            // 負の値や０が入っても最低限動作できる値に丸める。
            _lifeTimeSeconds = Mathf.Max(_lifeTimeSeconds, MinLifeTimeSeconds);
            _defaultMoveSpeedMetersPerSecond = Mathf.Max(
                _defaultMoveSpeedMetersPerSecond,
                MinMoveSpeedMetersPerSecond);
        }

        /// <summary>
        /// 弾生成直後に呼ぶ初期化
        /// 発射者の情報と、弾の攻撃種別、移動方向と移動速度を指定する。
        /// その後寿命で破壊するようにする。
        /// </summary>
        /// <param name="ownerObject">発射者のGameObject</param>
        /// <param name="ownerTeamId">発射者のTeamId</param>
        /// <param name="attackKind">弾の攻撃種別</param>
        /// <param name="moveDirection">弾の移動方向</param>
        /// <param name="moveSpeedMetersPerSecond">弾の移動速度</param>
        public void Initialize(
            GameObject ownerObject,
            int ownerTeamId,
            PlayerBulletAttackKind attackKind,
            Vector3 moveDirection,
            float moveSpeedMetersPerSecond)
        {
            _ownerObject = ownerObject;
            _ownerTeamId = ownerTeamId;
            _attackKind = attackKind;

            // 移動方向は正規化してから速度を掛ける。
            // 無効な方向が来た場合は、発射体の前方向を向かせる。
            Vector3 normalizedDirection = moveDirection.sqrMagnitude > Mathf.Epsilon
                ? moveDirection.normalized
                : transform.forward;

            float moveSpeed = Mathf.Max(
                moveSpeedMetersPerSecond,
                MinMoveSpeedMetersPerSecond);

            // Rigidbodyに速度を与えて弾を飛ばす。
            _rigidbody.linearVelocity = normalizedDirection * moveSpeed;

            // 一定時間経過後に弾を破棄する。
            Destroy(gameObject, _lifeTimeSeconds);
        }

        /// <summary>
        /// 速度指定を省略した簡易初期化
        /// </summary>
        /// <param name="ownerObject">発射者のGameObject</param>
        /// <param name="ownerTeamId">発射者のTeamId</param>
        /// <param name="attackKind">弾の攻撃種別</param>
        /// <param name="moveDirection">弾の移動方向</param>
        public void Initialize(
            GameObject ownerObject,
            int ownerTeamId,
            PlayerBulletAttackKind attackKind,
            Vector3 moveDirection)
        {
            Initialize(
                ownerObject,
                ownerTeamId,
                attackKind,
                moveDirection,
                _defaultMoveSpeedMetersPerSecond);
        }

        /// <summary>
        /// 外部から弾を破棄したいときの窓口
        /// </summary>
        public void RequestDestroy()
        {
            Destroy(gameObject);
        }

        /// <summary>
        /// 指定されたGameObjectが発射者本人かどうかを返す。
        /// </summary>
        /// <param name="targetObject">判定対象のGameObject</param>
        /// <returns>発射者本人であればtrue、それ以外はfalse</returns>
        public bool IsOwner(GameObject targetObject)
        {
            return targetObject != null && targetObject == _ownerObject;
        }
    }
}
