using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    [RequireComponent(typeof(BoardPresenter))]
    public sealed class PhaseAL1Presentation : MonoBehaviour
    {
        [SerializeField] private string levelId = "L1";
        public LevelDefinition Level { get; private set; }
        public ulong InitialStateHash { get; private set; }
        public PackGameplayController Gameplay { get; private set; }

        private void Start()
        {
            Level = LoadDeviceLevel(levelId);
            InitialStateHash = StateHash.Compute(Level.InitialState);
            var board = GetComponent<BoardPresenter>();
            var camera = Camera.main;
            var pointer = gameObject.AddComponent<PointerInteractor>();
            var preview = gameObject.AddComponent<DragPreviewPresenter>();
            Gameplay = gameObject.AddComponent<PackGameplayController>();
            Gameplay.Initialize(Level, board, camera, pointer, preview, LoadLevel);
            preview.Initialize(board, camera, pointer);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<DebugGameplayOverlay>().Initialize(Gameplay, preview);
#endif
        }

        public void LoadLevel(string id)
        {
            Level = LoadDeviceLevel(id);
            levelId = id;
            InitialStateHash = StateHash.Compute(Level.InitialState);
            if (Gameplay != null)
                Gameplay.LoadLevel(Level);
        }

        private static LevelDefinition LoadDeviceLevel(string id)
        {
            if (id != "L1" && id != "L2" && id != "L3" && id != "L4")
                throw new System.ArgumentException("PhaseADeviceLevelMustBeL1ToL4", nameof(id));
            return PhaseALevels.Load(id);
        }
    }
}
