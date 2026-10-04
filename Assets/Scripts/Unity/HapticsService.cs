using UnityEngine;

namespace ZipTrip.Unity
{
    public enum FeelCue { ItemLift, ItemSettle, Reject, Rotate, Undo, RuleSatisfied, RuleViolated }

    // Presentation-only Android bridge. A disabled or unsupported device produces no vibration.
    public sealed class HapticsService : MonoBehaviour
    {
        [SerializeField] private bool cuesEnabled = true;
        public bool CuesEnabled { get => cuesEnabled; set => cuesEnabled = value; }

        public bool Play(FeelCue cue)
        {
            if (!cuesEnabled || UnityEngine.Application.platform != RuntimePlatform.Android)
                return false;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                using (var view = window.Call<AndroidJavaObject>("getDecorView"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    // API 30 names in the feel book; the P30 Pro's API 29 uses restrained supported constants.
                    var api30 = version.GetStatic<int>("SDK_INT") >= 30;
                    var effect = cue == FeelCue.ItemSettle || cue == FeelCue.RuleSatisfied ? (api30 ? 16 : 6)
                        : cue == FeelCue.Reject ? (api30 ? 17 : 6)
                        : cue == FeelCue.RuleViolated ? 4 : 4;
                    return view.Call<bool>("performHapticFeedback", effect);
                }
            }
            catch (AndroidJavaException)
            {
                return false;
            }
        }
    }
}
