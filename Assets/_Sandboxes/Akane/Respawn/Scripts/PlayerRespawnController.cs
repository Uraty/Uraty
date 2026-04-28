using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRespawnController : MonoBehaviour
{
    [SerializeField] private Uraty.Features.Player.PlayerSpawner _playerSpawner;
    [SerializeField] private Uraty.Features.Timer.CountDown _respawnCountDown;

    private bool _isWaitingRespawn;     // リスポーン待ち状態かどうか

    private void Awake()
    {
        if (_respawnCountDown != null)
        {
            // CountDown の Start で自動開始されないように、先に止めておく
            _respawnCountDown.enabled = false;
        }
    }

    private void Update()
    {
        if (!_isWaitingRespawn)
        {
            return;
        }

        if (_respawnCountDown == null)
        {
            Debug.LogError("RespawnCountDown is not assigned");
            return;
        }

        // まだタイマー中
        if (_respawnCountDown.IsRunning)
        {
            return;
        }

        // StopTimer などで止めただけならリスポーンしない
        if (_respawnCountDown.RemainingSeconds > 0f)
        {
            return;
        }

        _isWaitingRespawn = false;
        _respawnCountDown.enabled = false;

        if (_playerSpawner == null)
        {
            Debug.LogError("PlayerSpawner is not assigned");
            return;
        }

        _playerSpawner.SpawnPlayer();
    }

    private void LateUpdate()
    {
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            Debug.Log("K key pressed - Requesting respawn");
            RequestRespawn();
        }
    }

    public void RequestRespawn()
    {
        if (_isWaitingRespawn)
        {
            return;
        }

        if (_respawnCountDown == null)
        {
            Debug.LogError("RespawnCountDown is not assigned");
            return;
        }

        _isWaitingRespawn = true;

        _respawnCountDown.enabled = true;
        _respawnCountDown.ResetTimer();
        _respawnCountDown.StartTimer();
    }

    public void CancelRespawn()
    {
        if (_respawnCountDown != null)
        {
            _respawnCountDown.StopTimer();
            _respawnCountDown.enabled = false;
        }

        _isWaitingRespawn = false;
    }
}
