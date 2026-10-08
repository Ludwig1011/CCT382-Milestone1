using UnityEngine;
using Unity.Cinemachine;

public class ThirdPersonController : MonoBehaviour
{
	[Header("Movement")]
	public float walkSpeed = 2.5f;
	public float runSpeed = 5.5f;
	public float rotationSpeed = 10f;

	[Header("Run Dust")]
	public ParticleSystem runDust;


	[Header("Gravity")]
	public float gravity = -20f;
	public float groundedGravity = -2f;

	[Header("Jump")]
	public float jumpHeight = 1.5f;
	public float coyoteTime = 0.12f;
	public float jumpBufferTime = 0.12f;

	[Header("Attack")]
	public float attackLockTime = 0.65f;

	[Header("Attack Audio")]
	public AudioSource attackAudioSource;
	public AudioClip swordSwingClip;

	[Header("Camera FOV")]
	public CinemachineCamera playerCamera;
	public float normalFOV = 60f;
	public float runFOV = 68f;
	public float fovLerpSpeed = 5f;

	[Header("Camera Shake")]
	public CinemachineBasicMultiChannelPerlin cameraNoise;
	public float landingShakeStrength = 1.2f;
	public float attackShakeStrength = 0.8f;
	public float shakeDuration = 0.15f;
	public float minimumLandingVelocity = -5f;

	[Header("References")]
	public Transform cameraTransform;
	public Animator animator;

	public void OnSwordSwing()
	{
		if (attackAudioSource == null || swordSwingClip == null)
			return;

		attackAudioSource.PlayOneShot(swordSwingClip);
	}

	private CharacterController controller;
	private PlayerInputActions inputActions;

	private Vector2 moveInput;

	private bool isRunning;
	private bool isAttacking;
	private bool wasGrounded;

	private float verticalVelocity;

	private float coyoteTimeCounter;
	private float jumpBufferCounter;

	private float attackTimer;
	private float shakeTimer;

	private void Awake()
	{
		controller = GetComponent<CharacterController>();
		inputActions = new PlayerInputActions();

		inputActions.Player.Move.performed += ctx =>
		{
			moveInput = ctx.ReadValue<Vector2>();
		};

		inputActions.Player.Move.canceled += ctx =>
		{
			moveInput = Vector2.zero;
		};

		inputActions.Player.Run.performed += ctx =>
		{
			isRunning = true;
		};

		inputActions.Player.Run.canceled += ctx =>
		{
			isRunning = false;
		};

		inputActions.Player.Jump.performed += ctx =>
		{
			jumpBufferCounter = jumpBufferTime;
		};

		inputActions.Player.Attack.performed += ctx =>
		{
			HandleAttack();
		};
	}

	private void OnEnable()
	{
		inputActions.Enable();
	}

	private void OnDisable()
	{
		inputActions.Disable();
	}

	private void Update()
	{
		UpdateJumpTimers();
		HandleAttackTimer();

		bool groundedNow = controller.isGrounded;

		// Detect landing before gravity resets vertical velocity.
		if (!wasGrounded &&
			groundedNow &&
			verticalVelocity <= minimumLandingVelocity)
		{
			StartCameraShake(landingShakeStrength);
		}

		wasGrounded = groundedNow;

		if (!isAttacking)
		{
			HandleJump();
			HandleMovement();
		}

		HandleGravity();

		if (animator != null)
		{
			animator.SetBool(
				"IsGrounded",
				controller.isGrounded
			);
		}

		HandleFOV();
		HandleCameraShake();

		HandleRunDust();
	}

	private void UpdateJumpTimers()
	{
		if (controller.isGrounded)
		{
			coyoteTimeCounter = coyoteTime;
		}
		else
		{
			coyoteTimeCounter -= Time.deltaTime;
		}

		if (jumpBufferCounter > 0f)
		{
			jumpBufferCounter -= Time.deltaTime;
		}
	}

	private void HandleJump()
	{
		if (jumpBufferCounter > 0f &&
			coyoteTimeCounter > 0f)
		{
			verticalVelocity = Mathf.Sqrt(
				jumpHeight * -2f * gravity
			);

			if (animator != null)
			{
				animator.SetTrigger("Jump");
			}

			jumpBufferCounter = 0f;
			coyoteTimeCounter = 0f;
		}
	}

	private void HandleMovement()
	{
		Vector3 forward = cameraTransform.forward;
		Vector3 right = cameraTransform.right;

		forward.y = 0f;
		right.y = 0f;

		forward.Normalize();
		right.Normalize();

		Vector3 moveDirection =
			forward * moveInput.y +
			right * moveInput.x;

		float currentSpeed =
			isRunning ? runSpeed : walkSpeed;

		if (moveDirection.magnitude > 0.1f)
		{
			moveDirection.Normalize();

			// 后退时不要让角色转身
			bool movingBackward = moveInput.y < -0.1f;

			if (!movingBackward)
			{
				Quaternion targetRotation =
					Quaternion.LookRotation(moveDirection);

				transform.rotation = Quaternion.Slerp(
					transform.rotation,
					targetRotation,
					rotationSpeed * Time.deltaTime
				);
			}
		}

		controller.Move(
			moveDirection *
			currentSpeed *
			Time.deltaTime
		);

		if (animator != null)
		{
			float animationSpeed = 0f;

			if (moveInput.magnitude > 0.1f)
			{
				animationSpeed =
					isRunning ? 1f : 0.5f;
			}

			animator.SetFloat(
				"Speed",
				animationSpeed
			);
		}
	}

	private void HandleGravity()
	{
		if (controller.isGrounded &&
			verticalVelocity < 0f)
		{
			verticalVelocity =
				groundedGravity;
		}
		else
		{
			verticalVelocity +=
				gravity * Time.deltaTime;
		}

		Vector3 verticalMovement =
			new Vector3(
				0f,
				verticalVelocity,
				0f
			);

		controller.Move(
			verticalMovement *
			Time.deltaTime
		);
	}

	private void HandleAttack()
	{
		if (!controller.isGrounded ||
			isAttacking)
		{
			return;
		}

		isAttacking = true;
		attackTimer = attackLockTime;

		if (animator != null)
		{
			animator.SetFloat(
				"Speed",
				0f
			);

			animator.SetTrigger(
				"Attack"
			);
		}

		StartCameraShake(
			attackShakeStrength
		);
	}

	private void HandleAttackTimer()
	{
		if (!isAttacking)
			return;

		attackTimer -= Time.deltaTime;

		if (attackTimer <= 0f)
		{
			isAttacking = false;
		}
	}

	private void HandleFOV()
	{
		if (playerCamera == null)
			return;

		float targetFOV =
			isRunning &&
			moveInput.magnitude > 0.1f
			? runFOV
			: normalFOV;

		var lens = playerCamera.Lens;

		lens.FieldOfView = Mathf.Lerp(
			lens.FieldOfView,
			targetFOV,
			fovLerpSpeed *
			Time.deltaTime
		);

		playerCamera.Lens = lens;
	}

	private void StartCameraShake(
		float strength
	)
	{
		if (cameraNoise == null)
			return;

		cameraNoise.AmplitudeGain =
			strength;

		shakeTimer =
			shakeDuration;
	}

	private void HandleCameraShake()
	{
		if (cameraNoise == null)
			return;

		if (shakeTimer > 0f)
		{
			shakeTimer -=
				Time.deltaTime;

			if (shakeTimer <= 0f)
			{
				cameraNoise.AmplitudeGain =
					0f;
			}
		}
	}

	private void HandleRunDust()
	{
		if (runDust == null)
			return;

		bool shouldPlay =
			isRunning &&
			moveInput.magnitude > 0.1f &&
			controller.isGrounded &&
			!isAttacking;

		if (shouldPlay)
		{
			if (!runDust.isPlaying)
			{
				runDust.Play();
			}
		}
		else
		{
			if (runDust.isPlaying)
			{
				runDust.Stop();
			}
		}
	}
}