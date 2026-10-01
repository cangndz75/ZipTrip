using UnityEditor;
using UnityEngine;
using ZipTrip.Unity;

namespace ZipTrip.Editor
{
    public static class PhaseALevelValidationMenu
    {
        [MenuItem("ZipTrip/Validate All Levels")]
        public static void ValidateAllLevels()
        {
            foreach (var result in PhaseALevelValidator.ValidateAll())
                Debug.Log(result.Level.Id + ": " + result.WithoutFold.SolutionDensity +
                    " (" + result.WithoutFold.CappedSolutionCount + ")" +
                    (result.WithFold == null ? "" : ", Fold=" + result.WithFold.SolutionDensity));
            Debug.Log("ZipTrip L1-L4 metric snapshots passed.");
        }
    }
}
