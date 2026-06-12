using UnityEngine;
using UnityEngine.UI;

namespace Uraty.Features.Character
{
    public sealed class CharacterReloadBar : MaskableGraphic
    {
        private const int DefaultSegmentCount = 3;
        private const float MinSize = 0.01f;
        private const float MinBillboardDirectionSqrMagnitude = 0.0001f;

        [Header("Reload Source")]
        [SerializeField] private CharacterStatus _characterStatus;

        [Header("Reload")]
        [SerializeField] private int _segmentCount = DefaultSegmentCount;

        [Header("Layout")]
        [SerializeField] private float _gap = 6.0f;
        [SerializeField] private float _roundRadius = 10.0f;

        [Header("Color")]
        [SerializeField] private Color _backgroundColor = new(0.05f, 0.05f, 0.05f, 0.85f);
        [SerializeField] private Color _fillColor = new(0.95f, 0.75f, 0.15f, 1.0f);
        [SerializeField] private Color _outlineColor = Color.black;

        [Header("Outline")]
        [SerializeField] private float _outlineThickness = 3.0f;

        [Header("Billboard")]
        [SerializeField] private bool _isBillboardEnabled = true;
        [SerializeField] private bool _isYawOnlyBillboard;

        private Camera _mainCamera;

        private float _lastReloadValue = -1.0f;
        private float _lastMaxReloadValue = -1.0f;

        public void SetUiVisible(bool isVisible)
        {
            enabled = isVisible;
        }

        private void Awake()
        {
            CacheReferences();
            CacheMainCamera();
        }

        private void Update()
        {
            UpdateReloadBar();
        }

        private void LateUpdate()
        {
            UpdateBillboard();
        }

        private void OnValidate()
        {
            _segmentCount = Mathf.Max(1, _segmentCount);
            _gap = Mathf.Max(0.0f, _gap);
            _roundRadius = Mathf.Max(0.0f, _roundRadius);
            _outlineThickness = Mathf.Max(0.0f, _outlineThickness);

            if (_characterStatus == null)
            {
                _characterStatus = GetComponentInParent<CharacterStatus>();
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (_characterStatus == null)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();

            if (rect.width <= MinSize || rect.height <= MinSize)
            {
                return;
            }

            int segmentCount = Mathf.Max(1, _segmentCount);

            float maxReloadValue = Mathf.Max(1.0f, _characterStatus.MaxReloadCount);
            float reloadValue = Mathf.Clamp(
                _characterStatus.CurrentReloadCount,
                0.0f,
                maxReloadValue);

            float totalGapWidth = _gap * (segmentCount - 1);
            float segmentWidth = (rect.width - totalGapWidth) / segmentCount;

            if (segmentWidth <= MinSize)
            {
                return;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                float xMin = rect.xMin + i * (segmentWidth + _gap);

                Rect outlineRect = new(
                    xMin,
                    rect.yMin,
                    segmentWidth,
                    rect.height);

                Rect backgroundRect = ShrinkRect(
                    outlineRect,
                    _outlineThickness);

                AddRoundedRect(
                    vertexHelper,
                    outlineRect,
                    _roundRadius,
                    _outlineColor);

                AddRoundedRect(
                    vertexHelper,
                    backgroundRect,
                    Mathf.Max(0.0f, _roundRadius - _outlineThickness),
                    _backgroundColor);

                float segmentFillRatio = GetSegmentFillRatio(
                    i,
                    reloadValue,
                    maxReloadValue,
                    segmentCount);

                if (segmentFillRatio <= 0.0f)
                {
                    continue;
                }

                Rect fillRect = backgroundRect;
                fillRect.width *= segmentFillRatio;

                AddRoundedRect(
                    vertexHelper,
                    fillRect,
                    Mathf.Max(0.0f, _roundRadius - _outlineThickness),
                    _fillColor);
            }
        }

        /// <summary>
        /// 必要な参照をキャッシュする。
        /// </summary>
        private void CacheReferences()
        {
            if (_characterStatus != null)
            {
                return;
            }

            _characterStatus = GetComponentInParent<CharacterStatus>();
        }

        /// <summary>
        /// MainCameraタグが付いているカメラをキャッシュする。
        /// </summary>
        private void CacheMainCamera()
        {
            if (_mainCamera != null)
            {
                return;
            }

            _mainCamera = Camera.main;
        }

        /// <summary>
        /// CharacterStatusのリロード値変更を検知して描画を更新する。
        /// </summary>
        private void UpdateReloadBar()
        {
            if (_characterStatus == null)
            {
                CacheReferences();

                if (_characterStatus == null)
                {
                    return;
                }
            }

            float maxReloadValue = Mathf.Max(1.0f, _characterStatus.MaxReloadCount);

            float reloadValue = Mathf.Clamp(
                _characterStatus.CurrentReloadCount,
                0.0f,
                maxReloadValue);

            if (Mathf.Approximately(reloadValue, _lastReloadValue) &&
                Mathf.Approximately(maxReloadValue, _lastMaxReloadValue))
            {
                return;
            }

            _lastReloadValue = reloadValue;
            _lastMaxReloadValue = maxReloadValue;

            SetVerticesDirty();
        }

        /// <summary>
        /// リロードバーをMainCameraに対してビルボードさせる。
        /// </summary>
        private void UpdateBillboard()
        {
            if (!_isBillboardEnabled)
            {
                return;
            }

            if (_mainCamera == null)
            {
                CacheMainCamera();

                if (_mainCamera == null)
                {
                    return;
                }
            }

            Transform cameraTransform = _mainCamera.transform;

            if (_isYawOnlyBillboard)
            {
                Vector3 forward = cameraTransform.forward;
                forward.y = 0.0f;

                if (forward.sqrMagnitude <= MinBillboardDirectionSqrMagnitude)
                {
                    return;
                }

                transform.rotation = Quaternion.LookRotation(
                    forward.normalized,
                    Vector3.up);

                return;
            }

            transform.rotation = Quaternion.LookRotation(
                cameraTransform.forward,
                cameraTransform.up);
        }

        /// <summary>
        /// 指定セグメントの充填率を取得する。
        /// </summary>
        /// <param name="segmentIndex">左から数えたセグメント番号。</param>
        /// <param name="reloadValue">現在リロード値。</param>
        /// <param name="maxReloadValue">最大リロード値。</param>
        /// <param name="segmentCount">セグメント数。</param>
        /// <returns>0から1の充填率。</returns>
        private static float GetSegmentFillRatio(
            int segmentIndex,
            float reloadValue,
            float maxReloadValue,
            int segmentCount)
        {
            float reloadPerSegment = maxReloadValue / segmentCount;

            float segmentStartValue = reloadPerSegment * segmentIndex;
            float segmentEndValue = segmentStartValue + reloadPerSegment;

            if (reloadValue >= segmentEndValue)
            {
                return 1.0f;
            }

            if (reloadValue <= segmentStartValue)
            {
                return 0.0f;
            }

            return Mathf.InverseLerp(
                segmentStartValue,
                segmentEndValue,
                reloadValue);
        }

        /// <summary>
        /// Rectを内側に縮小する。
        /// </summary>
        /// <param name="rect">元のRect。</param>
        /// <param name="amount">縮小量。</param>
        /// <returns>縮小後のRect。</returns>
        private static Rect ShrinkRect(Rect rect, float amount)
        {
            float validAmount = Mathf.Max(0.0f, amount);

            return new Rect(
                rect.x + validAmount,
                rect.y + validAmount,
                Mathf.Max(0.0f, rect.width - validAmount * 2.0f),
                Mathf.Max(0.0f, rect.height - validAmount * 2.0f));
        }

        /// <summary>
        /// 角丸矩形を追加する。
        /// </summary>
        /// <param name="vertexHelper">頂点ヘルパー。</param>
        /// <param name="rect">描画Rect。</param>
        /// <param name="radius">角丸半径。</param>
        /// <param name="drawColor">描画色。</param>
        private static void AddRoundedRect(
            VertexHelper vertexHelper,
            Rect rect,
            float radius,
            Color drawColor)
        {
            float validRadius = Mathf.Min(
                Mathf.Max(0.0f, radius),
                rect.width * 0.5f,
                rect.height * 0.5f);

            if (validRadius <= 0.0f)
            {
                AddQuad(
                    vertexHelper,
                    new Vector2(rect.xMin, rect.yMin),
                    new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMax, rect.yMax),
                    new Vector2(rect.xMax, rect.yMin),
                    drawColor);

                return;
            }

            AddQuad(
                vertexHelper,
                new Vector2(rect.xMin + validRadius, rect.yMin),
                new Vector2(rect.xMin + validRadius, rect.yMax),
                new Vector2(rect.xMax - validRadius, rect.yMax),
                new Vector2(rect.xMax - validRadius, rect.yMin),
                drawColor);

            AddQuad(
                vertexHelper,
                new Vector2(rect.xMin, rect.yMin + validRadius),
                new Vector2(rect.xMin, rect.yMax - validRadius),
                new Vector2(rect.xMin + validRadius, rect.yMax - validRadius),
                new Vector2(rect.xMin + validRadius, rect.yMin + validRadius),
                drawColor);

            AddQuad(
                vertexHelper,
                new Vector2(rect.xMax - validRadius, rect.yMin + validRadius),
                new Vector2(rect.xMax - validRadius, rect.yMax - validRadius),
                new Vector2(rect.xMax, rect.yMax - validRadius),
                new Vector2(rect.xMax, rect.yMin + validRadius),
                drawColor);

            AddCorner(
                vertexHelper,
                new Vector2(rect.xMin + validRadius, rect.yMin + validRadius),
                validRadius,
                180.0f,
                270.0f,
                drawColor);

            AddCorner(
                vertexHelper,
                new Vector2(rect.xMin + validRadius, rect.yMax - validRadius),
                validRadius,
                90.0f,
                180.0f,
                drawColor);

            AddCorner(
                vertexHelper,
                new Vector2(rect.xMax - validRadius, rect.yMax - validRadius),
                validRadius,
                0.0f,
                90.0f,
                drawColor);

            AddCorner(
                vertexHelper,
                new Vector2(rect.xMax - validRadius, rect.yMin + validRadius),
                validRadius,
                270.0f,
                360.0f,
                drawColor);
        }

        /// <summary>
        /// 角丸の角部分を追加する。
        /// </summary>
        /// <param name="vertexHelper">頂点ヘルパー。</param>
        /// <param name="center">中心座標。</param>
        /// <param name="radius">半径。</param>
        /// <param name="startDegree">開始角度。</param>
        /// <param name="endDegree">終了角度。</param>
        /// <param name="drawColor">描画色。</param>
        private static void AddCorner(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float startDegree,
            float endDegree,
            Color drawColor)
        {
            const int CornerSegmentCount = 6;

            int centerIndex = vertexHelper.currentVertCount;

            AddVertex(vertexHelper, center, drawColor);

            for (int i = 0; i <= CornerSegmentCount; i++)
            {
                float t = i / (float)CornerSegmentCount;
                float degree = Mathf.Lerp(startDegree, endDegree, t);
                float radian = degree * Mathf.Deg2Rad;

                Vector2 position = center + new Vector2(
                    Mathf.Cos(radian),
                    Mathf.Sin(radian)) * radius;

                AddVertex(vertexHelper, position, drawColor);
            }

            for (int i = 1; i <= CornerSegmentCount; i++)
            {
                vertexHelper.AddTriangle(
                    centerIndex,
                    centerIndex + i,
                    centerIndex + i + 1);
            }
        }

        /// <summary>
        /// 四角形を追加する。
        /// </summary>
        /// <param name="vertexHelper">頂点ヘルパー。</param>
        /// <param name="leftBottom">左下。</param>
        /// <param name="leftTop">左上。</param>
        /// <param name="rightTop">右上。</param>
        /// <param name="rightBottom">右下。</param>
        /// <param name="drawColor">描画色。</param>
        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 leftBottom,
            Vector2 leftTop,
            Vector2 rightTop,
            Vector2 rightBottom,
            Color drawColor)
        {
            int startIndex = vertexHelper.currentVertCount;

            AddVertex(vertexHelper, leftBottom, drawColor);
            AddVertex(vertexHelper, leftTop, drawColor);
            AddVertex(vertexHelper, rightTop, drawColor);
            AddVertex(vertexHelper, rightBottom, drawColor);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        /// <summary>
        /// 頂点を追加する。
        /// </summary>
        /// <param name="vertexHelper">頂点ヘルパー。</param>
        /// <param name="position">座標。</param>
        /// <param name="drawColor">描画色。</param>
        private static void AddVertex(
            VertexHelper vertexHelper,
            Vector2 position,
            Color drawColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = drawColor;

            vertexHelper.AddVert(vertex);
        }
    }
}
