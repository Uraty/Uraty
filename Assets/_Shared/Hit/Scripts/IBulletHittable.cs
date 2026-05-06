using UnityEngine;

using Uraty.Shared.Team;

namespace Uraty.Shared.Hit
{
    public interface IBulletHittable
    {
        /// <summary>
        /// 弾が当たったときの処理を行う。
        /// </summary>
        /// <param name="owner">弾を発射したオブジェクト</param>
        /// <param name="teamId">弾を発射したチームのID</param>
        /// <param name="damage">弾のダメージ量</param>
        /// <param name="isPiercing">弾が貫通するかどうか</param>
        /// <returns>弾が壊れるかどうかを返す。</returns>
        bool ReceiveBulletHit(
            GameObject owner,
            TeamId teamId,
            float damage,
            bool isPiercing
            );
    }
}
