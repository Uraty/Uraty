using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterSuperAim : CharacterAim
    {
        private const string SuperPreviewObjectName = "SuperAimPreview";

        protected override string PreviewObjectName => SuperPreviewObjectName;

        public void BeginSuperAim()
        {
            BeginAim();
        }

        public void CompleteSuperAim()
        {
            CompleteAim();
        }

        public bool TryConsumeSuper(out Vector3 aimPoint, out Vector3 targetDirection)
        {
            return TryConsume(out aimPoint, out targetDirection);
        }

        public bool TryConsumeSuper(
            out Vector3 aimPoint,
            out Vector3 targetDirection,
            out bool canAutoAim)
        {
            return TryConsume(out aimPoint, out targetDirection, out canAutoAim);
        }
    }
}
