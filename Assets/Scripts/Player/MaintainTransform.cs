using UnityEngine;

public class MaintainTransform : MonoBehaviour
{
    [SerializeField] public bool useWorldSpace = true;

    [SerializeField] public Vector3 targetPosition;
    [SerializeField] public Vector3 targetRotation;
    [SerializeField] public Vector3 targetScale = Vector3.one;

    [Header("Position Targets")]
    public bool targetPosX;
    public bool targetPosY;
    public bool targetPosZ;

    [Header("Rotation Targets")]
    public bool targetRotX;
    public bool targetRotY;
    public bool targetRotZ;

    [Header("Scale Targets")]
    public bool targetScaleX;
    public bool targetScaleY;
    public bool targetScaleZ;

    private const float AngleTolerance = 0.001f;

    private void Reset()
    {
        if (useWorldSpace)
        {
            targetPosition = transform.position;
            targetRotation = transform.eulerAngles;
        }
        else
        {
            targetPosition = transform.localPosition;
            targetRotation = transform.localEulerAngles;
        }
        targetScale = transform.localScale;
    }

    private void LateUpdate()
    {
        MaintainPosition();
        MaintainRotation();
        MaintainScale();
    }

    public void MaintainPosition()
    {
        if (!targetPosX && !targetPosY && !targetPosZ) return;

        Vector3 currentPos = useWorldSpace ? transform.position : transform.localPosition;
        Vector3 newPos = currentPos;
        bool changed = false;

        if (targetPosX && !Mathf.Approximately(currentPos.x, targetPosition.x))
        {
            newPos.x = targetPosition.x;
            changed = true;
        }
        if (targetPosY && !Mathf.Approximately(currentPos.y, targetPosition.y))
        {
            newPos.y = targetPosition.y;
            changed = true;
        }
        if (targetPosZ && !Mathf.Approximately(currentPos.z, targetPosition.z))
        {
            newPos.z = targetPosition.z;
            changed = true;
        }

        if (changed)
        {
            if (useWorldSpace)
                transform.position = newPos;
            else
                transform.localPosition = newPos;
        }
    }

    public void MaintainRotation()
    {
        if (!targetRotX && !targetRotY && !targetRotZ) return;

        Vector3 currentRot = useWorldSpace ? transform.eulerAngles : transform.localEulerAngles;
        Vector3 newRot = currentRot;
        bool changed = false;

        if (targetRotX && Mathf.Abs(Mathf.DeltaAngle(currentRot.x, targetRotation.x)) > AngleTolerance)
        {
            newRot.x = targetRotation.x;
            changed = true;
        }
        if (targetRotY && Mathf.Abs(Mathf.DeltaAngle(currentRot.y, targetRotation.y)) > AngleTolerance)
        {
            newRot.y = targetRotation.y;
            changed = true;
        }
        if (targetRotZ && Mathf.Abs(Mathf.DeltaAngle(currentRot.z, targetRotation.z)) > AngleTolerance)
        {
            newRot.z = targetRotation.z;
            changed = true;
        }

        if (changed)
        {
            if (useWorldSpace)
                transform.eulerAngles = newRot;
            else
                transform.localEulerAngles = newRot;
        }
    }

    public void MaintainScale()
    {
        if (!targetScaleX && !targetScaleY && !targetScaleZ) return;

        Vector3 currentScale = transform.localScale;
        Vector3 newScale = currentScale;
        bool changed = false;

        if (targetScaleX && !Mathf.Approximately(currentScale.x, targetScale.x))
        {
            newScale.x = targetScale.x;
            changed = true;
        }
        if (targetScaleY && !Mathf.Approximately(currentScale.y, targetScale.y))
        {
            newScale.y = targetScale.y;
            changed = true;
        }
        if (targetScaleZ && !Mathf.Approximately(currentScale.z, targetScale.z))
        {
            newScale.z = targetScale.z;
            changed = true;
        }

        if (changed)
        {
            transform.localScale = newScale;
        }
    }
}


