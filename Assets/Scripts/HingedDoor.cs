using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Door that is moved by its handle. Put this on the hinge pivot (the door panel extends along
/// the pivot's local +X when closed). When released it keeps swinging with the speed it was
/// moving at, slows down from friction, and latches shut only if it swings into the frame.
/// </summary>
public class HingedDoor : MonoBehaviour
{
    [SerializeField] private XRBaseInteractable handle;
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 100f;

    [Header("Inertia")]
    [Tooltip("How quickly a released door slows down, in degrees per second squared.")]
    [SerializeField] private float friction = 90f;
    [Tooltip("Fastest the door can swing after being let go, in degrees per second.")]
    [SerializeField] private float maxSpeed = 360f;
    [Tooltip("Fraction of speed kept when the door hits the fully-open stop.")]
    [Range(0f, 1f)] [SerializeField] private float stopBounce = 0.2f;
    [Tooltip("A door swinging shut latches once it gets this close to the frame.")]
    [SerializeField] private float latchAngle = 3f;

    private Quaternion closedRotation;
    private IXRSelectInteractor holder;
    private float grabOffset;

    public float Angle { get; private set; }
    public float Velocity { get; private set; }
    public bool IsHeld => holder != null;
    public bool IsLatched => Angle == 0f && Velocity == 0f;

    private void Awake()
    {
        closedRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (handle == null) return;
        handle.selectEntered.AddListener(OnGrab);
        handle.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        if (handle == null) return;
        handle.selectEntered.RemoveListener(OnGrab);
        handle.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        grabOffset = Angle - AngleTowards(HolderPosition());
        Velocity = 0f;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject != holder) return;
        holder = null;
        // Velocity keeps whatever the hand was doing; Update carries the swing on from here.
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (holder != null)
        {
            float previous = Angle;
            SetAngle(AngleTowards(HolderPosition()) + grabOffset);
            // Smoothed so a single jittery frame doesn't decide the throw speed.
            float measured = Mathf.Clamp((Angle - previous) / dt, -maxSpeed, maxSpeed);
            Velocity = Mathf.Lerp(Velocity, measured, 0.5f);
            return;
        }

        if (Velocity == 0f) return;

        float next = Angle + Velocity * dt;
        Velocity = Mathf.MoveTowards(Velocity, 0f, friction * dt);

        if (Velocity <= 0f && next <= minAngle + latchAngle)
        {
            // Swung into the frame: latch shut.
            Velocity = 0f;
            SetAngle(minAngle);
        }
        else if (next >= maxAngle)
        {
            Velocity = -Mathf.Abs(Velocity) * stopBounce;
            SetAngle(maxAngle);
        }
        else
        {
            SetAngle(next);
        }
    }

    private Vector3 HolderPosition() => holder.GetAttachTransform(handle).position;

    /// <summary>Angle around the hinge, measured from the closed position, pointing at a world position.</summary>
    public float AngleTowards(Vector3 worldPoint)
    {
        Vector3 local = transform.parent != null ? transform.parent.InverseTransformPoint(worldPoint) : worldPoint;
        Vector3 offset = local - transform.localPosition;
        offset.y = 0f;
        return Vector3.SignedAngle(closedRotation * Vector3.right, offset, Vector3.up);
    }

    public void SetAngle(float angle)
    {
        Angle = Mathf.Clamp(angle, minAngle, maxAngle);
        transform.localRotation = Quaternion.AngleAxis(Angle, Vector3.up) * closedRotation;
    }
}
