using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Tooltip("Speed of player movement, in meters/second")]
    [SerializeField] private float speed = 3.5f;
    
    [Tooltip("Speed of player when sprint, in meters/second")]
    [SerializeField] private float sprintSpeedMultiplier = 2f;
    
    [Tooltip("Force of player jump")]
    [SerializeField] private float jumpForce = 1.0f;
    
    private const float gravity = 9.81f;
    private const float pullDownForce = -2f;
    private const float defaultAnimationSpeed = 1.0f;
    private const float delatToWalk = 1.0f;
    private const float minStamina = 1.0f;
    
    [SerializeField] float rotationSpeed = 180f;
    
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction jumpAction;
    [SerializeField] private InputAction sprintAction;
    [SerializeField] private InputAction attackAction;
    
    [Tooltip("Attacking animation timer")]
    [SerializeField] private float spawnTimer = 0.5f;
    
    [Tooltip("Time to wait to finish animation")]
    [SerializeField] private float wait = 0.2f;
    
    [Tooltip("Reference to Staminabar")]
    [SerializeField] private Staminabar staminabar;
    
    [Tooltip("If stamina is 0 it needs to recharge to this value when he can sprint again")]
    [SerializeField] private float sprintCooldown = 20f;
   
    [Tooltip("Reference to Healthbar")]
    [SerializeField] private Healthbar healthbar;
    
    [Tooltip("Reference to Spawner where the ball instantiates")]
    [SerializeField] private Spawner spawner;
    
    [Tooltip("How fast the animation displays")]
    [SerializeField] private float speedAnim = 0.3f;

    private CharacterController characterController; // cc reference
    private Animator animator;  // animation manager
    private Vector3 velocity;   // velocity of player
    private bool isSprinting = false;   // indicator if sprinting
    private bool canAttack = true;      // indicator if can attack
    private bool isJumpingFromStanding = false; // indicator if jump from standing

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        attackAction.Enable();

        // Register the method for the "canceled" event of sprint action
        sprintAction.canceled += OnSprintCanceled;
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
        attackAction.Disable();

        // Unregister the method for the "canceled" event of sprint action
        sprintAction.canceled -= OnSprintCanceled;
    }

    private void OnValidate()
    {
        if (moveAction == null)
            moveAction = new InputAction(type: InputActionType.Value);
        if (moveAction.bindings.Count == 0)
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

        if (jumpAction == null)
        {
            jumpAction = new InputAction(type: InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
        }

        if (sprintAction == null)
        {
            sprintAction = new InputAction(type: InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
        }

        if (attackAction == null)
        {
            attackAction = new InputAction(type: InputActionType.Button);
            attackAction.AddBinding("<Mouse>/leftButton");
        }
    }

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        staminabar = GetComponent<Staminabar>();
        if (staminabar == null)
        {
            staminabar = FindObjectOfType<Staminabar>();

            if (staminabar == null)
            {
                Debug.LogError("Staminabar not found in the scene.");
            }
        }

        if (moveAction == null)
        {
            moveAction = new InputAction(type: InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        // Enable the moveAction
        moveAction.Enable();
    }

    private void Update()
    {
        if (healthbar.GetHealth() <= 0)
        {
            // Player can't move, exit the method
            return;
        }

        // Move forward only when the forward key is pressed
        float moveHorizontal = moveAction.ReadValue<Vector2>().x;
        float moveVertical = moveAction.ReadValue<Vector2>().y;

        Vector3 moveDirection = new Vector3(moveHorizontal, 0f, moveVertical).normalized;

        // Rotate the player to face the camera direction
        if (moveDirection != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }

        // Move backward only when the backward key is pressed
        float moveBackwardInput = moveAction.ReadValue<Vector2>().y;

        // Move left and right when the left and right keys are pressed
        moveDirection = Camera.main.transform.TransformDirection(moveDirection);
        moveDirection.y = 0; // Keep the player level with the ground

        // Sprinting
        if (sprintAction.triggered && characterController.isGrounded && staminabar.GetCurrentStamina() >= sprintCooldown)
        {
            isSprinting = true;
        }

        if (moveVertical == 0 && moveBackwardInput == 0 && moveHorizontal == 0)
        {
            isSprinting = false;
        }

        // Apply sprint speed multiplier if sprinting
        float currentSpeed = isSprinting ? speed * sprintSpeedMultiplier : speed;

        // Jump when the jump key is pressed and not sprinting
        if (jumpAction.triggered && characterController.isGrounded && !isSprinting)
        {
            velocity.y = Mathf.Sqrt(2 * jumpForce * gravity);
            animator.SetBool("isJumping", !characterController.isGrounded);

            isJumpingFromStanding = Mathf.Abs(moveVertical + moveHorizontal + moveBackwardInput) < 0.1f;
            if (isJumpingFromStanding)
                moveAction.Disable();
            StartCoroutine(EnableMoveActionAfterDelay(delatToWalk));
        }

        // Attack when the attack key is pressed
        if (attackAction.triggered && canAttack)
        {
            if (characterController.isGrounded) StartCoroutine(AttackAnimation());
        }

        // Apply gravity
        velocity.y -= gravity * Time.deltaTime;

        // Move the character and set isRunning and isSprinting animation parameters
        characterController.Move((moveDirection * currentSpeed + velocity) * Time.deltaTime);

        // Set isRunning and isJumping animation parameters
        bool isRunning = Mathf.Abs(moveVertical + moveBackwardInput + moveHorizontal) > 0.1f;
        animator.SetBool("isRunning", isRunning);
        animator.SetBool("isJumping", !characterController.isGrounded);
        animator.SetBool("isSprinting", isSprinting);
        animator.SetBool("isWalkingBack", moveVertical < 0);
        bool isWalkingRight = moveHorizontal > 0 && moveVertical == 0;
        bool isWalkingLeft = moveHorizontal < 0 && moveVertical == 0;
        animator.SetBool("isMovingRight", isWalkingRight);
        animator.SetBool("isMovingLeft", isWalkingLeft);

        // Clamp the character to the ground
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = pullDownForce;
            animator.SetBool("isJumping", false);
        }
    }

    IEnumerator AttackAnimation()
    {
        canAttack = false;
        if (characterController.isGrounded && !animator.GetBool("isJumping"))
        {
            animator.SetBool("isAttacking", true);
            moveAction.Disable();
            animator.speed = defaultAnimationSpeed / speedAnim;

            spawner.SpawnFireball();

            yield return new WaitForSeconds(spawnTimer);

            animator.SetBool("isAttacking", false);
            animator.speed = defaultAnimationSpeed;
            moveAction.Enable();
            yield return new WaitForSeconds(wait);
        }
        canAttack = true;
    }

    IEnumerator EnableMoveActionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Re-enable moveAction after the specified delay
        if (!moveAction.enabled)
            moveAction.Enable();
    }

    private void LateUpdate()
    {
        // Check if stamina is below a certain threshold and stop sprinting
        if (staminabar.GetCurrentStamina() < minStamina)
        {
            StopSprinting();
        }
    }

    // Method to handle the "canceled" event for sprint action
    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprinting = false;
    }

    public bool IsSprinting()
    {
        return isSprinting;
    }

    private void StopSprinting()
    {
        // Stop sprinting logic goes here
        isSprinting = false;
    }
}