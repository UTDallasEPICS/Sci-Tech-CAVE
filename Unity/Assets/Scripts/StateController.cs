/* Controls overall game state and behavior
 */ 

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BansheeGz.BGSpline.Components;

public class StateController : MonoBehaviour {
	/* States:
	 * Fade In - Reset the game state and fade in from black
	 * Idle - Game at starting position, no user detected
	 * Wait - User detected, wait to begin flying
	 * Liftoff - User started to fly, interpolate from rest to flying speeds
	 * Flying - Travel along fligth path; physics and user control enabled
	 * User Lost - User not found, wait to make sure before resetting game state to idle
	 * Game Over - Bird flew into something; reset the game
	 * Fade Out - Fade out to black; go to fade in
	 */
	private enum State {
		fadeIn,
		idle,
		wait,
		liftoff,
		flying,
		userLost,
		gameOver,
		fadeOut
	}
	State state = State.fadeIn;

	/// Parameters
	// Max flight speed along the path
	[Tooltip("Flight speed along the path")]
	public float flightSpeed = 10f;
	// The force the wings must exert to begin flying
	[Tooltip("The minimum force required to take off")]
	public float forceStartThreshold = 4f;
	// Rate at which to increase flight speed during takeoff (m/s)
	[Tooltip("The rate (m/s^2) the bird accelerates from rest")]
	public float liftoffRate = 10f;
	// Time to wait before resetting after game over (s)
	[Tooltip("Time to wait before resetting after game over (s)")]
	public float resetWaitTime = 4f;
	// Time to wait before resetting after user lost
	[Tooltip("Time to wait before resetting after user lost (s)")]
	public float errorResetWaitTime = 6f;
	// Distance of the camera from the player group
	[Tooltip("Distance of the camera from the player group")]
	public float cameraDistance = 10f;
	// Rate at which to fade in/out from black during transitions
	[Tooltip("Rate at which to fade in/out from black during transitions")]
	public float fadeRate = 1f;

	/// External References
	// Reference to the flight path
	private BGCcCursorChangeLinear[] flightPathSpeeds;
	private BGCcCursor[] flightPathCursors;
	private BGCcMath flightPathMath;
	// References to info icons
	private GameObject infoIcons;
	// Reference to the player
	private PlayerController playerController; // controller
	private AnimationController playerAnim; // animation controller
	// Referneces to the canvases (black screens, one for each display)
	private Image[] blackScreens = new Image[8];

	/// State Control Variables
	// Used to ensure wing forces are intentional; Keeps track over several frames
	private int thrustFrameCount = 0;
	// Marks the beginning time of a transition
	private float timeMarker;

	// Use this for initialization
	void Start () {
		// Get the references to the flight path cursor movement
		flightPathSpeeds = GameObject.Find("Flight Path").GetComponents<BGCcCursorChangeLinear>();
		flightPathCursors = GameObject.Find("Flight Path").GetComponents<BGCcCursor>();
		flightPathMath = GameObject.Find("Flight Path").GetComponent<BGCcMath>();
		// Get the referneces to the info icons
		infoIcons = GameObject.Find("Info Icons");
		// Get reference to player
		playerController = GameObject.Find("Player Group").transform.GetChild(0).GetComponent<PlayerController>();
		playerAnim = GameObject.Find("Player Group").transform.GetChild(0).GetComponent<AnimationController>();
		// Setup black screens; Duplicate the template so there's one for each display (max 8 displays, so just make the rest, displays 2-8)
		GameObject[] canvases = new GameObject[8];
		canvases[0] = GameObject.Find("Canvas");
		blackScreens[0] = canvases[0].transform.GetChild(0).gameObject.GetComponent<Image>();
		for (int i = 1; i < 8; i++) {
			canvases[i] = Instantiate(canvases[0]);
			// Default display is 1. Set correct target display for canvases 2-8
			canvases[i].GetComponent<Canvas>().targetDisplay = i+1;
			blackScreens[i] = canvases[i].transform.GetChild(0).gameObject.GetComponent<Image>();
		}
		// Setup game for the first time
		resetBird();
	}
	
	// Update is called once per frame
	void Update () {
		switch (state) {
			case State.fadeIn:
				// Fade out the black screens
				for (int i = 0; i < 8; i++) {
					Color newClr = blackScreens[i].color;
					newClr.a -= fadeRate * Time.deltaTime;
					blackScreens[i].color = newClr;
				}
				// Go to idle state when black screen is gone
				if (blackScreens[0].color.a <= 0) {
					// Set alpha to 0 (it's probably below 0)
					for (int i = 0; i < 8; i++) {
						blackScreens[i].color = new Color(0, 0, 0, 0);
					}
					// Go to idling state
					state = State.idle;
				}
			break;
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
				if (playerController.GetLeftWingForce() > forceStartThreshold && playerController.GetRightWingForce() > forceStartThreshold) {
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
				// TODO - Status message

				// If the user is found again, go back to flying
				if (CurrentUserTracker.CurrentUser != 0) {
					// Go to the next state
					state = State.flying;
				}
				// Wait for a certain number of seconds to pass, then go to fade out
				if (Time.time - timeMarker >= errorResetWaitTime) {
					state = State.fadeOut;
				}
			break;
			case State.gameOver:
				// Wait for a certain number of seconds to pass, then go to fade out
				if (Time.time - timeMarker >= resetWaitTime) {
					state = State.fadeOut;
				}
			break;
			case State.fadeOut:
				// Fade in the black overlay(s)
				for (int i = 0; i < 8; i++) {
					Color newClr = blackScreens[i].color;
					newClr.a += fadeRate * Time.deltaTime;
					blackScreens[i].color = newClr;
				}
				// Go to idle state when black screen is gone
				if (blackScreens[0].color.a >= 1) {
					// Set alpha to 1 (it's probably above 1)
					for (int i = 0; i < 8; i++) {
						blackScreens[i].color = new Color(0, 0, 0, 1);
					}
					// Reset the bird
					resetBird();
					// Go to fade in
					state = State.fadeIn;
				}
			break;
		}
		/// DEBUG Controls for Keyboard
		#if UNITY_EDITOR
		if (Input.GetKey("i")) {
            setSpeed(flightSpeed);
        }
		if (Input.GetKey("o")) {
            resetBird();
        }
		#endif
	}

	/// Helper Functions
	// Set the speed of the birds along the flight path
	private void setSpeed(float speed) {
		// The first is the bird, the second is the camera
		flightPathSpeeds[0].Speed = speed;
		flightPathSpeeds[1].Speed = speed;
	}
	// Get the speed of the birds along the flight path
	private float getSpeed() {
		return flightPathSpeeds[0].Speed;
	}

	// Enable / disable physics on the bird
	private void enablePhysics(bool enable) {
		playerController.gameObject.GetComponent<Rigidbody>().useGravity = enable;
		playerController.applyWingForces = enable;
	}

	// Reset the bird
	private void resetBird() {
		// Reset player group position
		flightPathCursors[0].Distance = 0;
		// Reset bird local position and velocity
		playerController.gameObject.transform.localPosition = new Vector3(0, 0, 0);
		playerController.gameObject.GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
		// Reset camera position
		flightPathCursors[1].Distance = flightPathMath.GetDistance() - cameraDistance;
		// Reset its animation
		playerAnim.SetDeathAnimation(false);
		// Dsiable user animation
		playerAnim.SetUserAnimation(false);
		// Turn oon Z-axis constraint
		playerController.SetConstrainZ(true);
		// Disable physics
		enablePhysics(false);
	}
	
	/// Public Functions
	// Reciever for bird crash event
	public void BirdCrash() {
		// Trigger death animation
		playerAnim.SetDeathAnimation(true);
		// Turn off Z-axis constraint
		playerController.SetConstrainZ(false);
		// Stop traveling along the path
		setSpeed(0f);
		// Disable bird flight
		playerController.applyWingForces = false;
		// Get the current time
		timeMarker = Time.time;
		// Go to death state
		state = State.gameOver;
	}

	/// Editor Functions
	#if UNITY_EDITOR
	// Called when variables changed in the inspector
	void OnValidate() {
		// Move the camera based on cameraDistance
		GameObject.Find("Flight Path").GetComponents<BGCcCursor>()[1].Distance = 
			GameObject.Find("Flight Path").GetComponent<BGCcMath>().GetDistance() - cameraDistance;
	}
	#endif
}
