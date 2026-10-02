using System;
using UnityEngine;

namespace ZipTrip.Unity
{
    // ZT-016 presentation mapping. Prefabs remain script-free visual assets.
    public sealed class GoldenItemPrefabCatalog : MonoBehaviour
    {
        [SerializeField] private GameObject laptop;
        [SerializeField] private GameObject sneakerPair;
        [SerializeField] private GameObject sweaterOpen;
        [SerializeField] private GameObject sweaterFolded;

        public void Configure(GameObject laptopPrefab, GameObject sneakerPairPrefab,
            GameObject sweaterOpenPrefab, GameObject sweaterFoldedPrefab)
        {
            laptop = laptopPrefab ?? throw new ArgumentNullException(nameof(laptopPrefab));
            sneakerPair = sneakerPairPrefab ?? throw new ArgumentNullException(nameof(sneakerPairPrefab));
            sweaterOpen = sweaterOpenPrefab ?? throw new ArgumentNullException(nameof(sweaterOpenPrefab));
            sweaterFolded = sweaterFoldedPrefab ?? throw new ArgumentNullException(nameof(sweaterFoldedPrefab));
        }

        public GameObject Resolve(string visualPrefabId, string shapeState)
        {
            GameObject result;
            switch (visualPrefabId)
            {
                case "PF_Item_Laptop" when shapeState == "open":
                    result = laptop;
                    break;
                case "PF_Item_SneakerPair" when shapeState == "open":
                    result = sneakerPair;
                    break;
                case "PF_Item_Sweater" when shapeState == "open":
                    result = sweaterOpen;
                    break;
                case "PF_Item_Sweater" when shapeState == "folded":
                    result = sweaterFolded;
                    break;
                default:
                    throw new ArgumentException("Unknown golden visual mapping: " +
                        visualPrefabId + "/" + shapeState);
            }

            if (result == null)
                throw new InvalidOperationException("Golden prefab reference is missing: " +
                    visualPrefabId + "/" + shapeState);
            return result;
        }
    }
}
