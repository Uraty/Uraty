namespace Uraty.Shared.Battle
{
    /// <summary>
    /// 弾が何かにヒットしたときの反応結果を表す。
    ///
    /// Terrain や Player など複数の受け側から返される共通レスポンスとして扱う。
    /// </summary>
    public struct BulletHitResponse
    {
        /// <summary>
        /// このヒットが受け側で正式に処理されたか。
        /// 受け先が存在しない場合などは false を返す。
        /// </summary>
        public bool WasHandled;

        /// <summary>
        /// 地形側で起こす反応。
        /// </summary>
        public TerrainHitReaction TerrainReaction;

        /// <summary>
        /// 弾側で起こす反応。
        /// </summary>
        public BulletHitReaction BulletReaction;

        /// <summary>
        /// 何に当たったかの種別。
        /// </summary>
        public BulletHitTargetKind TargetKind;

        /// <summary>
        /// 未処理を表す既定値。
        /// </summary>
        public static BulletHitResponse None => new BulletHitResponse
        {
            WasHandled = false,
            TerrainReaction = TerrainHitReaction.None,
            BulletReaction = BulletHitReaction.None,
            TargetKind = BulletHitTargetKind.None,
        };

        /// <summary>
        /// このレスポンスで弾が貫通継続できるか。
        /// </summary>
        public bool CanPassThrough => BulletReaction == BulletHitReaction.Pierce;
    }
}
