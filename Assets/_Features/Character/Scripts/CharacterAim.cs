using System;
using System.Collections.Generic;

using TriInspector;

using UnityEngine;

namespace Uraty.Features.Character
{
    public abstract class CharacterAim : MonoBehaviour
    {
        private enum AimPreviewType
        {
            Line,
            Fan,
            Throw,
        }

        private const float MinDirectionSqrMagnitude = 0.0001f;
        private const float PreviewStartGap = 0.5f;
        private const int MinArcSegments = 6;
        private const int MaxArcSegments = 64;

        [Title("Definition")]
        [SerializeField, InlineProperty, HideLabel]
        private AimPreviewDefinition _previewDefinition = new();

        [Title("Prediction Mesh")]
        [SerializeField] private Material _predictionMaterial;

        [SerializeField] private float _groundOffset = 0.02f;

        [Title("Throw Prediction")]
        [Min(1)]
        [SerializeField] private int _throwSampleCount = 16;

        [Title("Request")]
        [SerializeField] private bool _consumeOnce = true;

        private Vector3 _aimPoint;
        private Vector3 _targetDirection = Vector3.forward;
        private Vector2 _aimScreenPosition;

        private Vector3 _releasedAimPoint;
        private Vector3 _releasedTargetDirection = Vector3.forward;
        private bool _releasedCanAutoAim;

        private bool _isAiming;
        private bool _hasValidAimPoint;
        private bool _hasActionRequest;

        private Transform _previewTransform;
        private MeshFilter _previewMeshFilter;
        private MeshRenderer _previewMeshRenderer;
        private Mesh _predictionMesh;

        private readonly List<Vector3> _vertices = new(256);
        private readonly List<int> _triangles = new(768);
        private readonly List<Vector3> _normals = new(256);

        protected abstract string PreviewObjectName
        {
            get;
        }

        private void Awake()
        {
            EnsurePreviewObject();

            _predictionMesh = new Mesh
            {
                name = $"{GetType().Name}_{nameof(_predictionMesh)}"
            };

            _predictionMesh.MarkDynamic();
            _previewMeshFilter.sharedMesh = _predictionMesh;

            ApplyPreviewMaterial();
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

        public void SetAim(
            Vector3 aimDirectionWorld,
            Vector3 aimPointWorld,
            Vector2 aimScreenPosition)
        {
            _aimScreenPosition = aimScreenPosition;

            aimDirectionWorld.y = 0f;
            aimPointWorld.y = GetFlatOrigin().y;

            if (aimDirectionWorld.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                _hasValidAimPoint = false;

                if (_isAiming)
                {
                    ResetPredictionMesh();
                }

                return;
            }

            _targetDirection = aimDirectionWorld.normalized;
            _aimPoint = aimPointWorld;
            _hasValidAimPoint = true;

            if (_isAiming)
            {
                UpdatePredictionMesh();
            }
        }

        public void BeginAim()
        {
            if (_previewDefinition == null)
            {
                return;
            }

            _isAiming = true;

            ApplyPreviewMaterial();

            if (_hasValidAimPoint)
            {
                UpdatePredictionMesh();
            }
            else
            {
                ResetPredictionMesh();
            }
        }

        public void CompleteAim()
        {
            if (!_isAiming)
            {
                return;
            }

            if (_previewDefinition == null)
            {
                ClearReleasedRequest();
                EndAim();
                return;
            }

            if (_hasValidAimPoint)
            {
                _releasedAimPoint = _aimPoint;
                _releasedTargetDirection = _targetDirection;
                _releasedCanAutoAim = false;
            }
            else
            {
                Vector3 fallbackDirection = GetFallbackDirection();

                _releasedTargetDirection = fallbackDirection;
                _releasedAimPoint = GetFlatOrigin() + (fallbackDirection * GetRange());
                _releasedCanAutoAim = true;
            }

            _hasActionRequest = true;

            EndAim();
        }

        public bool TryConsume(out Vector3 aimPoint, out Vector3 targetDirection)
        {
            return TryConsume(
                out aimPoint,
                out targetDirection,
                out _);
        }

        public bool TryConsume(
            out Vector3 aimPoint,
            out Vector3 targetDirection,
            out bool canAutoAim)
        {
            aimPoint = _releasedAimPoint;
            targetDirection = _releasedTargetDirection;
            canAutoAim = _releasedCanAutoAim;

            if (!_hasActionRequest)
            {
                return false;
            }

            if (_consumeOnce)
            {
                ClearReleasedRequest();
            }

            return true;
        }

        public bool IsAiming()
        {
            return _isAiming;
        }

        public bool HasRequest()
        {
            return _hasActionRequest;
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

            return GetFallbackDirection();
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

            return GetFlatOrigin() + (GetTargetDirection() * GetRange());
        }

        public Vector2 GetAimScreenPosition()
        {
            return _aimScreenPosition;
        }

        public float GetRange()
        {
            if (_previewDefinition == null)
            {
                return 0f;
            }

            switch (_previewDefinition.Type)
            {
                case AimPreviewType.Line:
                    return GetMaxLineRange(_previewDefinition.Line);

                case AimPreviewType.Fan:
                    return GetMaxFanRange(_previewDefinition.Fan);

                case AimPreviewType.Throw:
                    return GetMaxThrowDistance(_previewDefinition.Throw);

                default:
                    return 0f;
            }
        }

        private void EndAim()
        {
            _isAiming = false;
            HidePredictionMeshOnly();
        }

        private Vector3 GetFallbackDirection()
        {
            Vector3 direction = _targetDirection;
            direction.y = 0f;

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                direction = Vector3.forward;
            }

            return direction.normalized;
        }

        private void ApplyPreviewMaterial()
        {
            if (_previewMeshRenderer == null || _predictionMaterial == null)
            {
                return;
            }

            _previewMeshRenderer.sharedMaterial = _predictionMaterial;
        }

        private void EnsurePreviewObject()
        {
            Transform existingChild = transform.Find(PreviewObjectName);
            if (existingChild != null)
            {
                _previewTransform = existingChild;
            }
            else
            {
                GameObject previewObject = new(PreviewObjectName);
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

        private void InitializePredictionMesh()
        {
            _aimPoint = GetFlatOrigin();
            ClearMesh();

            if (_previewMeshRenderer != null)
            {
                _previewMeshRenderer.enabled = false;
            }
        }

        private void UpdatePredictionMesh()
        {
            if (_predictionMesh == null || _previewDefinition == null)
            {
                return;
            }

            if (!_hasValidAimPoint)
            {
                ResetPredictionMesh();
                return;
            }

            _vertices.Clear();
            _triangles.Clear();
            _normals.Clear();

            switch (_previewDefinition.Type)
            {
                case AimPreviewType.Line:
                    BuildLinePreview(_previewDefinition.Line);
                    break;

                case AimPreviewType.Fan:
                    BuildFanPreview(_previewDefinition.Fan);
                    break;

                case AimPreviewType.Throw:
                    BuildThrowPreview(_previewDefinition.Throw);
                    break;
            }

            ApplyMesh();
        }

        private void BuildLinePreview(LinePreviewDefinition definition)
        {
            if (definition == null || definition.Lines == null)
            {
                return;
            }

            for (int i = 0; i < definition.Lines.Length; i++)
            {
                LineAimLineSetting aimLine = definition.Lines[i];
                if (aimLine == null)
                {
                    continue;
                }

                float visibleLength = Mathf.Max(0f, aimLine.Range);
                if (visibleLength <= 0f || aimLine.Width <= 0f)
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

                Vector3 origin = GetRenderOrigin() + (forward * PreviewStartGap);

                AddLineArea(origin, forward, visibleLength, aimLine.Width, aimLine.OffsetDistanceFromAimLine);
            }
        }

        private void BuildFanPreview(FanPreviewDefinition definition)
        {
            if (definition == null || definition.Lines == null)
            {
                return;
            }

            for (int i = 0; i < definition.Lines.Length; i++)
            {
                FanAimLineSetting aimLine = definition.Lines[i];
                if (aimLine == null)
                {
                    continue;
                }

                float visibleLength = Mathf.Max(0f, aimLine.Range);
                float angle = Mathf.Max(0f, aimLine.Angle);

                if (visibleLength <= 0f || angle <= 0f)
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

                float innerRadius = PreviewStartGap;
                float outerRadius = PreviewStartGap + visibleLength;

                AddFanArea(GetRenderOrigin(), forward, innerRadius, outerRadius, angle);
            }
        }

        private void BuildThrowPreview(ThrowPreviewDefinition definition)
        {
            if (definition == null || definition.Lines == null)
            {
                return;
            }

            Vector3 flatOrigin = GetFlatOrigin();
            Vector3 renderOrigin = GetRenderOrigin();

            for (int i = 0; i < definition.Lines.Length; i++)
            {
                ThrowAimLineSetting aimLine = definition.Lines[i];
                if (aimLine == null)
                {
                    continue;
                }

                if (!TryGetThrowCircleInfo(
                        definition,
                        aimLine,
                        out _,
                        out Vector3 circleCenterPoint,
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
                ThrowBulletSetting bullet = definition.Bullets[i];
                if (bullet == null)
                {
                    continue;
                }

                ThrowAimLineSetting aimLine = GetAssignedAimLine(definition.Lines, i);
                if (aimLine == null)
                {
                    continue;
                }

                if (!TryGetThrowCircleInfo(
                        definition,
                        aimLine,
                        out Vector3 aimLineDirection,
                        out Vector3 circleCenterPoint,
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
                    PreviewStartGap);
            }
        }

        private bool TryGetThrowCircleInfo(
            ThrowPreviewDefinition definition,
            ThrowAimLineSetting aimLine,
            out Vector3 aimLineDirection,
            out Vector3 circleCenterPoint,
            out float distanceRatio)
        {
            aimLineDirection = _targetDirection;
            circleCenterPoint = GetFlatOrigin();
            distanceRatio = 0f;

            if (definition == null || aimLine == null)
            {
                return false;
            }

            GetThrowPreviewDistanceInfo(definition, aimLine, out float endDistance, out distanceRatio);

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

            float circleCenterDistance =
                Mathf.Max(0f, endDistance + aimLine.OffsetDistanceFromAimLine) +
                PreviewStartGap;

            circleCenterPoint = GetFlatOrigin() + (aimLineDirection * circleCenterDistance);
            circleCenterPoint.y = GetFlatOrigin().y;

            return true;
        }

        private void GetThrowPreviewDistanceInfo(
            ThrowPreviewDefinition definition,
            ThrowAimLineSetting aimLine,
            out float endDistance,
            out float distanceRatio)
        {
            endDistance = 0f;
            distanceRatio = 0f;

            float minRange = Mathf.Min(aimLine.MinRange, aimLine.MaxRange);
            float maxRange = Mathf.Max(aimLine.MinRange, aimLine.MaxRange);

            minRange = Mathf.Max(0f, minRange);
            maxRange = Mathf.Max(0f, maxRange);

            if (maxRange <= 0f)
            {
                return;
            }

            if (!definition.IsVariableRange)
            {
                endDistance = maxRange;
                distanceRatio = 1f;
                return;
            }

            float aimDistance = Mathf.Max(
                0f,
                GetPlanarDistance(GetFlatOrigin(), _aimPoint) - PreviewStartGap);

            if (maxRange <= minRange)
            {
                endDistance = maxRange;
                distanceRatio = 1f;
                return;
            }

            endDistance = Mathf.Clamp(aimDistance, minRange, maxRange);
            distanceRatio = Mathf.InverseLerp(minRange, maxRange, endDistance);
        }

        private float GetThrowPreviewParabolaHeight(
            ThrowPreviewDefinition definition,
            ThrowAimLineSetting aimLine,
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
            if (totalDistance <= MinDirectionSqrMagnitude)
            {
                return;
            }

            float clampedStartDistance = Mathf.Clamp(startDistance, 0f, totalDistance);

            if (clampedStartDistance >= totalDistance)
            {
                return;
            }

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
                ApplyPreviewMaterial();
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
        }

        private Vector3 GetFlatOrigin()
        {
            return transform.position;
        }

        private Vector3 GetRenderOrigin()
        {
            return transform.position + (Vector3.up * _groundOffset);
        }

        private float GetMaxLineRange(LinePreviewDefinition definition)
        {
            if (definition == null || definition.Lines == null)
            {
                return 0f;
            }

            float max = 0f;

            for (int i = 0; i < definition.Lines.Length; i++)
            {
                LineAimLineSetting line = definition.Lines[i];
                if (line == null)
                {
                    continue;
                }

                float endDistance = PreviewStartGap + Mathf.Max(0f, line.Range);
                max = Mathf.Max(max, endDistance);
            }

            return max;
        }

        private float GetMaxFanRange(FanPreviewDefinition definition)
        {
            if (definition == null || definition.Lines == null)
            {
                return 0f;
            }

            float max = 0f;

            for (int i = 0; i < definition.Lines.Length; i++)
            {
                FanAimLineSetting line = definition.Lines[i];
                if (line == null)
                {
                    continue;
                }

                float endDistance = PreviewStartGap + Mathf.Max(0f, line.Range);
                max = Mathf.Max(max, endDistance);
            }

            return max;
        }

        private float GetMaxThrowDistance(ThrowPreviewDefinition definition)
        {
            if (definition == null || definition.Lines == null)
            {
                return 0f;
            }

            float max = 0f;

            for (int i = 0; i < definition.Lines.Length; i++)
            {
                ThrowAimLineSetting aimLine = definition.Lines[i];
                if (aimLine == null)
                {
                    continue;
                }

                float maxRange = Mathf.Max(aimLine.MinRange, aimLine.MaxRange);
                float endDistance =
                    Mathf.Max(0f, maxRange + aimLine.OffsetDistanceFromAimLine) +
                    PreviewStartGap;

                max = Mathf.Max(max, endDistance);
            }

            return max;
        }

        private T GetAssignedAimLine<T>(T[] aimLines, int bulletIndex)
            where T : class
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

        [Serializable]
        private sealed class AimPreviewDefinition
        {
            [EnumToggleButtons]
            [SerializeField] private AimPreviewType _type = AimPreviewType.Line;

            [ShowIf(nameof(IsLine))]
            [SerializeField, InlineProperty, HideLabel]
            private LinePreviewDefinition _line = new();

            [ShowIf(nameof(IsFan))]
            [SerializeField, InlineProperty, HideLabel]
            private FanPreviewDefinition _fan = new();

            [ShowIf(nameof(IsThrow))]
            [SerializeField, InlineProperty, HideLabel]
            private ThrowPreviewDefinition _throw = new();

            public AimPreviewType Type => _type;
            public LinePreviewDefinition Line => _line;
            public FanPreviewDefinition Fan => _fan;
            public ThrowPreviewDefinition Throw => _throw;

            private bool IsLine => _type == AimPreviewType.Line;
            private bool IsFan => _type == AimPreviewType.Fan;
            private bool IsThrow => _type == AimPreviewType.Throw;
        }

        [Serializable]
        private sealed class LinePreviewDefinition
        {
            [SerializeField]
            private LineAimLineSetting[] _lines =
            {
                new()
            };

            public LineAimLineSetting[] Lines => _lines;
        }

        [Serializable]
        private sealed class FanPreviewDefinition
        {
            [SerializeField]
            private FanAimLineSetting[] _lines =
            {
                new()
            };

            public FanAimLineSetting[] Lines => _lines;
        }

        [Serializable]
        private sealed class ThrowPreviewDefinition
        {
            [SerializeField] private bool _isVariableRange = true;
            [SerializeField] private bool _isVariableLandingTime = true;

            [SerializeField]
            private ThrowAimLineSetting[] _lines =
            {
                new()
            };

            [SerializeField]
            private ThrowBulletSetting[] _bullets =
            {
                new()
            };

            public bool IsVariableRange => _isVariableRange;
            public bool IsVariableLandingTime => _isVariableLandingTime;
            public ThrowAimLineSetting[] Lines => _lines;
            public ThrowBulletSetting[] Bullets => _bullets;
        }

        [Serializable]
        private sealed class LineAimLineSetting
        {
            [Min(0f)]
            [SerializeField] private float _range = 6f;

            [Min(0f)]
            [SerializeField] private float _width = 1f;

            [SerializeField] private float _offsetAngleFromAimLine;

            [SerializeField] private float _offsetDistanceFromAimLine;

            public float Range => _range;
            public float Width => _width;
            public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
            public float OffsetDistanceFromAimLine => _offsetDistanceFromAimLine;
        }

        [Serializable]
        private sealed class FanAimLineSetting
        {
            [Min(0f)]
            [SerializeField] private float _range = 5f;

            [Min(0f)]
            [SerializeField] private float _angle = 45f;

            [SerializeField] private float _offsetAngleFromAimLine;

            public float Range => _range;
            public float Angle => _angle;
            public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
        }

        [Serializable]
        private sealed class ThrowAimLineSetting
        {
            [Min(0f)]
            [SerializeField] private float _circleRadius = 1f;

            [Min(0f)]
            [SerializeField] private float _minRange = 1f;

            [Min(0f)]
            [SerializeField] private float _maxRange = 7f;

            [SerializeField] private float _offsetAngleFromAimLine;

            [SerializeField] private float _offsetDistanceFromAimLine;

            [Min(0f)]
            [SerializeField] private float _fixedParabolaHeight = 2f;

            [Min(0f)]
            [SerializeField] private float _minParabolaHeight = 1f;

            [Min(0f)]
            [SerializeField] private float _maxParabolaHeight = 3f;

            [Min(0f)]
            [SerializeField] private float _parabolaWidth = 0.25f;

            public float CircleRadius => _circleRadius;
            public float MinRange => _minRange;
            public float MaxRange => _maxRange;
            public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
            public float OffsetDistanceFromAimLine => _offsetDistanceFromAimLine;
            public float FixedParabolaHeight => _fixedParabolaHeight;
            public float MinParabolaHeight => _minParabolaHeight;
            public float MaxParabolaHeight => _maxParabolaHeight;
            public float ParabolaWidth => _parabolaWidth;
        }

        [Serializable]
        private sealed class ThrowBulletSetting
        {
            [SerializeField] private float _offsetAngleFromAimLine;

            [SerializeField] private float _offsetDistanceFromAimLine;

            public float OffsetAngleFromAimLine => _offsetAngleFromAimLine;
            public float OffsetDistanceFromAimLine => _offsetDistanceFromAimLine;
        }
    }
}
