using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class AdvancedHandController : MonoBehaviour
{
    [Header("VR Hardware Inputs")]
    public InputActionProperty thumbAction;
    public InputActionProperty indexAction;
    public InputActionProperty middleAction;
    public InputActionProperty ringAction;
    public InputActionProperty pinkyAction;
    public InputActionProperty flipAction;

    [Header("Keyboard Overrides")]
    public Key thumbKey = Key.Digit1;
    public Key indexKey = Key.Digit2;
    public Key middleKey = Key.Digit3;
    public Key ringKey = Key.Digit4;
    public Key pinkyKey = Key.Digit5;

    [Header("Palm Orientation")]
    public Key flipKey = Key.P;
    public Vector3 flippedLocalPosition = new Vector3(0f, 0.05f, 0.1f);
    public Vector3 flippedLocalRotation = new Vector3(0f, 180f, 0f);
    [Range(0.05f, 2f)] public float flipSmoothTime = 0.25f;
    
    [Header("Controller Interaction")]
    public GameObject controllerVisual;
    public MonoBehaviour[] interactorsToDisable;

    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Internal References")]
    [SerializeField] private Animator animator;

    private int flipState;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private bool thumbToggle, indexToggle, middleToggle, ringToggle, pinkyToggle;
    private readonly float threshold = 0.05f;

    private float targetThumb, targetIndex, targetMiddle, targetRing, targetPinky;
    private float currentThumb, currentIndex, currentMiddle, currentRing, currentPinky;

    void Start()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError($"Animator not found on {gameObject.name}!", this);
            enabled = false;
        }
    }

    void Update()
    {
        HandleKeyboard();
        HandleVRInput();
        UpdatePose();

        currentThumb = Mathf.MoveTowards(currentThumb, targetThumb, smoothSpeed * Time.deltaTime);
        currentIndex = Mathf.MoveTowards(currentIndex, targetIndex, smoothSpeed * Time.deltaTime);
        currentMiddle = Mathf.MoveTowards(currentMiddle, targetMiddle, smoothSpeed * Time.deltaTime);
        currentRing = Mathf.MoveTowards(currentRing, targetRing, smoothSpeed * Time.deltaTime);
        currentPinky = Mathf.MoveTowards(currentPinky, targetPinky, smoothSpeed * Time.deltaTime);

        animator.SetFloat("Thumb", currentThumb);
        animator.SetFloat("Index", currentIndex);
        animator.SetFloat("Middle", currentMiddle);
        animator.SetFloat("Ring", currentRing);
        animator.SetFloat("Pinky", currentPinky);
    }

    private void UpdatePose()
    {
        Vector3 targetPos;
        Quaternion targetRot;

        if (flipState == 1)
        {
            targetPos = flippedLocalPosition;
            targetRot = Quaternion.Euler(flippedLocalRotation);
        }
        else if (flipState == 2)
        {
            targetPos = flippedLocalPosition;
            Vector3 rot2 = flippedLocalRotation;
            rot2.y -= 180f;
            targetRot = Quaternion.Euler(rot2);
        }
        else
        {
            targetPos = originalLocalPosition;
            targetRot = originalLocalRotation;
        }

        float step = Time.deltaTime / Mathf.Max(flipSmoothTime, 0.01f);
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPos, (targetPos - transform.localPosition).magnitude * step * 5f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, step * 5f);

        bool isFlippedAny = flipState != 0;

        if (controllerVisual != null)
        {
            bool shouldShow = !isFlippedAny;
            if (controllerVisual.activeSelf != shouldShow)
                controllerVisual.SetActive(shouldShow);
        }

        if (interactorsToDisable != null)
        {
            bool shouldEnable = !isFlippedAny;
            foreach (var interactor in interactorsToDisable)
            {
                if (interactor != null && interactor.enabled != shouldEnable)
                    interactor.enabled = shouldEnable;
            }
        }
    }

    private void HandleKeyboard()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[thumbKey].wasPressedThisFrame) thumbToggle = !thumbToggle;
        if (Keyboard.current[indexKey].wasPressedThisFrame) indexToggle = !indexToggle;
        if (Keyboard.current[middleKey].wasPressedThisFrame) middleToggle = !middleToggle;
        if (Keyboard.current[ringKey].wasPressedThisFrame) ringToggle = !ringToggle;
        if (Keyboard.current[pinkyKey].wasPressedThisFrame) pinkyToggle = !pinkyToggle;

        if (Keyboard.current[flipKey].wasPressedThisFrame)
        {
            flipState = (flipState + 1) % 3;
        }
    }

    private void HandleVRInput()
    {
        if (flipAction.action != null && flipAction.action.WasPressedThisFrame())
        {
            flipState = (flipState + 1) % 3;
        }

        float vrThumb = GetSafeVRValue(thumbAction);
        float vrIndex = GetSafeVRValue(indexAction);
        float vrMiddle = GetSafeVRValue(middleAction);
        float vrRing = GetSafeVRValue(ringAction);
        float vrPinky = GetSafeVRValue(pinkyAction);

        if (vrThumb > threshold) thumbToggle = false;
        if (vrIndex > threshold) indexToggle = false;
        if (vrMiddle > threshold) middleToggle = false;
        if (vrRing > threshold) ringToggle = false;
        if (vrPinky > threshold) pinkyToggle = false;

        targetThumb = (vrThumb > threshold) ? vrThumb : (thumbToggle ? 1f : 0f);
        targetIndex = (vrIndex > threshold) ? vrIndex : (indexToggle ? 1f : 0f);
        targetMiddle = (vrMiddle > threshold) ? vrMiddle : (middleToggle ? 1f : 0f);
        targetRing = (vrRing > threshold) ? vrRing : (ringToggle ? 1f : 0f);
        targetPinky = (vrPinky > threshold) ? vrPinky : (pinkyToggle ? 1f : 0f);
    }

    private float GetSafeVRValue(InputActionProperty property)
    {
        if (property.action != null && property.action.enabled)
        {
            return property.action.ReadValue<float>();
        }
        return 0f;
    }
}