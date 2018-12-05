
/*
 * Bird Player Controller
 * 
 * This player contoller is for birds that are children of a player group (or "flock"). The player group 
 * follows a flight path, determined by a spline. Each bird controlled by a player may move up/down/left/right 
 * relative to the player group (or camera, which is static and attached to the player group). Due to this 
 * behavior, the flight path's spline must not have sharp turns. Since player-controlled bird movement is relative 
 * to the plane of the camera, the camera cannot turn a large amount between frames, or it will seem very different 
 * from world space, and the movement will not seem realistic.
 * 
 * The bird movement and movement controls are simplified.
 * Lift occurs at two fixed points on the left and right side of the bird where its wings would be when fully 
 * extended. The amount of lift created at each point depends on how much that wing is extended outward (and the 
 * bird's speed, of course).
 * Upward force is created at a lift point when the corresponding wing is quickly moved downward. The same effect 
 * happens when the wing is moved wuickly upward, but dampened.
 * 
 * Bird object settings (for the object to which this controller is attached):
 * The bird should have medium drag, about 0.5 translational, 3 rotational.
 * The bird's coordinate axes should be unmodified. The forward vector faces away from the camera, the right 
 * vector faces right, etc.
 * 
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {

    /*  TODO
     * Generate lift based on bird's forward speed and the amount its wings are extended
     * Take velocity vector from last frame and rotate it so it is always relative to camera
     * Rotation is lockat right now. Allow some rotation for realistic movement.
     * As above, for all constants; use dampening instead of setting constants for realistic movement.
     *   
     * Misc unrelated:
     * Slight wind particle effect, intensity and speed based on group's speed
     * Slight wind trail effect
     * Download (free) terrain
     * Integrate bird animations
     * 
     */

    /// External References
    // Reference to the rigid body of the bird
    private Rigidbody rb;
    // Velocity object of the player group
    private VelocityFromPosition group;
	// Reference to the component that obtains motion tracking info
	private SensorInterface tracker;

    /// Parameters: Scalars
    // Thrust caused by flapping wings
    public float flapThrustScale = 0.5f;
    // How much lift is generated
    public float liftScale = 0.4f;
    // How much vertical drag is generated
    public float vertDragScale = 1f;
    // Torque dampening factor (also edit rotational drag)
    public Vector3 torqueDampenPosFactor = new Vector3(0.02f, 0.02f, 0.02f);

    /// Parameters: Child References
    // Left/right lift point
    private Transform leftLiftPoint;
    private Transform rightLiftPoint;

	/// Paramters: Debug
	// Whether to apply lift
	public bool applyLift = true;

    /// Control State
    // Amount left/right wing is extended (0 - 1, 0 = against body, 1 = horizontal)
    private float leftWingExtended = 1;
    private float rightWingExtended = 1;
    // Total lift under left/right wing
    private float leftWingForce = 0;
    private float rightWingForce = 0;
	// Past tracker angle values
	private float pLeftArmAngle = 0, pRightArmAngle = 0;

	/// Object State
    // Rotation, read-only (0 upright, increses negative one direction, positive the other) (-180 to 180 rather than 0 to 360)
    private Vector3 rotation = new Vector3(0, 0, 0);
	// Base velocity (from the flight group) of the bird in local coordinates. Does not include local velocity due to physics.
	private Vector3 localBaseVelocity = new Vector3(0, 0, 0);

    // Initialization
    void Start() {
        // Get bird RigidBody (physics body)
        rb = GetComponent<Rigidbody>();
        // Get left and right lift points
        leftLiftPoint = transform.GetChild(0);
        rightLiftPoint = transform.GetChild(1);
        // Get the group (parent) velocity reference
        group = rb.transform.parent.GetComponent<VelocityFromPosition>();
		// Get the tracker / reference to sensor info
		tracker = GameObject.Find("Tracker").GetComponent<SensorInterface>();
    }

    // Update is called once per frame.
    void Update() {
        /// DEBUG Vectors
        // Forward vector
        Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 5, Color.green);
        // Left wing lift
        Debug.DrawRay(leftLiftPoint.position, -leftLiftPoint.up * 0.5f * leftWingForce, Color.blue);
        // Right wing lift
        Debug.DrawRay(rightLiftPoint.position, -rightLiftPoint.up * 0.5f * rightWingForce, Color.blue);
        // Velocity vectors
        Vector3 vel = rb.GetPointVelocity(leftLiftPoint.TransformPoint(leftLiftPoint.position));
        vel = rb.transform.InverseTransformDirection(vel); // (local space)
        vel = rb.velocity;
        Debug.DrawRay(new Vector3(rb.transform.position.x, rb.transform.position.y + 2, rb.transform.position.z), rb.transform.right * vel.x * 0.2f, Color.red);
        Debug.DrawRay(new Vector3(rb.transform.position.x, rb.transform.position.y + 2, rb.transform.position.z), rb.transform.up * vel.y * 0.2f, Color.green);
        Debug.DrawRay(new Vector3(rb.transform.position.x, rb.transform.position.y + 2, rb.transform.position.z), rb.transform.forward * vel.z * 0.2f, Color.blue);
    }

    // FixedUpdate is called after a fixed amount of time (good for adding forces).
    void FixedUpdate() {
        /// Get Physical State
		// Get rotation, constrin -180 to 180
        rotation.x = rb.transform.localRotation.eulerAngles.x < 180 ? rb.transform.localRotation.eulerAngles.x : rb.transform.localRotation.eulerAngles.x - 360;
        rotation.y = rb.transform.localRotation.eulerAngles.y < 180 ? rb.transform.localRotation.eulerAngles.y : rb.transform.localRotation.eulerAngles.y - 360;
        rotation.z = rb.transform.localRotation.eulerAngles.z < 180 ? rb.transform.localRotation.eulerAngles.z : rb.transform.localRotation.eulerAngles.z - 360;
		// Get base velocity in local coordinates
		localBaseVelocity = transform.InverseTransformDirection(group.velocity);

        /// DEBUG Controls for Keyboard
        // Add constant force to bird
        if (Input.GetKey("up")) {
            rb.AddRelativeForce(Vector3.up * flapThrustScale);
        }
        if (Input.GetKey("left")) {
            rb.AddRelativeForce(Vector3.left * flapThrustScale);
        }
        if (Input.GetKey("right")) {
            rb.AddRelativeForce(Vector3.right * flapThrustScale);
        }
        if (Input.GetKey("down")) {
            rb.AddRelativeForce(Vector3.down * flapThrustScale);
        }
        // Control wing extension
        if (Input.GetKey("e")) {
            leftWingExtended += 0.05f;
        }
        if (Input.GetKey("d")) {
            leftWingExtended -= 0.05f;
        }
        if (Input.GetKey("r")) {
            rightWingExtended += 0.05f;
        }
        if (Input.GetKey("f")) {
            rightWingExtended -= 0.05f;
        }
        // Add constant torque to bird
        if (Input.GetKey("q")) {
            rb.AddRelativeTorque(Vector3.forward * 5);
        }
        if (Input.GetKey("w")) {
            rb.AddRelativeTorque(Vector3.forward * -5);
        }

        /// Input Control
		// Set wing extension based on arm angle
		leftWingExtended = Mathf.Cos(Mathf.PI * tracker.data.leftArmAngle / 180);
		rightWingExtended = Mathf.Cos(Mathf.PI * tracker.data.rightArmAngle / 180);

        // Clamp wing extension 0 - 1
        leftWingExtended = Mathf.Clamp(leftWingExtended, 0, 1);
        rightWingExtended = Mathf.Clamp(rightWingExtended, 0, 1);

        /// Lift Generation
        // Reset lift at beginning of update
        leftWingForce = 0;
        rightWingForce = 0;

		// Generate lift based on bird's forward speed, downward speed, and the amount its wings are extended
		// (per-wing basis, so rotation matters)
			// Left
				// Velocity at left lift point
				Vector3 leftVel = /*rb.GetPointVelocity(leftLiftPoint.TransformPoint(leftLiftPoint.position)) +*/ group.velocity;
				leftVel = rb.transform.InverseTransformDirection(leftVel); // (local space)
                
				// Counteract vertical velocity
				float leftDrag = leftVel.y;
				// Have a greater effect if falling downward
				if (leftDrag < 0) leftDrag *= -2f;
				else leftDrag*= -0.5f;

				// Add lift if going forward quickly
				float leftLift = leftVel.z;
				if (leftLift < 0) leftLift = 0;
            // Right
				// Velocity at right lift point
				Vector3 rightVel = /*rb.GetPointVelocity(rightLiftPoint.TransformPoint(rightLiftPoint.position)) +*/ group.velocity;
				rightVel = rb.transform.InverseTransformDirection(rightVel); // (local space)

				// Counteract vertical velocity
				float rightDrag = rightVel.y;
				// Have a greater effect if falling downward
				if (rightDrag < 0) rightDrag *= -2f;
				else rightDrag *= -0.5f;

				// Add lift if going forward quickly
				float rightLift = rightVel.z;
				if (rightLift < 0) rightLift = 0;
			// Apply the lift
			if (applyLift) {
				leftWingForce += leftWingExtended * (leftDrag*vertDragScale*0 + leftLift*liftScale);
                rightWingForce += rightWingExtended * (rightDrag*vertDragScale*0 + rightLift*liftScale);
			}
			/// DEBUG - remove
			print(leftWingExtended);

        /// Input Control - Wing Thrust
        // Generate thrust based on flapping wings.
		if (pLeftArmAngle - tracker.data.leftArmAngle > 0)
			leftWingForce += flapThrustScale * (pLeftArmAngle - tracker.data.leftArmAngle);
		if (pRightArmAngle - tracker.data.rightArmAngle > 0)
			rightWingForce += flapThrustScale * (pRightArmAngle - tracker.data.rightArmAngle);

        /// Rotational Dampening
        // Nudge bird rotation so it stays upright when no other forces are applied
        rb.AddRelativeTorque(Vector3.forward * -rotation.z * torqueDampenPosFactor.z); // z (roll)
        rb.AddRelativeTorque(Vector3.right * -rotation.x  * torqueDampenPosFactor.x); // x (pitch)
        rb.AddRelativeTorque(Vector3.up * -rotation.y  * torqueDampenPosFactor.y); // y (yaw)
        
        /// Move the Bird
        // Apply forces based on lift at left/right wing
		AddRelativeForceAtPosition(new Vector3(0, leftWingForce, 0), leftLiftPoint.position);
		AddRelativeForceAtPosition(new Vector3(0, rightWingForce, 0), rightLiftPoint.position);

        // Keep depth (distance from camera) fixed; prevent drift in forward / backward direction
        rb.transform.localPosition = new Vector3(rb.transform.localPosition.x,rb.transform.localPosition.y,0);
		
		/// Input Control
		// Save previous tracker arm angles
		pLeftArmAngle = tracker.data.leftArmAngle;
		pRightArmAngle = tracker.data.rightArmAngle;
    }

	// Add a relative force at a given (world) position on the object
    private void AddRelativeForceAtPosition(Vector3 relativeForce, Vector3 worldPosition) {
		// Convert force to world coordinates
		relativeForce = transform.TransformDirection(relativeForce);
		// Apply force
        rb.AddForceAtPosition(relativeForce, worldPosition);
    }
}
