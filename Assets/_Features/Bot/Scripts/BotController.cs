using R3;

using UnityEngine;

namespace Uraty.Features.Bot
{
    /// <summary>
    /// BotInputInterpreter の入力結果を
    /// Stream として公開する。
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class BotController : MonoBehaviour
    {
        [SerializeField]
        private BotInputInterpreter _inputInterpreter;

        private readonly Subject<MoveRequest>
            _moveRequestedSubject = new();

        private readonly Subject<AimRequest>
            _aimRequestedSubject = new();

        private readonly Subject<ActionRequest>
            _attackRequestedSubject = new();

        private DisposableBag _disposables;

        public Observable<MoveRequest>
            MoveRequestedStream =>
            _moveRequestedSubject;

        public Observable<AimRequest>
            AimRequestedStream =>
            _aimRequestedSubject;

        public Observable<ActionRequest>
            AttackRequestedStream =>
            _attackRequestedSubject;

        private void Reset()
        {
            _inputInterpreter =
                GetComponent<BotInputInterpreter>();
        }

        private void Awake()
        {
            if (_inputInterpreter == null)
            {
                _inputInterpreter =
                    GetComponent<BotInputInterpreter>();
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
            PublishAttackRequest();
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
                    _inputInterpreter.AimPointWorld));
        }

        private void PublishAttackRequest()
        {
            if (_inputInterpreter
                .AttackReleasedThisFrame)
            {
                _attackRequestedSubject
                    .OnNext(new ActionRequest());
            }
        }

        private void OnDestroy()
        {
            _disposables.Dispose();

            _moveRequestedSubject.Dispose();
            _aimRequestedSubject.Dispose();
            _attackRequestedSubject.Dispose();
        }

        public readonly struct MoveRequest
        {
            public MoveRequest(
                Vector3 moveDirectionWorld)
            {
                MoveDirectionWorld =
                    moveDirectionWorld;
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
                Vector3 aimPointWorld)
            {
                AimDirectionWorld =
                    aimDirectionWorld;

                AimPointWorld =
                    aimPointWorld;
            }

            public Vector3 AimDirectionWorld
            {
                get;
            }

            public Vector3 AimPointWorld
            {
                get;
            }
        }

        public readonly struct ActionRequest
        {
        }
    }
}
