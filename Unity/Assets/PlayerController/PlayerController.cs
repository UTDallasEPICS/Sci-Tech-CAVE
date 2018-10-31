
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
 * Bird object settings (for the object to which this controller is attached)
 * The bird should have medium drag, about 0.2 translational, 0.1 rotational.
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

    // Reference to the rigid body of the bird
    public Rigidbody rb;
    // Thrust caused by flapping wings
    public float flapThrustScale = 50f;
    // How much lift is generated
    public float liftScale = 1f;
    // How much vertical drag is generated
    public float vertDragScale = 1f;

    // Distance from center of bird to right/left points where lift is generated
    public float liftPointDistance = 1.5f; //(unused)
    // Left/right lift point
    private Transform leftLiftPoint;
    private Transform rightLiftPoint;

    // Amount left/right wing is extended (0 - 1, 0 = against body, 1 = horizontal)
    private float leftWingExtended = 1;
    private float rightWingExtended = 1;

    // Total lift under left/right wing
    private float leftLift = 0;
    private float rightLift = 0;

    // Initialization
    void Start() {
        // Bird RigidBody (physics body)
        rb = GetComponent<Rigidbody>();
        // Left and right lift points
        leftLiftPoint = transform.GetChild(0);
        rightLiftPoint = transform.GetChild(1);
    }

    // Update is called once per frame.
    void Update() {
        /// Debug vectors
        // Forward vector
        Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 5, Color.green);
        // Left wing lift
        Debug.DrawRay(leftLiftPoint.position, -leftLiftPoint.up * 0.5f * leftLift, Color.blue);
        // Right wing lift
        Debug.DrawRay(rightLiftPoint.position, -rightLiftPoint.up * 0.5f * rightLift, Color.blue);
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
        // Debug controls for keyboard
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
        // Control wing extension
        if (Input.GetKey("e")) {
            leftWingExtended += 0.2f;
        }
        if (Input.GetKey("d")) {
            leftWingExtended -= 0.2f;
        }
        if (Input.GetKey("r")) {
            rightWingExtended += 0.2f;
        }
        if (Input.GetKey("f")) {
            rightWingExtended -= 0.2f;
        }

        // Add sensor control input here //

        // Clamp wing extension 0 - 1
        leftWingExtended = Mathf.Clamp(leftWingExtended, 0, 1);
        rightWingExtended = Mathf.Clamp(rightWingExtended, 0, 1);

        // Reset lift at beginning of update
        leftLift = 0;
        rightLift = 0;

        // Generate lift based on bird's forward speed, downward speed, and the amount its wings are extended
        // (per-wing basis, so rotation matters)
            // Left
                // Velocity at left lift point
                Vector3 leftVel = rb.GetPointVelocity(leftLiftPoint.TransformPoint(leftLiftPoint.position));
                leftVel = rb.transform.InverseTransformDirection(leftVel); // (local space)

                // Counteract vertical velocity
                float leftDrag = leftVel.y;
                // Have a greater effect if falling downward
                if (leftDrag < 0) leftDrag *= -3;
                else leftDrag*= -1;

                // Add lift if going forward quickly
                float ll = leftVel.z;
                if (ll < 0) ll = 0;

                leftLift += leftWingExtended * (leftDrag*vertDragScale*0 + ll*liftScale);
            // Right
                // Velocity at right lift point
                Vector3 rightVel = rb.GetPointVelocity(rightLiftPoint.TransformPoint(rightLiftPoint.position));
                rightVel = rb.transform.InverseTransformDirection(rightVel); // (local space)

                // Counteract vertical velocity
                float rightDrag = rightVel.y;
                // Have a greater effect if falling downward
                if (rightDrag < 0) rightDrag *= -3;
                else rightDrag *= -1;

                // Add lift if going forward quickly
                float rl = rightVel.z;
                if (rl < 0) rl = 0;

                rightLift += rightWingExtended * (rightDrag*vertDragScale*0 + rl*liftScale);

        // Generate lift based on flapping wings
        //

        // Apply forces based on lift at left/right wing
        AddRelativeForceAtPosition(new Vector3(0, leftLift, 0), leftLiftPoint.position);
        AddRelativeForceAtPosition(new Vector3(0, rightLift, 0), rightLiftPoint.position);

        // Keep depth (distance from camera) fixed; prevent drift in forward / backward direction
        rb.transform.localPosition = new Vector3(rb.transform.localPosition.x,rb.transform.localPosition.y,0);
    }

    private void AddRelativeForceAtPosition(Vector3 force, Vector3 position) {
        position = transform.TransformPoint(position);
        rb.AddForceAtPosition(force, position);
    }
}
