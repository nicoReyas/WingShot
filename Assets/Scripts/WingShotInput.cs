using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// All gameplay input goes through one adapter, including projects set to Input System only.
public static class WingShotInput
{
    public static bool ScopeHeld
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }
    }

    public static Vector2 PointerDelta
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 12f;
#else
            return Vector2.zero;
#endif
        }
    }

    // Check for a connected mouse before hiding the system cursor.
    public static bool HasPointer
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePresent;
#else
            return false;
#endif
        }
    }

    // Read the mouse position in screen pixels.
    public static Vector2 PointerPosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }
    }

    // Return true only on the frame when the left mouse button is first pressed.
    public static bool ShootPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }
    }

    // Return true only on the frame when ESC is first pressed.
    public static bool PausePressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
