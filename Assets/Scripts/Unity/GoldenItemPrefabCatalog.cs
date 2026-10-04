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
        [SerializeField] private GameObject passport;
        [SerializeField] private GameObject towel;
        [SerializeField] private GameObject shampoo;
        [SerializeField] private GameObject sunglasses;
        [SerializeField] private GameObject travelPouch;

        public void Configure(GameObject laptopPrefab, GameObject sneakerPairPrefab,
            GameObject sweaterOpenPrefab, GameObject sweaterFoldedPrefab)
        {
            laptop = laptopPrefab ?? throw new ArgumentNullException(nameof(laptopPrefab));
            sneakerPair = sneakerPairPrefab ?? throw new ArgumentNullException(nameof(sneakerPairPrefab));
            sweaterOpen = sweaterOpenPrefab ?? throw new ArgumentNullException(nameof(sweaterOpenPrefab));
            sweaterFolded = sweaterFoldedPrefab ?? throw new ArgumentNullException(nameof(sweaterFoldedPrefab));
        }

        public void ConfigureGoldenLv1Final(GameObject passportPrefab, GameObject towelPrefab,
            GameObject shampooPrefab, GameObject sunglassesPrefab, GameObject travelPouchPrefab)
        {
            passport = passportPrefab ?? throw new ArgumentNullException(nameof(passportPrefab));
            towel = towelPrefab ?? throw new ArgumentNullException(nameof(towelPrefab));
            shampoo = shampooPrefab ?? throw new ArgumentNullException(nameof(shampooPrefab));
            sunglasses = sunglassesPrefab ?? throw new ArgumentNullException(nameof(sunglassesPrefab));
            travelPouch = travelPouchPrefab ?? throw new ArgumentNullException(nameof(travelPouchPrefab));
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
                case "PF_Item_Passport" when shapeState == "open":
                    result = passport;
                    break;
                case "PF_Item_Towel" when shapeState == "open":
                    result = towel;
                    break;
                case "PF_Item_Shampoo" when shapeState == "open":
                    result = shampoo;
                    break;
                case "PF_Item_Sunglasses" when shapeState == "open":
                    result = sunglasses;
                    break;
                case "PF_Item_TravelPouch" when shapeState == "open":
                    result = travelPouch;
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
