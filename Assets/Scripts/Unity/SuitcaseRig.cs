using System;
using UnityEngine;

namespace ZipTrip.Unity
{
    /// <summary>
    /// Completion-only anchors on the instantiated container; gameplay never reads this component.
    /// Future container prefabs need the ContainerRig Base/Lid/InteriorAnchor/InteriorMin/InteriorMax/HingeAnchor
    /// contract. Lid is the required completion pivot: its authored open rotation and identity closed rotation must
    /// remain valid. Optional direct children are StrapA and StrapB with Use Authored Strap Poses enabled and open/
    /// closed local position, Euler rotation and scale set for each; ZipperPull with ZipperStart/ZipperEnd; and
    /// CelebrationOrigin. These can also be assigned directly on this component.
    /// Absent zipper anchors use the rim sweep. The Golden suitcase has no separate strap meshes, so two plain
    /// collider-free bands are generated at runtime. Missing optional elements never affect Domain completion.
    /// </summary>
    public sealed class SuitcaseRig : MonoBehaviour
    {
        [Serializable]
        private struct StrapPose
        {
            public Vector3 OpenPosition;
            public Vector3 ClosedPosition;
            public Vector3 OpenEuler;
            public Vector3 ClosedEuler;
            public Vector3 OpenScale;
            public Vector3 ClosedScale;
        }

        [SerializeField] private Transform lidPivot;
        [SerializeField] private Transform strapA;
        [SerializeField] private Transform strapB;
        [SerializeField] private Transform zipperPull;
        [SerializeField] private Transform zipperStart;
        [SerializeField] private Transform zipperEnd;
        [SerializeField] private Transform celebrationOrigin;
        [SerializeField] private bool useAuthoredStrapPoses;
        [SerializeField] private StrapPose strapAPose;
        [SerializeField] private StrapPose strapBPose;
        private Vector3 _strapAClosedScale;
        private Vector3 _strapBClosedScale;
        private Material _generatedStrapMaterial;
        private Transform _boundRoot;

        public Transform LidPivot => lidPivot;
        public Transform StrapA => strapA;
        public Transform StrapB => strapB;
        public Transform ZipperPull => zipperPull;
        public Transform ZipperStart => zipperStart;
        public Transform ZipperEnd => zipperEnd;
        public Transform CelebrationOrigin => celebrationOrigin != null ? celebrationOrigin : transform;
        public bool HasStraps => strapA != null && strapB != null;
        public bool HasZipperPath => zipperPull != null && zipperStart != null && zipperEnd != null;

        public void Bind(ContainerRig container)
        {
            if (container == null || container.Lid == null)
                throw new InvalidOperationException("SuitcaseRig requires a lid pivot.");
            if (_boundRoot == container.Root)
                return;
            _boundRoot = container.Root;
            lidPivot = container.Lid;
            if (strapA == null) strapA = transform.Find("StrapA");
            if (strapB == null) strapB = transform.Find("StrapB");
            if (zipperPull == null) zipperPull = transform.Find("ZipperPull");
            if (zipperStart == null) zipperStart = transform.Find("ZipperStart");
            if (zipperEnd == null) zipperEnd = transform.Find("ZipperEnd");
            if (celebrationOrigin == null) celebrationOrigin = transform.Find("CelebrationOrigin");
            if (!HasStraps)
                BuildPresentationStraps(container);
            _strapAClosedScale = strapA != null ? strapA.localScale : Vector3.one;
            _strapBClosedScale = strapB != null ? strapB.localScale : Vector3.one;
            SetStraps(0f, 0f);
            if (!HasZipperPath)
                Debug.LogWarning("SuitcaseRig: optional zipper anchors absent; rim sweep is used.", this);
        }

        public void RequireLid()
        {
            if (lidPivot == null)
                throw new InvalidOperationException("SuitcaseRig requires a lid pivot.");
        }

        public void SetStraps(float a, float b)
        {
            if (useAuthoredStrapPoses)
            {
                ApplyPose(strapA, strapAPose, a);
                ApplyPose(strapB, strapBPose, b);
                return;
            }
            if (strapA != null)
                strapA.localScale = Vector3.Scale(_strapAClosedScale, new Vector3(1f, 1f, Mathf.Clamp01(a)));
            if (strapB != null)
                strapB.localScale = Vector3.Scale(_strapBClosedScale, new Vector3(1f, 1f, Mathf.Clamp01(b)));
        }

        private static void ApplyPose(Transform strap, StrapPose pose, float progress)
        {
            if (strap == null) return;
            var t = Mathf.Clamp01(progress);
            strap.localPosition = Vector3.Lerp(pose.OpenPosition, pose.ClosedPosition, t);
            strap.localRotation = Quaternion.Slerp(Quaternion.Euler(pose.OpenEuler), Quaternion.Euler(pose.ClosedEuler), t);
            strap.localScale = Vector3.Lerp(pose.OpenScale, pose.ClosedScale, t);
        }

        // The approved Golden model has no separate strap meshes. These collider-free bands change presentation only.
        private void BuildPresentationStraps(ContainerRig container)
        {
            if (strapA != null || strapB != null)
            {
                Debug.LogWarning("SuitcaseRig: incomplete optional strap pair; strap beat is skipped.", this);
                return;
            }
            var min = container.InteriorMinLocal;
            var max = container.InteriorMaxLocal;
            var baseTop = container.Base.GetComponent<Renderer>().bounds.max.y;
            var y = transform.InverseTransformPoint(new Vector3(container.Root.position.x, baseTop + 0.03f, container.Root.position.z)).y;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning("SuitcaseRig: no strap shader; optional strap beat is skipped.", this);
                return;
            }
            _generatedStrapMaterial = new Material(shader);
            var leather = PresentationKit.Hex(0x593B30);
            _generatedStrapMaterial.SetColor(PresentationKit.BaseColorId, leather);
            _generatedStrapMaterial.SetColor(PresentationKit.ColorId, leather);
            _generatedStrapMaterial.SetFloat("_Smoothness", 0.2f);
            strapA = CreateBand("StrapA", Mathf.Lerp(min.x, max.x, 0.33f), y, min.z, max.z);
            strapB = CreateBand("StrapB", Mathf.Lerp(min.x, max.x, 0.67f), y, min.z, max.z);
        }

        private Transform CreateBand(string name, float x, float y, float z0, float z1)
        {
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = name;
            band.transform.SetParent(transform, false);
            band.transform.localPosition = new Vector3(x, y, (z0 + z1) * 0.5f);
            band.transform.localScale = new Vector3(0.018f, 0.009f, Mathf.Abs(z1 - z0));
            Destroy(band.GetComponent<Collider>());
            band.GetComponent<Renderer>().sharedMaterial = _generatedStrapMaterial;
            return band.transform;
        }

        private void OnDestroy()
        {
            if (_generatedStrapMaterial != null)
                Destroy(_generatedStrapMaterial);
        }
    }
}
