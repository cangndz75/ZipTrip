using System;
using UnityEngine;

namespace ZipTrip.Unity
{
    // ZA-004 static scene only. Fits the approved suitcase shell on narrow phones.
    [RequireComponent(typeof(Camera))]
    public sealed class ReadabilityPortraitFraming : MonoBehaviour
    {
        public const float ApprovedSizeAtNineSixteen = 6.74f;
        public const float ExteriorWidth = 7.20f;
        public const float SideMargin = 0.19f;

        private Camera _camera;
        private float _lastAspect;

        public static float SizeForAspect(float aspect)
        {
            if (aspect <= 0f)
                throw new ArgumentOutOfRangeException(nameof(aspect));
            return Mathf.Max(ApprovedSizeAtNineSixteen,
                (ExteriorWidth + 2f * SideMargin) / (2f * aspect));
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Apply();
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(_lastAspect, _camera.aspect))
                Apply();
        }

        private void Apply()
        {
            _lastAspect = _camera.aspect;
            _camera.orthographicSize = SizeForAspect(_lastAspect);
        }
    }
}
