using TMPro;

using UnityEngine;

namespace Uraty.Features.Character
{
    public sealed class CharacterHP : MonoBehaviour
    {
        [SerializeField] private CharacterStatus _characterStatus;
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private Canvas _canvas;

        private Camera _mainCamera;

        private float _lastHp = -1f;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            UpdateHpText();
        }

        private void LateUpdate()
        {
            BillboardToMainCamera();
        }

        private void UpdateHpText()
        {
            if (_characterStatus == null || _hpText == null)
            {
                return;
            }

            float currentHp = _characterStatus.CurrentHp;

            if (Mathf.Approximately(_lastHp, currentHp))
            {
                return;
            }

            _lastHp = currentHp;

            _hpText.text = Mathf.CeilToInt(currentHp).ToString();
        }

        private void BillboardToMainCamera()
        {
            if (_canvas == null)
            {
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;

                if (_mainCamera == null)
                {
                    return;
                }
            }

            Transform canvasTransform = _canvas.transform;
            Transform cameraTransform = _mainCamera.transform;

            canvasTransform.rotation = Quaternion.LookRotation(
                canvasTransform.position - cameraTransform.position,
                cameraTransform.up
            );
        }
    }
}
