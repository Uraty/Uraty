namespace Uraty.Shared.Terrain
{
    /// <summary>
    /// Player の通行可否を返すルール。
    /// </summary>
    public interface IPlayerPassRule
    {
        bool CanPlayerPass(PlayerPassContext context);
    }
}
