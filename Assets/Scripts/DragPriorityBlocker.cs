using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DragPriorityBlocker : MonoBehaviour
{
    // This component automatically sets a global drag lock while the object is pressed (OnMouseDown/OnMouseUp)
    // Useful for simple mouse-based dragging scripts; if your drag logic is custom you can call
    // InputLockManager.BeginDrag()/EndDrag() manually instead.

    bool isBlocking = false;

    void OnMouseDown()
    {
        InputLockManager.BeginDrag();
        isBlocking = true;
    }

    void OnMouseUp()
    {
        if (isBlocking)
        {
            InputLockManager.EndDrag();
            isBlocking = false;
        }
    }

    void OnDisable()
    {
        if (isBlocking)
        {
            InputLockManager.EndDrag();
            isBlocking = false;
        }
    }

    // Public helpers if you want to call from your own drag script
    public void StartBlocking() => InputLockManager.BeginDrag();
    public void StopBlocking() => InputLockManager.EndDrag();
}