using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AI;

using Uraty.Features.Character;

namespace Uraty.Features.Bot
{
    /// <summary>
    /// シーンに1つだけ存在するBotController
    /// </summary>
    public sealed class BotController : MonoBehaviour
    {
        private const float MinMoveSqrMagnitude = 0.001f;

        [SerializeField]
        private float _updateInterval = 0.1f;

        private readonly List<BotUnit> _bots = new();

        private float _timer;

        public void Initialize(
            GameObject playerObject,
            IReadOnlyList<GameObject> characterObjects)
        {
            _bots.Clear();

            for (int i = 0; i < characterObjects.Count; i++)
            {
                GameObject obj = characterObjects[i];

                if (obj == null)
                {
                    continue;
                }

                // プレイヤーは除外
                if (obj == playerObject)
                {
                    continue;
                }

                if (!obj.TryGetComponent(out CharacterStatus status))
                {
                    continue;
                }

                if (!obj.TryGetComponent(out CharacterMove move))
                {
                    continue;
                }

                if (!obj.TryGetComponent(out NavMeshAgent agent))
                {
                    continue;
                }

                // NavMeshAgentは経路探索専用
                agent.updatePosition = false;
                agent.updateRotation = false;

                _bots.Add(new BotUnit(
                    obj,
                    status,
                    move,
                    agent));
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (_timer < _updateInterval)
            {
                return;
            }

            _timer = 0f;

            UpdateBots();
        }

        private void UpdateBots()
        {
            foreach (BotUnit bot in _bots)
            {
                if (bot.Status.IsDead)
                {
                    continue;
                }

                GameObject target = FindNearestEnemy(bot);

                if (target == null)
                {
                    bot.Move.Move(Vector3.zero);
                    continue;
                }

                Move(bot, target);
            }
        }

        private GameObject FindNearestEnemy(BotUnit self)
        {
            GameObject nearest = null;

            float nearestSqrDistance = float.MaxValue;

            foreach (BotUnit other in _bots)
            {
                if (other == self)
                {
                    continue;
                }

                if (other.Status.TeamId == self.Status.TeamId)
                {
                    continue;
                }

                if (other.Status.IsDead)
                {
                    continue;
                }

                // bush内は索敵しない
                if (other.Status.IsInsideBush)
                {
                    continue;
                }

                Vector3 diff =
                    other.Object.transform.position
                    - self.Object.transform.position;

                diff.y = 0f;

                float sqrDistance = diff.sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = other.Object;
                }
            }

            return nearest;
        }

        private void Move(
            BotUnit bot,
            GameObject target)
        {
            bot.Agent.SetDestination(
                target.transform.position);

            if (!bot.Agent.hasPath)
            {
                bot.Move.Move(Vector3.zero);
                return;
            }

            Vector3 direction =
                bot.Agent.steeringTarget
                - bot.Object.transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude <= MinMoveSqrMagnitude)
            {
                bot.Move.Move(Vector3.zero);
                return;
            }

            direction.Normalize();

            bot.Move.Move(direction);
        }

        private sealed class BotUnit
        {
            public GameObject Object
            {
                get;
            }

            public CharacterStatus Status
            {
                get;
            }

            public CharacterMove Move
            {
                get;
            }

            public NavMeshAgent Agent
            {
                get;
            }

            public BotUnit(
                GameObject obj,
                CharacterStatus status,
                CharacterMove move,
                NavMeshAgent agent)
            {
                Object = obj;
                Status = status;
                Move = move;
                Agent = agent;
            }
        }
    }
}
