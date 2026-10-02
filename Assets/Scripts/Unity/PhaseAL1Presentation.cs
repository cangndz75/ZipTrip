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

        private void Start()
        {
            Level = PhaseALevels.Load(levelId);
            InitialStateHash = StateHash.Compute(Level.InitialState);
            var board = GetComponent<BoardPresenter>();
            board.Present(Level, Level.InitialState);
            var camera = Camera.main.GetComponent<FixedGameplayCamera>();
            camera.Configure(Level.InitialState.Container, Camera.main.aspect,
                board.PresentationBounds);
            var pointer = gameObject.AddComponent<PointerInteractor>();
            gameObject.AddComponent<DragPreviewPresenter>().Initialize(board, Camera.main, pointer);
        }
    }
}
