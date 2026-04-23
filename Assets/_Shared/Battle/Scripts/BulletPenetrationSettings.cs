using System;
using UnityEngine;
using TriInspector;

namespace Uraty.Shared.Battle
{
    [Serializable]
    public sealed class BulletPenetrationSettings
    {
        [SerializeField, LabelText("プレイヤー貫通")] private bool _canPiercePlayer = false;
        [SerializeField, LabelText("壁貫通")] private bool _canPierceWall = false;
        [SerializeField, LabelText("草貫通")] private bool _canPierceBush = false;

        public bool CanPiercePlayer => _canPiercePlayer;
        public bool CanPierceWall => _canPierceWall;
        public bool CanPierceBush => _canPierceBush;

        public void Validate()
        {
        }
    }
}
