using FPMovement;
using UnityEngine;

public class CursorManager : MonoBehaviour
{
    private void Start()
    {
        SetLocked();
    }

    public enum CursorState
    {
        Gameplay,
        UI,
    }

    public static void SetState(CursorState state)
    {
        Debug.Log("Cursor state set to: " + state);
        switch (state)
        {
            case CursorState.Gameplay:
                SetLocked();
                break;

            case CursorState.UI:
                SetVisible();
                break;
        }
    }

    public static void SetLocked()
    {
        Camera.main.transform.parent.parent.parent.GetComponent<MouseLookController>().enableLook = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void SetVisible()
    {
        Camera.main.transform.parent.parent.parent.GetComponent<MouseLookController>().enableLook = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
