using R3;

using UnityEngine;

namespace Uraty.Features.Player
{
    /// <summary>
    /// PlayerInputInterpreter の入力結果を Stream として公開するクラス。
    /// Character は直接参照しない。
    /// AutoAim かどうかも判断しない。
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private PlayerInputInterpreter _inputInterpreter;

        private readonly Subject<MoveRequest> _moveRequestedSubject = new();
        private readonly Subject<AimRequest> _aimRequestedSubject = new();

        private readonly Subject<ActionInputRequest> _attackInputRequestedSubject = new();
        private readonly Subject<ActionInputRequest> _superInputRequestedSubject = new();

        private readonly Subject<ActionRequest> _attackRequestedSubject = new();
        private readonly Subject<ActionRequest> _superRequestedSubject = new();

        private DisposableBag _disposables;

        public Observable<MoveRequest> MoveRequestedStream => _moveRequestedSubject;
        public Observable<AimRequest> AimRequestedStream => _aimRequestedSubject;

        public Observable<ActionInputRequest> AttackInputRequestedStream => _attackInputRequestedSubject;
        public Observable<ActionInputRequest> SuperInputRequestedStream => _superInputRequestedSubject;

        public Observable<ActionRequest> AttackRequestedStream => _attackRequestedSubject;
        public Observable<ActionRequest> SuperRequestedStream => _superRequestedSubject;

        private void Reset()
        {
            _inputInterpreter = GetComponent<PlayerInputInterpreter>();
        }

        private void Awake()
        {
            if (_inputInterpreter == null)
            {
                _inputInterpreter = GetComponent<PlayerInputInterpreter>();
            }
        }

        private void Start()
        {
            Observable.EveryUpdate()
                .Subscribe(_ => PublishRequests())
                .AddTo(ref _disposables);
        }

        private void PublishRequests()
        {
            if (_inputInterpreter == null)
            {
                return;
            }

            PublishMoveRequest();
            PublishAimRequest();
            PublishActionInputRequests();
            PublishActionRequests();
        }

        private void PublishMoveRequest()
        {
            _moveRequestedSubject.OnNext(
                new MoveRequest(
                    _inputInterpreter.MoveDirectionWorld));
        }

        private void PublishAimRequest()
        {
            _aimRequestedSubject.OnNext(
                new AimRequest(
                    _inputInterpreter.AimDirectionWorld,
                    _inputInterpreter.AimPointWorld,
                    _inputInterpreter.CurrentAimScreenPosition));
        }

        private void PublishActionInputRequests()
        {
            ActionInputRequest attackInputRequest = new(
                _inputInterpreter.AttackPressedThisFrame,
                _inputInterpreter.AttackIsPressed,
                _inputInterpreter.AttackReleasedThisFrame);

            if (attackInputRequest.HasInput)
            {
                _attackInputRequestedSubject.OnNext(attackInputRequest);
            }

            ActionInputRequest superInputRequest = new(
                _inputInterpreter.SuperPressedThisFrame,
                _inputInterpreter.SuperIsPressed,
                _inputInterpreter.SuperReleasedThisFrame);

            if (superInputRequest.HasInput)
            {
                _superInputRequestedSubject.OnNext(superInputRequest);
            }
        }

        private void PublishActionRequests()
        {
            if (_inputInterpreter.AttackReleasedThisFrame)
            {
                _attackRequestedSubject.OnNext(new ActionRequest());
            }

            if (_inputInterpreter.SuperReleasedThisFrame)
            {
                _superRequestedSubject.OnNext(new ActionRequest());
            }
        }

        private void OnDestroy()
        {
            _disposables.Dispose();

            _moveRequestedSubject.Dispose();
            _aimRequestedSubject.Dispose();

            _attackInputRequestedSubject.Dispose();
            _superInputRequestedSubject.Dispose();

            _attackRequestedSubject.Dispose();
            _superRequestedSubject.Dispose();
        }

        public readonly struct MoveRequest
        {
            public MoveRequest(Vector3 moveDirectionWorld)
            {
                MoveDirectionWorld = moveDirectionWorld;
            }

            public Vector3 MoveDirectionWorld
            {
                get;
            }
        }

        public readonly struct AimRequest
        {
            public AimRequest(
                Vector3 aimDirectionWorld,
                Vector3 aimPointWorld,
                Vector2 aimScreenPosition)
            {
                AimDirectionWorld = aimDirectionWorld;
                AimPointWorld = aimPointWorld;
                AimScreenPosition = aimScreenPosition;
            }

            public Vector3 AimDirectionWorld
            {
                get;
            }
            public Vector3 AimPointWorld
            {
                get;
            }
            public Vector2 AimScreenPosition
            {
                get;
            }
        }

        public readonly struct ActionInputRequest
        {
            public ActionInputRequest(
                bool pressedThisFrame,
                bool isPressed,
                bool releasedThisFrame)
            {
                PressedThisFrame = pressedThisFrame;
                IsPressed = isPressed;
                ReleasedThisFrame = releasedThisFrame;
            }

            public bool PressedThisFrame
            {
                get;
            }
            public bool IsPressed
            {
                get;
            }
            public bool ReleasedThisFrame
            {
                get;
            }

            public bool HasInput => PressedThisFrame || IsPressed || ReleasedThisFrame;
        }

        public readonly struct ActionRequest
        {
        }
    }
}
