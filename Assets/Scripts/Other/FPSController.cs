using UnityEngine;

public class FPSController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Speed of the player when walking on the ground.")]
    public float walkSpeed = 5.0f;
    [Tooltip("Speed of the player when flying.")]
    public float flySpeed = 10.0f;
    [Tooltip("Speed of the player when flying and holding Shift (boost).")] // New
    public float flySprintSpeed = 20.0f; // New
    [Tooltip("Gravity applied when not flying.")]
    public float gravity = -9.81f; // Standard gravity value

    [Header("Look Settings")]
    [Tooltip("Sensitivity of mouse input for looking around.")]
    public float lookSensitivity = 2.0f;
    [Tooltip("Minimum vertical angle the camera can look (clamped).")]
    public float minLookAngle = -90.0f; // Look straight down
    [Tooltip("Maximum vertical angle the camera can look (clamped).")]
    public float maxLookAngle = 90.0f;  // Look straight up

    // Private variables
    private CharacterController characterController;
    private Camera playerCamera;
    private float rotationX = 0; // Current vertical rotation of the camera
    private Vector3 velocity; // Current velocity for gravity
    private bool isFlying = false; // Flag to determine if the player is in flight mode

    void Awake()
    {
        // Get the CharacterController component attached to this GameObject.
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogError("FPSController requires a CharacterController component!");
            enabled = false; // Disable the script if no CharacterController is found
            return;
        }

        // Find the main camera component.
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            // Fallback to Camera.main if not found as a child
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogError("No camera found! Please assign a camera or tag your camera as 'MainCamera'.");
                enabled = false;
                return;
            }
            Debug.LogWarning("FPSController: Using Camera.main. Consider making the camera a child of the player object for optimal behaviour.");
        }


        // Lock and hide the cursor to keep it in the center of the screen
        // and prevent it from interfering with mouse look.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Handle mouse look input
        HandleLook();

        // Toggle flight mode with the 'F' key
        if (Input.GetKeyDown(KeyCode.F))
        {
            isFlying = !isFlying;
            velocity.y = 0;
        }

        // Handle movement based on current mode (walking or flying)
        if (isFlying)
        {
            HandleFlyMovement();
        }
        else
        {
            HandleWalkMovement();
        }
    }

    // Handles camera rotation based on mouse input.
    void HandleLook()
    {
        // Get mouse input for horizontal and vertical rotation.
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        // Apply horizontal rotation to the player's body (transform).
        transform.Rotate(Vector3.up * mouseX);

        // Apply vertical rotation to the camera.
        rotationX -= mouseY;
        // Clamp the vertical rotation to prevent the camera from flipping over.
        rotationX = Mathf.Clamp(rotationX, minLookAngle, maxLookAngle);

        // Set the camera's local rotation.
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
    }

    // Handles player movement when walking on the ground.
    void HandleWalkMovement()
    {
        // Check if the character is grounded. If not, apply gravity.
        if (characterController.isGrounded)
        {
            // Reset vertical velocity if grounded and it's positive (e.g., after a small hop)
            // or keep it slightly negative to stick to the ground.
            if (velocity.y > 0) velocity.y = -0.5f;
            else velocity.y = -0.5f; // Small negative value to ensure it stays grounded
        }
        else
        {
            // Apply gravity over time.
            velocity.y += gravity * Time.deltaTime;
        }

        // Get input for forward/backward and strafing movement.
        float moveX = Input.GetAxis("Horizontal"); // A/D keys
        float moveZ = Input.GetAxis("Vertical");   // W/S keys

        // Calculate movement direction relative to the player's forward direction.
        Vector3 move = transform.right * moveX + transform.forward * moveZ;

        if (move.magnitude > 1)
        {
            move.Normalize();
        }

        // Apply movement using the CharacterController.
        characterController.Move(move * walkSpeed * Time.deltaTime + velocity * Time.deltaTime);
    }

    // Handles player movement when flying.
    void HandleFlyMovement()
    {
        // Get input for horizontal, vertical, and depth movement.
        float moveX = Input.GetAxis("Horizontal"); // A/D keys
        float moveZ = Input.GetAxis("Vertical");   // W/S keys
        float moveY = 0; // Up/Down movement

        // Check for 'E' key for upward movement and 'Q' key for downward movement.
        if (Input.GetKey(KeyCode.E))
        {
            moveY = 1;
        }
        else if (Input.GetKey(KeyCode.Q))
        {
            moveY = -1;
        }

        // Calculate movement direction relative to the camera's orientation.
        Vector3 moveDirection = playerCamera.transform.right * moveX +
                                playerCamera.transform.forward * moveZ +
                                playerCamera.transform.up * moveY;

        // Normalize to prevent faster diagonal movement *if* magnitude is greater than 1
        if (moveDirection.magnitude > 1)
        {
            moveDirection.Normalize();
        }

        // Determine current speed based on whether Shift is pressed
        float currentFlySpeed = flySpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) // Check for Left or Right Shift
        {
            currentFlySpeed = flySprintSpeed;
        }

        transform.position += moveDirection * currentFlySpeed * Time.deltaTime;
    }

    // Ensures the cursor is locked and hidden when the application gains focus.
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

    }
}