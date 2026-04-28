using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Uraty.Features.Player
{
    public class PlayerAim : MonoBehaviour
    {
        private enum AimActionType
        {
            None,
            Attack,
            Super,
        }

        private const float MinDirectionSqrMagnitude = 0.0001f;
        private const int MinArcSegments = 6;
        private const int MaxArcSegments = 64;

        [SerializeField] private PlayerStatus _playerStatus;

        [Header("Input")]
        [SerializeField] private PlayerInputInterpreter _inputInterpreter;

        [Header("Prediction Mesh")]
        [SerializeField] private Material _attackPredictionMaterial;

        [FormerlySerializedAs("_specialPredictionMaterial")]
        [SerializeField] private Material _superPredictionMaterial;

        [SerializeField] private float _groundOffset = 0.02f;
        [SerializeField] private string _previewObjectName = "AimPreview";

        [Header("Throw Prediction")]
        [SerializeField] private int _throwSampleCount = 16;

        [Header("Request")]
        [SerializeField] private bool _consumeAttackOnce = true;

        [FormerlySerializedAs("_consumeSpecialOnce")]
        [SerializeField] private bool _consumeSuperOnce = true;

        private Vector3 _aimPoint;
        private Vector3 _targetDirection = Vector3.forward;

        private Vector3 _releasedAimPoint;
        private Vector3 _releasedTargetDirection = Vector3.forward;
        private bool _releasedCanAutoAim;

        private bool _isAiming;
        private bool _hasValidAimPoint;
        private bool _hasActionRequest;

        private AimActionType _currentAimActionType = AimActionType.None;
        private AimActionType _releasedAimActionType = AimActionType.None;

        private Transform _previewTransform;
        private MeshFilter _previewMeshFilter;
        private MeshRenderer _previewMeshRenderer;
        private Mesh _predictionMesh;

        private readonly List<Vector3> _vertices = new List<Vector3>(256);
        private readonly List<int> _triangles = new List<int>(768);
        private readonly List<Vector3> _normals = new List<Vector3>(256);

        private void Awake()
        {
            if (_inputInterpreter == null)
            {
                _inputInterpreter = GetComponent<PlayerInputInterpreter>();
            }

            EnsurePreviewObject();

            _predictionMesh = new Mesh
            {
                name = $"{nameof(PlayerAim)}_{nameof(_predictionMesh)}"
            };
            _predictionMesh.MarkDynamic();

            _previewMeshFilter.sharedMesh = _predictionMesh;

            Material defaultMaterial = _attackPredictionMaterial != null
                ? _attackPredictionMaterial
                : _superPredictionMaterial;

            if (defaultMaterial != null)
            {
                _previewMeshRenderer.sharedMaterial = defaultMaterial;
            }

            InitializePredictionMesh();
        }

        private void OnDestroy()
        {
            if (_predictionMesh != null)
            {
                Destroy(_predictionMesh);
                _predictionMesh = null;
            }
        }

        private void Update()
        {
            if (_inputInterpreter == null || !HasAnyDefinition())
            {
                if (_isAiming)
                {
                    _isAiming = false;
                    _currentAimActionType = AimActionType.None;
                    ResetPredictionMesh();
                }

                return;
            }

            if (!_isAiming)
            {
                if (_inputInterpreter.AttackPressedThisFrame && HasDefinition(AimActionType.Attack))
                {
                    BeginAim(AimActionType.Attack);
                }
                else if (_inputInterpreter.SuperPressedThisFrame && HasDefinition(AimActionType.Super))
                {
                    BeginAim(AimActionType.Super);
                }
            }

            if (_isAiming && IsCurrentAimButtonPressed())
            {
                if (TryUpdateAim())
                {
                    UpdatePredictionMesh();
                }
                else
                {
                    ResetPredictionMesh();
                }
            }

            if (_isAiming && WasCurrentAimButtonReleased())
            {
                if (_inputInterpreter.HasValidAimPointWorld)
                {
                    TryUpdateAim();
                }

                CompleteAimRelease();

                _isAiming = false;
                _currentAimActionType = AimActionType.None;
                HidePredictionMeshOnly();
            }
        }

        private void BeginAim(AimActionType actionType)
        {
            _isAiming = true;
            _hasValidAimPoint = false;
            _currentAimActionType = actionType;
            ApplyPreviewMaterial(actionType);
        }

        private bool IsCurrentAimButtonPressed()
        {
            if (_inputInterpreter == null)
            {
                return false;
            }

            switch (_currentAimActionType)
            {
                case AimActionType.Attack:
                    return _inputInterpreter.AttackIsPressed;

                case AimActionType.Super:
                    return _inputInterpreter.SuperIsPressed;

                default:
                    return false;
            }
        }

        private bool WasCurrentAimButtonReleased()
        {
            if (_inputInterpreter == null)
            {
                return false;
            }

            switch (_currentAimActionType)
            {
                case AimActionType.Attack:
                    return _inputInterpreter.AttackReleasedThisFrame;

                case AimActionType.Super:
                    return _inputInterpreter.SuperReleasedThisFrame;

                default:
                    return false;
            }
        }

        private void CompleteAimRelease()
        {
            if (!HasDefinition(_currentAimActionType))
            {
                ClearReleasedRequest();
                return;
            }

            if (!TryConsumeCurrentReleaseInfo(out PlayerInputInterpreter.ReleaseInfo releaseInfo))
            {
                ClearReleasedRequest();
                return;
            }

            if (_hasValidAimPoint)
            {
                _releasedAimPoint = _aimPoint;
                _releasedTargetDirection = _targetDirection;
            }
            else
            {
                if (!TryResolveReleaseDirection(releaseInfo, out Vector3 resolvedDirection))
                {
                    ClearReleasedRequest();
                    return;
                }

                _releasedTargetDirection = resolvedDirection;
                _releasedAimPoint = GetFlatOrigin() + (resolvedDirection * GetAttackRange());
            }

            _releasedCanAutoAim = releaseInfo.CanAutoAim;
            _releasedAimActionType = _currentAimActionType;
            _hasActionRequest = true;
        }

        private bool TryConsumeCurrentReleaseInfo(out PlayerInputInterpreter.ReleaseInfo releaseInfo)
        {
            releaseInfo = default;

            if (_inputInterpreter == null)
            {
                return false;
            }

            switch (_currentAimActionType)
            {
                case AimActionType.Attack:
                    return _inputInterpreter.TryConsumeAttackRelease(out releaseInfo);

                case AimActionType.Super:
                    return _inputInterpreter.TryConsumeSuperRelease(out releaseInfo);

                default:
                    return false;
            }
        }

        private bool TryResolveReleaseDirection(
            PlayerInputInterpreter.ReleaseInfo releaseInfo,
            out Vector3 direction)
        {
            direction = releaseInfo.AimDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = _targetDirection;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = Vector3.forward;
            }

            direction.Normalize();
            return true;
        }

        private void ApplyPreviewMaterial(AimActionType actionType)
        {
            if (_previewMeshRenderer == null)
            {
                return;
            }

            Material material = actionType == AimActionType.Super
                ? _superPredictionMaterial
                : _attackPredictionMaterial;

            if (material == null)
            {
                material = _attackPredictionMaterial != null
                    ? _attackPredictionMaterial
                    : _superPredictionMaterial;
            }

            if (material != null)
            {
                _previewMeshRenderer.sharedMaterial = material;
            }
        }

        private void EnsurePreviewObject()
        {
            Transform existingChild = transform.Find(_previewObjectName);
            if (existingChild != null)
            {
                _previewTransform = existingChild;
            }
            else
            {
                GameObject previewObject = new GameObject(_previewObjectName);
                _previewTransform = previewObject.transform;
                _previewTransform.SetParent(transform, false);
            }

            _previewTransform.SetParent(transform, false);
            _previewTransform.localPosition = Vector3.zero;
            _previewTransform.localRotation = Quaternion.identity;
            _previewTransform.localScale = Vector3.one;

            _previewMeshFilter = _previewTransform.GetComponent<MeshFilter>();
            if (_previewMeshFilter == null)
            {
                _previewMeshFilter = _previewTransform.gameObject.AddComponent<MeshFilter>();
            }

            _previewMeshRenderer = _previewTransform.GetComponent<MeshRenderer>();
            if (_previewMeshRenderer == null)
            {
                _previewMeshRenderer = _previewTransform.gameObject.AddComponent<MeshRenderer>();
            }

            _previewMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _previewMeshRenderer.receiveShadows = false;
            _previewMeshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _previewMeshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _previewMeshRenderer.enabled = false;
        }

        private bool HasAnyDefinition()
        {
            return GetDefinition(AimActionType.Attack) != null || GetDefinition(AimActionType.Super) != null;
        }

        private bool HasDefinition(AimActionType actionType)
        {
            return GetDefinition(actionType) != null;
        }

        private AttackDefinition GetDefinition(AimActionType actionType)
        {
            if (_playerStatus == null || _playerStatus.RoleDefinition == null)
            {
                return null;
            }

            switch (actionType)
            {
                case AimActionType.Attack:
                    return _playerStatus.RoleDefinition.Attack;

                case AimActionType.Super:
                    return _playerStatus.RoleDefinition.Super;

                default:
                    return null;
            }
        }

        private AttackDefinition GetActiveDefinition()
        {
            if (_isAiming)
            {
                AttackDefinition currentDefinition = GetDefinition(_currentAimActionType);
                if (currentDefinition != null)
                {
                    return currentDefinition;
                }
            }

            if (_hasActionRequest)
            {
                AttackDefinition releasedDefinition = GetDefinition(_releasedAimActionType);
                if (releasedDefinition != null)
                {
                    return releasedDefinition;
                }
            }

            AttackDefinition attackDefinition = GetDefinition(AimActionType.Attack);
            if (attackDefinition != null)
            {
                return attackDefinition;
            }

            return GetDefinition(AimActionType.Super);
        }

        private void InitializePredictionMesh()
        {
            _aimPoint = GetFlatOrigin();
            ClearMesh();

            if (_previewMeshRenderer != null)
            {
                _previewMeshRenderer.enabled = false;
            }
        }

        private bool TryUpdateAim()
        {
            if (_inputInterpreter == null || !_inputInterpreter.HasValidAimPointWorld)
            {
                _hasValidAimPoint = false;
                return false;
            }

            _aimPoint = _inputInterpreter.AimPointWorld;

            Vector3 direction = _inputInterpreter.AimDirectionWorld;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                _hasValidAimPoint = false;
                return false;
            }

            _targetDirection = direction.normalized;
            _hasValidAimPoint = true;
            return true;
        }

        private void UpdatePredictionMesh()
        {
            if (_predictionMesh == null)
            {
                return;
            }

            AttackDefinition definition = GetDefinition(_currentAimActionType);
            if (definition == null)
            {
                return;
            }

            _vertices.Clear();
            _triangles.Clear();
            _normals.Clear();

            switch (definition.Type)
            {
                case AimType.Line:
                    BuildLinePreview(definition.Line);
                    break;

                case AimType.Fan:
                    BuildFanPreview(definition.Fan);
                    break;

                case AimType.Throw:
                    BuildThrowPreview(definition.Throw);
                    break;
            }

            ApplyMesh();
        }

        private void BuildLinePreview(LineAttackDefinition definition)
        {
            if (definition == null || definition.AimLines == null)
            {
                return;
            }

            for (int i = 0; i < definition.AimLines.Length; i++)
            {
                LineAimLineDefinition aimLine = definition.AimLines[i];
                if (aimLine == null)
                {
                    continue;
                }

                float visibleLength = Mathf.Max(0f, aimLine.Range);
                if (visibleLength <= 0f || aimLine.Width <= 0f)
                {
                    continue;
                }

                float effectiveRange = Mathf.Max(0f, aimLine.EffectiveRange);
                float startDistance = Mathf.Max(0f, effectiveRange - visibleLength);
                float angleOffset = aimLine.OffsetAngleFromAimLine;
                float lateralOffset = aimLine.OffsetDistanceFromAimLine;

                Vector3 forward = Quaternion.Euler(0f, angleOffset, 0f) * _targetDirection;
                forward.y = 0f;

                if (forward.sqrMagnitude <= MinDirectionSqrMagnitude)
                {
                    forward = _targetDirection;
                }

                forward.Normalize();

                Vector3 origin = GetRenderOrigin() + (forward * startDistance);

                AddLineArea(origin, forward, visibleLength, aimLine.Width, lateralOffset);
            }
        }

        private void BuildFanPreview(FanAttackDefinition definition)
        {
            if (definition == null || definition.AimLines == null)
            {
                return;
            }

            for (int i = 0; i < definition.AimLines.Length; i++)
            {
                FanAimLineDefinition aimLine = definition.AimLines[i];
                if (aimLine == null)
                {
                    continue;
                }

                float visibleLength = Mathf.Max(0f, aimLine.Range);
                float effectiveRange = Mathf.Max(0f, aimLine.EffectiveRange);
                float angle = Mathf.Max(0f, aimLine.Angle);

                if (visibleLength <= 0f || angle <= 0f)
                {
                    continue;
                }

                float innerRadius = Mathf.Max(0f, effectiveRange - visibleLength);
                float outerRadius = effectiveRange;
                if (outerRadius <= 0f || innerRadius >= outerRadius)
                {
                    continue;
                }

                Vector3 forward = Quaternion.Euler(0f, aimLine.OffsetAngleFromAimLine, 0f) * _targetDirection;
                forward.y = 0f;

                if (forward.sqrMagnitude <= MinDirectionSqrMagnitude)
                {
                    forward = _targetDirection;
                }

                forward.Normalize();

                AddFanArea(GetRenderOrigin(), forward, innerRadius, outerRadius, angle);
            }
        }

        private void BuildThrowPreview(ThrowAttackDefinition definition)
        {
            if (definition == null || definition.AimLines == null)
            {
                return;
            }

            Vector3 flatOrigin = GetFlatOrigin();
            Vector3 renderOrigin = GetRenderOrigin();

            for (int i = 0; i < definition.AimLines.Length; i++)
            {
                ThrowAimLineDefinition aimLine = definition.AimLines[i];
                if (aimLine == null)
                {
                    continue;
                }

                if (!TryGetThrowCircleInfo(
                        definition,
                        aimLine,
                        out _,
                        out Vector3 circleCenterPoint,
                        out _,
                        out _))
                {
                    continue;
                }

                Vector3 renderCircleCenterPoint = circleCenterPoint + (Vector3.up * _groundOffset);
                AddCircleArea(renderCircleCenterPoint, Mathf.Max(0f, aimLine.CircleRadius));
            }

            if (definition.Bullets == null || definition.Bullets.Length == 0)
            {
                return;
            }

            for (int i = 0; i < definition.Bullets.Length; i++)
            {
                ThrowBulletDefinition bullet = definition.Bullets[i];
                if (bullet == null)
                {
                    continue;
                }

                ThrowAimLineDefinition aimLine = GetAssignedAimLine(definition.AimLines, i);
                if (aimLine == null)
                {
                    continue;
                }

                if (!TryGetThrowCircleInfo(
                        definition,
                        aimLine,
                        out Vector3 aimLineDirection,
                        out Vector3 circleCenterPoint,
                        out float visibleLength,
                        out float distanceRatio))
                {
                    continue;
                }

                Vector3 offsetDirection = Quaternion.Euler(0f, bullet.OffsetAngleFromAimLine, 0f) * aimLineDirection;
                offsetDirection.y = 0f;

                if (offsetDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
                {
                    offsetDirection = aimLineDirection;
                }

                offsetDirection.Normalize();

                Vector3 landingPoint = circleCenterPoint + (offsetDirection * Mathf.Max(0f, bullet.OffsetDistanceFromAimLine));
                landingPoint.y = flatOrigin.y;

                float totalDistanceToCircleCenter = GetPlanarDistance(flatOrigin, circleCenterPoint);
                float startDistance = Mathf.Max(0f, totalDistanceToCircleCenter - visibleLength);

                float parabolaHeight = GetThrowPreviewParabolaHeight(definition, aimLine, distanceRatio);
                float parabolaWidth = Mathf.Max(0f, aimLine.ParabolaWidth);

                Vector3 renderCircleCenterPoint = circleCenterPoint + (Vector3.up * _groundOffset);
                Vector3 renderLandingPoint = landingPoint + (Vector3.up * _groundOffset);

                AddThrowArc(
                    renderOrigin,
                    renderCircleCenterPoint,
                    renderLandingPoint,
                    parabolaHeight,
                    parabolaWidth,
                    startDistance);
            }
        }

        private bool TryGetThrowCircleInfo(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            out Vector3 aimLineDirection,
            out Vector3 circleCenterPoint,
            out float visibleLength,
            out float distanceRatio)
        {
            aimLineDirection = _targetDirection;
            circleCenterPoint = GetFlatOrigin();
            visibleLength = 0f;
            distanceRatio = 0f;

            if (definition == null || aimLine == null)
            {
                return false;
            }

            GetThrowPreviewDistanceInfo(definition, aimLine, out float endDistance, out visibleLength, out distanceRatio);

            if (endDistance <= 0f)
            {
                return false;
            }

            aimLineDirection = Quaternion.Euler(0f, aimLine.OffsetAngleFromAimLine, 0f) * _targetDirection;
            aimLineDirection.y = 0f;

            if (aimLineDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                aimLineDirection = _targetDirection;
            }

            if (aimLineDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                aimLineDirection = Vector3.forward;
            }

            aimLineDirection.Normalize();

            float circleCenterDistance = Mathf.Max(0f, endDistance + aimLine.OffsetDistanceFromAimLine);
            circleCenterPoint = GetFlatOrigin() + (aimLineDirection * circleCenterDistance);
            circleCenterPoint.y = GetFlatOrigin().y;

            return true;
        }

        private void GetThrowPreviewDistanceInfo(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            out float endDistance,
            out float visibleLength,
            out float distanceRatio)
        {
            float maxEffectiveRange = Mathf.Max(0f, aimLine.EffectiveRange);
            float maxRange = Mathf.Max(aimLine.MinRange, aimLine.MaxRange);

            if (!definition.IsVariableRange)
            {
                endDistance = maxEffectiveRange;
                visibleLength = maxRange;
                distanceRatio = endDistance > 0f ? 1f : 0f;
                return;
            }

            if (maxEffectiveRange <= 0f)
            {
                endDistance = 0f;
                visibleLength = 0f;
                distanceRatio = 0f;
                return;
            }

            float aimDistance = Mathf.Min(GetPlanarDistance(GetFlatOrigin(), _aimPoint), maxEffectiveRange);
            distanceRatio = Mathf.Clamp01(aimDistance / maxEffectiveRange);

            endDistance = aimDistance;
            visibleLength = Mathf.Min(aimDistance, maxRange * distanceRatio);
        }

        private float GetThrowPreviewParabolaHeight(
            ThrowAttackDefinition definition,
            ThrowAimLineDefinition aimLine,
            float distanceRatio)
        {
            if (!definition.IsVariableLandingTime)
            {
                return Mathf.Max(0f, aimLine.FixedParabolaHeight);
            }

            float minHeight = Mathf.Min(aimLine.MinParabolaHeight, aimLine.MaxParabolaHeight);
            float maxHeight = Mathf.Max(aimLine.MinParabolaHeight, aimLine.MaxParabolaHeight);
            return Mathf.Lerp(minHeight, maxHeight, Mathf.Clamp01(distanceRatio));
        }

        private float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void AddThrowArc(
            Vector3 start,
            Vector3 circleCenterPoint,
            Vector3 landingPoint,
            float height,
            float width,
            float startDistance)
        {
            if (width <= 0f)
            {
                return;
            }

            float totalDistance = GetPlanarDistance(start, circleCenterPoint);
            if (totalDistance <= 0.0001f)
            {
                return;
            }

            float clampedStartDistance = Mathf.Clamp(startDistance, 0f, totalDistance);
            float startT = clampedStartDistance / totalDistance;

            Vector3 offsetFromCircleCenter = landingPoint - circleCenterPoint;
            Vector3 previousPoint = EvaluateThrowPosition(start, circleCenterPoint, offsetFromCircleCenter, height, startT);

            int sampleCount = Mathf.Max(1, _throwSampleCount);

            for (int i = 1; i <= sampleCount; i++)
            {
                float segmentT = i / (float)sampleCount;
                float t = Mathf.Lerp(startT, 1f, segmentT);

                Vector3 currentPoint = EvaluateThrowPosition(start, circleCenterPoint, offsetFromCircleCenter, height, t);
                AddRibbonSegment(previousPoint, currentPoint, width);

                previousPoint = currentPoint;
            }
        }

        private Vector3 EvaluateThrowPosition(
            Vector3 start,
            Vector3 circleCenterPoint,
            Vector3 offsetFromCircleCenter,
            float height,
            float t)
        {
            Vector3 point = EvaluateParabolaPosition(start, circleCenterPoint, height, t);
            point += offsetFromCircleCenter * t;
            return point;
        }

        private Vector3 EvaluateParabolaPosition(Vector3 start, Vector3 end, float height, float t)
        {
            Vector3 point = Vector3.Lerp(start, end, t);
            point.y += 4f * height * t * (1f - t);
            return point;
        }

        private void AddRibbonSegment(Vector3 start, Vector3 end, float width)
        {
            Vector3 direction = end - start;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = _targetDirection;
            }

            Vector3 right = Vector3.Cross(Vector3.up, direction.normalized);
            if (right.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                return;
            }

            Vector3 halfWidth = right.normalized * (width * 0.5f);

            Vector3 v0 = start - halfWidth;
            Vector3 v1 = start + halfWidth;
            Vector3 v2 = end + halfWidth;
            Vector3 v3 = end - halfWidth;

            AddQuad(v0, v1, v2, v3);
        }

        private void AddLineArea(Vector3 origin, Vector3 forward, float length, float width, float lateralOffset)
        {
            if (length <= 0f || width <= 0f)
            {
                return;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 offset = right * lateralOffset;
            Vector3 halfWidth = right * (width * 0.5f);
            Vector3 start = origin + offset;
            Vector3 end = start + (forward * length);

            Vector3 v0 = start - halfWidth;
            Vector3 v1 = start + halfWidth;
            Vector3 v2 = end + halfWidth;
            Vector3 v3 = end - halfWidth;

            AddQuad(v0, v1, v2, v3);
        }

        private void AddFanArea(Vector3 center, Vector3 forward, float innerRadius, float outerRadius, float angleDeg)
        {
            if (outerRadius <= 0f || angleDeg <= 0f)
            {
                return;
            }

            innerRadius = Mathf.Max(0f, innerRadius);
            if (innerRadius >= outerRadius)
            {
                return;
            }

            int segmentCount = GetArcSegmentCount(outerRadius, angleDeg);
            float halfAngle = angleDeg * 0.5f;

            int previousInnerIndex = -1;
            int previousOuterIndex = -1;

            for (int i = 0; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;

                int currentInnerIndex = AddVertex(center + (direction * innerRadius));
                int currentOuterIndex = AddVertex(center + (direction * outerRadius));

                if (previousInnerIndex >= 0)
                {
                    AddTriangleDoubleSided(previousInnerIndex, previousOuterIndex, currentOuterIndex);
                    AddTriangleDoubleSided(previousInnerIndex, currentOuterIndex, currentInnerIndex);
                }

                previousInnerIndex = currentInnerIndex;
                previousOuterIndex = currentOuterIndex;
            }
        }

        private void AddCircleArea(Vector3 center, float radius)
        {
            if (radius <= 0f)
            {
                return;
            }

            int segmentCount = GetArcSegmentCount(radius, 360f);
            int centerIndex = AddVertex(center);

            int firstIndex = -1;
            int previousIndex = -1;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = (360f * i) / segmentCount;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                int currentIndex = AddVertex(center + (direction * radius));

                if (firstIndex < 0)
                {
                    firstIndex = currentIndex;
                }

                if (previousIndex >= 0)
                {
                    AddTriangleDoubleSided(centerIndex, previousIndex, currentIndex);
                }

                previousIndex = currentIndex;
            }

            if (firstIndex >= 0 && previousIndex >= 0)
            {
                AddTriangleDoubleSided(centerIndex, previousIndex, firstIndex);
            }
        }

        private int GetArcSegmentCount(float radius, float angleDeg)
        {
            float density = (angleDeg / 12f) + (radius * 1.5f);
            return Mathf.Clamp(Mathf.CeilToInt(density), MinArcSegments, MaxArcSegments);
        }

        private void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int startIndex = _vertices.Count;

            AddVertex(v0);
            AddVertex(v1);
            AddVertex(v2);
            AddVertex(v3);

            AddTriangleDoubleSided(startIndex + 0, startIndex + 1, startIndex + 2);
            AddTriangleDoubleSided(startIndex + 0, startIndex + 2, startIndex + 3);
        }

        private int AddVertex(Vector3 worldVertex)
        {
            Vector3 localVertex = _previewTransform != null
                ? _previewTransform.InverseTransformPoint(worldVertex)
                : worldVertex;

            _vertices.Add(localVertex);
            _normals.Add(Vector3.up);
            return _vertices.Count - 1;
        }

        private void AddTriangleDoubleSided(int a, int b, int c)
        {
            _triangles.Add(a);
            _triangles.Add(b);
            _triangles.Add(c);

            _triangles.Add(a);
            _triangles.Add(c);
            _triangles.Add(b);
        }

        private void ApplyMesh()
        {
            ClearMesh();

            if (_vertices.Count == 0)
            {
                if (_previewMeshRenderer != null)
                {
                    _previewMeshRenderer.enabled = false;
                }

                return;
            }

            _predictionMesh.SetVertices(_vertices);
            _predictionMesh.SetTriangles(_triangles, 0);
            _predictionMesh.SetNormals(_normals);
            _predictionMesh.RecalculateBounds();

            if (_previewMeshRenderer != null)
            {
                ApplyPreviewMaterial(_currentAimActionType);
                _previewMeshRenderer.enabled = true;
            }
        }

        private void ClearMesh()
        {
            if (_predictionMesh != null)
            {
                _predictionMesh.Clear();
            }
        }

        private void HidePredictionMeshOnly()
        {
            ClearMesh();

            if (_previewMeshRenderer != null)
            {
                _previewMeshRenderer.enabled = false;
            }
        }

        private void ResetPredictionMesh()
        {
            _hasValidAimPoint = false;
            HidePredictionMeshOnly();
        }

        private void ClearReleasedRequest()
        {
            _hasActionRequest = false;
            _releasedCanAutoAim = false;
            _releasedAimActionType = AimActionType.None;
        }

        private Vector3 GetFlatOrigin()
        {
            return transform.position;
        }

        private Vector3 GetRenderOrigin()
        {
            return transform.position + (Vector3.up * _groundOffset);
        }

        public bool TryConsumeAttack(out Vector3 aimPoint, out Vector3 targetDirection)
        {
            return TryConsumeRequest(
                AimActionType.Attack,
                _consumeAttackOnce,
                out aimPoint,
                out targetDirection,
                out _);
        }

        public bool TryConsumeAttack(
            out Vector3 aimPoint,
            out Vector3 targetDirection,
            out bool canAutoAim)
        {
            return TryConsumeRequest(
                AimActionType.Attack,
                _consumeAttackOnce,
                out aimPoint,
                out targetDirection,
                out canAutoAim);
        }

        public bool TryConsumeSuper(out Vector3 aimPoint, out Vector3 targetDirection)
        {
            return TryConsumeRequest(
                AimActionType.Super,
                _consumeSuperOnce,
                out aimPoint,
                out targetDirection,
                out _);
        }

        public bool TryConsumeSuper(
            out Vector3 aimPoint,
            out Vector3 targetDirection,
            out bool canAutoAim)
        {
            return TryConsumeRequest(
                AimActionType.Super,
                _consumeSuperOnce,
                out aimPoint,
                out targetDirection,
                out canAutoAim);
        }

        private bool TryConsumeRequest(
            AimActionType actionType,
            bool consumeOnce,
            out Vector3 aimPoint,
            out Vector3 targetDirection,
            out bool canAutoAim)
        {
            aimPoint = _releasedAimPoint;
            targetDirection = _releasedTargetDirection;
            canAutoAim = _releasedCanAutoAim;

            if (!_hasActionRequest || _releasedAimActionType != actionType)
            {
                return false;
            }

            if (consumeOnce)
            {
                ClearReleasedRequest();
            }

            return true;
        }

        public bool IsAiming()
        {
            return _isAiming;
        }

        public bool IsAttack()
        {
            return _isAiming
                ? _currentAimActionType == AimActionType.Attack
                : _hasActionRequest && _releasedAimActionType == AimActionType.Attack;
        }

        public bool IsSuper()
        {
            return _isAiming
                ? _currentAimActionType == AimActionType.Super
                : _hasActionRequest && _releasedAimActionType == AimActionType.Super;
        }

        public bool CanAutoAim()
        {
            return _hasActionRequest && _releasedCanAutoAim;
        }

        public Vector3 GetTargetDirection()
        {
            if (_isAiming && _hasValidAimPoint)
            {
                return _targetDirection;
            }

            if (_hasActionRequest)
            {
                return _releasedTargetDirection;
            }

            return _targetDirection.sqrMagnitude > MinDirectionSqrMagnitude
                ? _targetDirection.normalized
                : transform.forward;
        }

        public Vector3 GetTargetPoint()
        {
            if (_isAiming && _hasValidAimPoint)
            {
                return _aimPoint;
            }

            if (_hasActionRequest)
            {
                return _releasedAimPoint;
            }

            return GetFlatOrigin() + (GetTargetDirection() * GetAttackRange());
        }

        public float GetAttackRange()
        {
            AttackDefinition definition = GetActiveDefinition();
            if (definition == null)
            {
                return 0f;
            }

            switch (definition.Type)
            {
                case AimType.Line:
                    return GetMaxLineEffectiveRange(definition.Line);

                case AimType.Fan:
                    return GetMaxFanEffectiveRange(definition.Fan);

                case AimType.Throw:
                    return GetMaxThrowDistance(definition.Throw);

                default:
                    return 0f;
            }
        }

        private float GetMaxLineEffectiveRange(LineAttackDefinition definition)
        {
            if (definition == null || definition.AimLines == null)
            {
                return 0f;
            }

            float max = 0f;
            for (int i = 0; i < definition.AimLines.Length; i++)
            {
                if (definition.AimLines[i] == null)
                {
                    continue;
                }

                max = Mathf.Max(max, definition.AimLines[i].EffectiveRange);
            }

            return max;
        }

        private float GetMaxFanEffectiveRange(FanAttackDefinition definition)
        {
            if (definition == null || definition.AimLines == null)
            {
                return 0f;
            }

            float max = 0f;
            for (int i = 0; i < definition.AimLines.Length; i++)
            {
                if (definition.AimLines[i] == null)
                {
                    continue;
                }

                max = Mathf.Max(max, definition.AimLines[i].EffectiveRange);
            }

            return max;
        }

        private float GetMaxThrowDistance(ThrowAttackDefinition definition)
        {
            if (definition == null || definition.AimLines == null)
            {
                return 0f;
            }

            float max = 0f;

            for (int i = 0; i < definition.AimLines.Length; i++)
            {
                ThrowAimLineDefinition aimLine = definition.AimLines[i];
                if (aimLine == null)
                {
                    continue;
                }

                GetThrowPreviewDistanceInfo(definition, aimLine, out float endDistance, out _, out _);
                max = Mathf.Max(max, endDistance);
            }

            return max;
        }

        private T GetAssignedAimLine<T>(T[] aimLines, int bulletIndex) where T : class
        {
            if (aimLines == null || aimLines.Length == 0)
            {
                return null;
            }

            int index = bulletIndex % aimLines.Length;
            if (aimLines[index] != null)
            {
                return aimLines[index];
            }

            for (int i = 0; i < aimLines.Length; i++)
            {
                if (aimLines[i] != null)
                {
                    return aimLines[i];
                }
            }

            return null;
        }
    }
}
