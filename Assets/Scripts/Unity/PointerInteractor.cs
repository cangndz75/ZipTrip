using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace ZipTrip.Unity
{
    public enum PointerPhase
    {
        Down,
        Move,
        Up,
        Cancel
    }

    public readonly struct PointerSignal
    {
        public PointerPhase Phase { get; }
        public Vector2 ScreenPosition { get; }

        public PointerSignal(PointerPhase phase, Vector2 screenPosition)
        {
            Phase = phase;
            ScreenPosition = screenPosition;
        }
    }

    // Polls the Input System and exposes one source-independent pointer flow.
    public sealed class PointerInteractor : MonoBehaviour
    {
        private enum Owner { None, Mouse, Touch }

        private Owner _owner;
        private Mouse _ownerMouse;
        private Touchscreen _ownerScreen;
        private int _ownerTouchId;
        private Vector2 _lastPosition;
        private bool _mouseWasPressed;

        public event Action<PointerSignal> PointerEvent;
        public bool HasActivePointer => _owner != Owner.None;

        private void Update() => Poll();

        // Public so simulated Input System tests can advance one input sample deterministically.
        public void Poll()
        {
            var mouse = Mouse.current;
            var mousePressed = mouse != null && mouse.leftButton.isPressed;
            var mouseDown = mousePressed && !_mouseWasPressed;
            _mouseWasPressed = mousePressed;

            if (_owner == Owner.Mouse)
            {
                if (mouse != _ownerMouse)
                    Release(PointerPhase.Cancel, _lastPosition);
                else if (!mousePressed)
                    Release(PointerPhase.Up, mouse.position.ReadValue());
                else
                    MoveIfChanged(mouse.position.ReadValue());
                return;
            }

            if (_owner == Owner.Touch)
            {
                if (_ownerScreen == null || !_ownerScreen.added ||
                    !TryFindTouch(_ownerScreen, _ownerTouchId, out var ownedTouch))
                {
                    Release(PointerPhase.Cancel, _lastPosition);
                    return;
                }

                var position = ownedTouch.position.ReadValue();
                var phase = ownedTouch.phase.ReadValue();
                if (phase == InputTouchPhase.Canceled)
                    Release(PointerPhase.Cancel, position);
                else if (phase == InputTouchPhase.Ended)
                    Release(PointerPhase.Up, position);
                else
                    MoveIfChanged(position);
                return;
            }

            var screen = Touchscreen.current;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (touch.phase.ReadValue() != InputTouchPhase.Began)
                        continue;
                    _owner = Owner.Touch;
                    _ownerScreen = screen;
                    _ownerTouchId = touch.touchId.ReadValue();
                    _lastPosition = touch.position.ReadValue();
                    PointerEvent?.Invoke(new PointerSignal(PointerPhase.Down, _lastPosition));
                    return;
                }
            }

            if (mouseDown)
            {
                _owner = Owner.Mouse;
                _ownerMouse = mouse;
                _lastPosition = mouse.position.ReadValue();
                PointerEvent?.Invoke(new PointerSignal(PointerPhase.Down, _lastPosition));
            }
        }

        private static bool TryFindTouch(Touchscreen screen, int id, out TouchControl result)
        {
            foreach (var touch in screen.touches)
            {
                if (touch.touchId.ReadValue() != id || touch.phase.ReadValue() == InputTouchPhase.None)
                    continue;
                result = touch;
                return true;
            }
            result = null;
            return false;
        }

        private void MoveIfChanged(Vector2 position)
        {
            if (position == _lastPosition)
                return;
            _lastPosition = position;
            PointerEvent?.Invoke(new PointerSignal(PointerPhase.Move, position));
        }

        private void Release(PointerPhase phase, Vector2 position)
        {
            _owner = Owner.None;
            _ownerMouse = null;
            _ownerScreen = null;
            _ownerTouchId = 0;
            _lastPosition = position;
            PointerEvent?.Invoke(new PointerSignal(phase, position));
        }

        private void OnDisable()
        {
            if (HasActivePointer)
                Release(PointerPhase.Cancel, _lastPosition);
        }
    }
}
