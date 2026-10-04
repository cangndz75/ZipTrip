using UnityEngine;

namespace ZipTrip.Unity
{
    // Semantic cue router. Clip slots are intentionally empty until approved audio assets exist.
    public sealed class AudioCueService : MonoBehaviour
    {
        [SerializeField] private bool cuesEnabled = true;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip lift;
        [SerializeField] private AudioClip fabricSettle;
        [SerializeField] private AudioClip leatherSettle;
        [SerializeField] private AudioClip plasticSettle;
        [SerializeField] private AudioClip paperSettle;
        [SerializeField] private AudioClip metalSettle;
        [SerializeField] private AudioClip reject;
        [SerializeField] private AudioClip rotate;
        [SerializeField] private AudioClip undo;
        [SerializeField] private AudioClip ruleSatisfied;
        [SerializeField] private AudioClip ruleViolated;

        public bool CuesEnabled { get => cuesEnabled; set => cuesEnabled = value; }
        public void ConfigureSource(AudioSource audioSource) => source = audioSource;

        public void ConfigureClip(FeelCue cue, AudioClip clip, MaterialFamily material = MaterialFamily.Neutral)
        {
            switch (cue)
            {
                case FeelCue.ItemLift: lift = clip; break;
                case FeelCue.ItemSettle:
                    switch (material)
                    {
                        case MaterialFamily.Fabric: fabricSettle = clip; break;
                        case MaterialFamily.Leather: leatherSettle = clip; break;
                        case MaterialFamily.Plastic: plasticSettle = clip; break;
                        case MaterialFamily.Paper: paperSettle = clip; break;
                        default: metalSettle = clip; break;
                    }
                    break;
                case FeelCue.Reject: reject = clip; break;
                case FeelCue.Rotate: rotate = clip; break;
                case FeelCue.Undo: undo = clip; break;
                case FeelCue.RuleSatisfied: ruleSatisfied = clip; break;
                case FeelCue.RuleViolated: ruleViolated = clip; break;
            }
        }

        public bool Play(FeelCue cue, MaterialFamily material = MaterialFamily.Neutral)
        {
            if (!cuesEnabled || source == null)
                return false;
            AudioClip clip;
            switch (cue)
            {
                case FeelCue.ItemLift: clip = lift; break;
                case FeelCue.ItemSettle:
                    clip = material == MaterialFamily.Fabric ? fabricSettle
                        : material == MaterialFamily.Leather ? leatherSettle
                        : material == MaterialFamily.Plastic ? plasticSettle
                        : material == MaterialFamily.Paper ? paperSettle : metalSettle;
                    break;
                case FeelCue.Reject: clip = reject; break;
                case FeelCue.Rotate: clip = rotate; break;
                case FeelCue.Undo: clip = undo; break;
                case FeelCue.RuleSatisfied: clip = ruleSatisfied; break;
                default: clip = ruleViolated; break;
            }
            if (clip == null)
                return false;
            source.PlayOneShot(clip);
            return true;
        }
    }
}
