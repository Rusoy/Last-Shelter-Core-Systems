using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2.0f;
    [SerializeField] private float inputSmoothSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float sprintThreshold = 0.9f;

    [Header("Jump Parameters")]
    [SerializeField] private float gravity = 25f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("Look Settings")]
    [SerializeField] private bool useBuiltInLook = false; // Set false when using MobileLook
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float upDownRange = 80.0f;

    [Header("Input Actions")]
    [SerializeField] public InputActionAsset PlayerControls;

    [Header("Footstep Sounds")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float walkStepInterval = 0.5f;
    [SerializeField] private float sprintStepInterval = 0.3f;
    [SerializeField] private float velocityThreshold = 2.0f;

    // States
    public bool isMoving { get; private set; }
    public bool isRunning { get; private set; }
    public bool isGrounded => characterController.isGrounded;
    public bool isDancing { get; private set; }
    public bool isKicking { get; private set; }
    public bool isPicking { get; private set; }

    public bool Idle => !isMoving;

    private float nextStepTime;
    private Camera mainCamera;
    private float verticalRotation;
    private Vector3 currentMovement = Vector3.zero;
    private CharacterController characterController;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction lookAction;
    private InputAction danceAction;

    private Vector2 moveInput;
    private Vector2 smoothInputVector;
    private Vector2 lookInput;

    private Animator playerAnimator;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerAnimator = GetComponent<Animator>();
        mainCamera = Camera.main;

        var map = PlayerControls.FindActionMap("PlayerControlsMaps");
        if (map == null)
        {
#if UNITY_EDITOR
            Debug.LogError("Action map 'PlayerControlsMaps' not found!");
#endif
            return;
        }

        moveAction = map.FindAction("Move");
        jumpAction = map.FindAction("Jump");
        lookAction = map.FindAction("Look");
        sprintAction = map.FindAction("Sprint");
        danceAction = map.FindAction("Dance");

        moveAction.performed += context => moveInput = context.ReadValue<Vector2>();
        moveAction.canceled += context => moveInput = Vector2.zero;

        lookAction.performed += context => lookInput = context.ReadValue<Vector2>();
        lookAction.canceled += context => lookInput = Vector2.zero;

        danceAction.performed += ctx => SetDance(true);
        danceAction.canceled += ctx => SetDance(false);
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
        lookAction?.Enable();
        danceAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
        sprintAction?.Disable();
        lookAction?.Disable();
        danceAction?.Disable();
    }

    private void Update()
    {
        if (isDancing) return;
        if (isKicking) return;
        if (isPicking) return;

        MovePlayer();
    }

    private void MovePlayer()
    {
        // Smooth raw input to avoid snappy direction changes
        smoothInputVector = Vector2.Lerp(smoothInputVector, moveInput, Time.deltaTime * inputSmoothSpeed);
        float inputMagnitude = smoothInputVector.magnitude;

        // Hysteresis prevents rapid idle/moving flicker at the threshold boundary
        if (isMoving) isMoving = inputMagnitude > 0.10f;
        else isMoving = inputMagnitude > 0.20f;

        bool sprintInput = sprintAction.ReadValue<float>() > 0.5f;
        isRunning = isMoving && (inputMagnitude >= sprintThreshold || sprintInput);

        HandleMovement();

        if (useBuiltInLook)
            HandleRotation();

        HandleFootSteps();
    }

    private void HandleMovement()
    {
        float speedMultiplierCalc = isRunning ? sprintMultiplier : 1f;
        float currentSpeed = walkSpeed * speedMultiplierCalc;

        float currentSpeedX = smoothInputVector.x * currentSpeed;
        float currentSpeedY = smoothInputVector.y * currentSpeed;

        Vector3 horizontalMovement = new Vector3(currentSpeedX, 0, currentSpeedY);
        horizontalMovement = transform.rotation * horizontalMovement;

        currentMovement.x = horizontalMovement.x;
        currentMovement.z = horizontalMovement.z;

        HandleGravityAndJumping();

        characterController.Move(currentMovement * Time.deltaTime);
    }

    private void HandleGravityAndJumping()
    {
        if (characterController.isGrounded)
        {
            // A small constant downward force keeps CharacterController.isGrounded
            // reliably true on flat surfaces; without it, the grounded check flickers.
            currentMovement.y = -2.5f;

            if (jumpAction.triggered)
            {
                // v = sqrt(h * 2 * g)
                currentMovement.y = Mathf.Sqrt(jumpHeight * 2f * gravity);
                if (playerAnimator != null) playerAnimator.SetTrigger("jump");
            }
        }
        else
        {
            currentMovement.y -= gravity * Time.deltaTime;
        }
    }

    private void HandleRotation()
    {
        if (mainCamera == null) return;

        float mouseXRotation = lookInput.x * mouseSensitivity;
        transform.Rotate(0, mouseXRotation, 0);

        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);

        mainCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    void HandleFootSteps()
    {
        if (!characterController.isGrounded || !isMoving || characterController.velocity.magnitude < velocityThreshold)
            return;

        float currentStepInterval = isRunning ? sprintStepInterval : walkStepInterval;

        if (Time.time > nextStepTime)
        {
            PlayFootstepSound();
            nextStepTime = Time.time + currentStepInterval;
        }
    }

    void PlayFootstepSound()
    {
        if (footstepSource != null && footstepClips.Length > 0)
        {
            int randomIndex = Random.Range(0, footstepClips.Length);
            footstepSource.PlayOneShot(footstepClips[randomIndex]);
        }
    }

    public void LootKick()
    {
        if (isKicking) return;
        isKicking = true;

        smoothInputVector = Vector2.zero; // Prevent slide during kick
        playerAnimator.Play("Kick", 1, 0f);
        Invoke(nameof(OnKickEnd), 1.2f);
    }

    // Called by animation event when the kick finishes
    public void OnKickEnd()
    {
        isKicking = false;
        CancelInvoke(nameof(OnKickEnd));
    }

    public void StartPick(Transform targetItem)
    {
        if (isPicking) return;
        isPicking = true;

        smoothInputVector = Vector2.zero;

        // Choose pick animation based on item height relative to character feet
        float heightDifference = targetItem.position.y - transform.position.y;
        if (heightDifference > 0.8f)
            playerAnimator.Play("PickHigh");
        else
            playerAnimator.Play("PickLow");

        Invoke(nameof(OnPickEnd), 1.5f);
    }

    // Called by animation event at the end of the pick animation
    public void OnPickEnd()
    {
        isPicking = false;
        CancelInvoke(nameof(OnPickEnd));
    }

    private void SetDance(bool active)
    {
        isDancing = active;
        if (playerAnimator) playerAnimator.SetBool("dance", active);
    }
}
