using System;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    [RequireComponent(typeof(Camera))]
    public sealed class FixedGameplayCamera : MonoBehaviour
    {
        public const float Distance = 12f;
        public const float HorizontalMargin = 0.4f;
        public const float BottomSafetyMargin = 0.4f;
        private const float Pitch = 75f;

        private Camera _camera;
        private ContainerDefinition _container;
        private Bounds? _presentationBounds;
        private float _lastAspect;

        public static float OrthographicSize(int outerWidth, float aspect)
        {
            if (outerWidth <= 0 || aspect <= 0f)
                throw new ArgumentOutOfRangeException();
            return (outerWidth + 2f * HorizontalMargin) / (2f * aspect);
        }

        public static RectInt OuterBounds(ContainerMask mask)
        {
            if (mask == null)
                throw new ArgumentNullException(nameof(mask));
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            foreach (var cell in mask.GetValidCells())
            {
                minX = Math.Min(minX, cell.X);
                minY = Math.Min(minY, cell.Y);
                maxX = Math.Max(maxX, cell.X);
                maxY = Math.Max(maxY, cell.Y);
            }
            if (minX == int.MaxValue)
                throw new ArgumentException("Container mask is empty.", nameof(mask));
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public void Configure(ContainerDefinition container, float aspect)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            if (aspect <= 0f)
                throw new ArgumentOutOfRangeException(nameof(aspect));
            _presentationBounds = null;
            Apply(aspect);
        }

        public void Configure(ContainerDefinition container, float aspect, Bounds presentationBounds)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            if (aspect <= 0f)
                throw new ArgumentOutOfRangeException(nameof(aspect));
            _presentationBounds = presentationBounds;
            Apply(aspect);
        }

        private void Apply(float aspect)
        {
            _camera = GetComponent<Camera>();
            var bounds = OuterBounds(_container.Mask);
            var center = new Vector3(bounds.xMin + bounds.width * 0.5f, 0f,
                -(bounds.yMin + bounds.height * 0.5f));
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            transform.position = center - transform.forward * Distance;
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 30f;
            var size = OrthographicSize(bounds.width, aspect);
            if (_presentationBounds.HasValue)
            {
                var visual = _presentationBounds.Value;
                var min = visual.min;
                var max = visual.max;
                for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                for (var z = 0; z < 2; z++)
                {
                    var corner = transform.InverseTransformPoint(new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z));
                    size = Mathf.Max(size,
                        (Mathf.Abs(corner.x) + HorizontalMargin) / aspect,
                        corner.y + HorizontalMargin,
                        -corner.y + BottomSafetyMargin);
                }
            }
            _camera.orthographicSize = size;
            _lastAspect = aspect;
        }

        private void LateUpdate()
        {
            if (_container != null && !Mathf.Approximately(_camera.aspect, _lastAspect))
                Apply(_camera.aspect);
        }
    }
}
