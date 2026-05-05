namespace Uraty.Shared.Visibility
{
    public interface IRevealTarget
    {
        void AddRevealSource(object source);
        void RemoveRevealSource(object source);
    }
}
