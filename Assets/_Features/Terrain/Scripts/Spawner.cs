using UnityEngine;

using Uraty.Shared.Team;

namespace Uraty.Features.Terrain
{
 public class Spawner : MonoBehaviour
 {
 [SerializeField] private TeamId _teamId = TeamId.None;

 [Header("State")]
 [SerializeField]
 private bool _isUsed;

 public TeamId TeamId => _teamId;

 public bool IsUsed => _isUsed;

 public bool TryReserve()
 {
 if (_isUsed)
 {
 return false;
 }

 _isUsed = true;
 return true;
 }

 public void Release()
 {
 _isUsed = false;
 }

 public void MarkUsed()
 {
 _isUsed = true;
 }
 }
}
