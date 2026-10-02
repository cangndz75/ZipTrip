using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZipTrip.Unity
{
    // ZA-005 facilitator-only static presentation. It never reads gameplay state.
    public sealed class MG1ReadabilityPresenter : MonoBehaviour
    {
        private const float AdvanceHoldSeconds = 1.5f;
        [SerializeField] private GameObject[] items;

        private int[] _order;
        private int _position;
        private float _heldSeconds;
        private bool _advancedWhileHeld;

        public static int[] ShuffledOrder(int seed)
        {
            var order = new[] { 0, 1, 2, 3 };
            var random = new System.Random(seed);
            for (var index = order.Length - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (order[index], order[swap]) = (order[swap], order[index]);
            }
            return order;
        }

        public void Configure(GameObject[] itemInstances)
        {
            if (itemInstances == null || itemInstances.Length != 4)
                throw new ArgumentException("MG-1 requires four item instances.", nameof(itemInstances));
            items = itemInstances;
            foreach (var item in items)
                item.SetActive(false);
        }

        private void Awake()
        {
            if (items == null || items.Length != 4)
                throw new InvalidOperationException("MG-1 scene has no four-item setup.");
            var seed = DateTime.UtcNow.Ticks.GetHashCode();
            _order = ShuffledOrder(seed);
            _position = 0;
            ShowCurrent();
            Debug.Log("MG1 presentation seed=" + seed + "; item order=" +
                string.Join(",", Array.ConvertAll(_order, index => items[index].name)));
        }

        private void Update()
        {
            var held = (Touchscreen.current != null &&
                        Touchscreen.current.primaryTouch.press.isPressed) ||
                       (Mouse.current != null && Mouse.current.leftButton.isPressed);
            if (!held)
            {
                _heldSeconds = 0f;
                _advancedWhileHeld = false;
                return;
            }
            if (_advancedWhileHeld) return;
            _heldSeconds += Time.unscaledDeltaTime;
            if (_heldSeconds < AdvanceHoldSeconds) return;
            _advancedWhileHeld = true;
            _position++;
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            foreach (var item in items)
                item.SetActive(false);
            if (_position < _order.Length)
                items[_order[_position]].SetActive(true);
        }
    }
}
