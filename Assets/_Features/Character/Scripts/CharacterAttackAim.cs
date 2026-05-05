using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterAttackAim : CharacterAim
    {
        private const string AttackPreviewObjectName = "AttackAimPreview";

        protected override string PreviewObjectName => AttackPreviewObjectName;

        public void BeginAttackAim()
        {
            BeginAim();
        }

        public void CompleteAttackAim()
        {
            CompleteAim();
        }

        public bool TryConsumeAttack(out Vector3 aimPoint, out Vector3 targetDirection)
        {
            return TryConsume(out aimPoint, out targetDirection);
        }

        public bool TryConsumeAttack(
            out Vector3 aimPoint,
            out Vector3 targetDirection,
            out bool canAutoAim)
        {
            return TryConsume(out aimPoint, out targetDirection, out canAutoAim);
        }
    }
}
