/* Controls overall game state and behavior
 * 
 * TODO change animation state and physics state
 */ 

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BansheeGz.BGSpline.Components;

public class StateController : MonoBehaviour {
	/* States:
	 * Idle - Game at starting position, no user detected
	 * Wait - User detected, wait to begin flying
	 * Liftoff - User started to fly, interpolate from rest to flying speeds
	 * Flying - Travel along fligth path; physics and user control enabled
	 * User Lost - User not found, wait to make sure before resetting game state to idle
	 */
	private enum State {
		idle,
		wait,
		liftoff,
		flying,
		userLost
	}
	State state = State.idle;

	/// Parameters
	// Flight speed along the path
	[Range(0, 20)]
	public float flightSpeed = 10f;

	/// Parameters: Fixed
	// The force the wings must exert to begin flying
	private const float forceStartThreshold = 4f;
	// Rate at which to increase flight speed during takeoff (m/s)
	private const float liftoffRate = 10f;

	/// External References
	// Reference to the flight path
	BGCcCursorChangeLinear[] flightPathMotion;
	// References to info icons
	GameObject infoIcons;
	// Reference to player
	PlayerController player;
	// Reference to player animation
	AnimationController playerAnim;

	/// State Control Variables
	// Used to ensure eing forces are intentional; Keeps track over several frames
	private int thrustFrameCount = 0;

	// Use this for initialization
	void Start () {
		// Get the references to the flight path cursor movement
		flightPathMotion = GameObject.Find("Flight Path").GetComponents<BGCcCursorChangeLinear>();
		// Get the referneces to the info icons
		infoIcons = GameObject.Find("Info Icons");
		// Get reference to player
		player = GameObject.Find("Player Group").transform.GetChild(0).GetComponent<PlayerController>();
		// Get reference to player animator
		playerAnim = GameObject.Find("Player Group").transform.GetChild(0).GetComponent<AnimationController>();
		/// Setup first state 'idle' for first time
		// Disable physics at start
		enablePhysics(false);
	}
	
	// Update is called once per frame
	void Update () {
		switch (state) {
			case State.idle:
				// If a person is detected, go to the wait state
				if (CurrentUserTracker.CurrentUser != 0) {
					// Change the icon to prompt the user to fly
					infoIcons.transform.GetChild(0).gameObject.SetActive(false);
					infoIcons.transform.GetChild(1).gameObject.SetActive(true);
					// Enable user-controlled wing animation
					playerAnim.SetUserAnimation(true);
					// Go to the next state
					state = State.wait;
				}
			break;
			case State.wait:
				// If a person not found, go back to idle state
				if (CurrentUserTracker.CurrentUser == 0) {
					// Change the icon to 'detect humans'
					infoIcons.transform.GetChild(0).gameObject.SetActive(true);
					infoIcons.transform.GetChild(1).gameObject.SetActive(false);
					// Go to the next state
					state = State.idle;
				}
				// If user stars flying (force greater than threshold for 6 frames), go to liftoff
				if (player.GetLeftWingForce() > forceStartThreshold && player.GetRightWingForce() > forceStartThreshold) {
					thrustFrameCount += 1;
					if (thrustFrameCount >= 6) {
						thrustFrameCount = 0;
						// Enable physics
						enablePhysics(true);
						// Go to liftoff
						state = State.liftoff;
					}
				}
				else
					thrustFrameCount = 0;
			break;
			case State.liftoff:
				// Gradually increase the speed
				setSpeed(getSpeed() + liftoffRate * Time.deltaTime);
				// Go to flying state once max speed reached
				if (getSpeed() >= 10) {
					setSpeed(10);
					state = State.flying;
				}
			break;
			case State.flying:

			break;
			case State.userLost:

			break;
		}
		if (Input.GetKey("u")) {
			infoIcons.transform.GetChild(0).gameObject.SetActive(false);
			infoIcons.transform.GetChild(1).gameObject.SetActive(true);
        }
		if (Input.GetKey("i")) {
            setSpeed(flightSpeed);
        }
		if (Input.GetKey("p")) {
            //playerAnim.Test();
        }
	}

	/// Helper Functions
	// Set the speed of the birds along the flight path
	private void setSpeed(float speed) {
		// The first is the bird, the second is the camera
		flightPathMotion[0].Speed = speed;
		flightPathMotion[1].Speed = speed;
	}
	// Get the speed of the birds along the flight path
	private float getSpeed() {
		return flightPathMotion[0].Speed;
	}

	// Enable / disable physics on the bird
	private void enablePhysics(bool enable) {
		player.gameObject.GetComponent<Rigidbody>().useGravity = enable;
		player.applyWingForces = enable;
	}
}
