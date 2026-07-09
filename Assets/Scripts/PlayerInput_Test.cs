using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput_Test : MonoBehaviour
{
    private void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            CameraController.Instance.StartPan(Mouse.current.position.ReadValue());
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            CameraController.Instance.EndPan();
        }
    }
}
