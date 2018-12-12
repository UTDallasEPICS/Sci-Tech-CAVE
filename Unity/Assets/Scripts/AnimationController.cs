/* Controls the bird animation
 * 
 * The associated custom Animator has four layers:
 * Left Wing - Contains bones for the left wing - Layer fully enabled when the user is in control
 * Right Wing - Contains bones for the right wing - Layer fully enabled when the user is in control
 * Body WithoutWings - Contains bones for the body, exluding the wings and bones that affect the wings - Layer always fully enabled
 * Body WithWings - Contains all bones - the body, wings, and bones that affect the wings - Layer fully enabled when the user is NOT in control
 */ 

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationController : MonoBehaviour {
	/// External References
	// Reference to user input data
	private SensorInterface tracker;
	// Reference to Animator
	private Animator anim;

	/// Parameters
	// Rate to fade between animations (weight/s)
	float fadeRate = 1f;

	/* Animation States:
	 * Auto - Bird fully controlled by idling animations
	 * Fade in - Interpolate wings from rest to user arm position
	 * User control - Wings fully controlled by user's arms
	 * Fade out - Interpolate wings from user arm position to rest
	 */
	private enum AnimState {
		auto,
		fadeIn,
		userControl,
		fadeOut
	}
	AnimState state = AnimState.auto;

	// Initialization
	void Start () {
		// Get the tracker / reference to sensor info
		tracker = GameObject.Find("Tracker").GetComponent<SensorInterface>();
		// Get a reference to the Animator component
		anim = GetComponent<Animator>();
		/// Start state
		// Disable user control at start
		setUserControl(0f);
	}
	
	// Update is called once per frame
	void Update () {
		 switch (state) {
			case AnimState.auto:
				// On autopilot, choose some random idling animation every ~10 seconds
				// (default is "Idle" animation; returns to "Idle" once special animation finishes)
				if (Random.value < Time.deltaTime*.1){
					float rand = Random.value;
					if (rand < .4)
						anim.SetTrigger("sing");
					else if (rand < .6)
						anim.SetTrigger("peck");
					else if (rand < .8)
						anim.SetTrigger("preen");	
					else
						anim.SetTrigger("ruffle");
					// Make the "Idle" animation look different after every special animation by changing the blendTree's weight
					anim.SetFloat("IdleAgitated", Random.value);
				}
			break;
			case AnimState.fadeIn:
				// Update wing animation based on arm angles
				updateWings();
				// Gradually increase user control weight
				setUserControl(getUserControl() + fadeRate * Time.deltaTime);
				// Go to next state when end reached
				if (getUserControl() >= 1) {
					setUserControl(1f); // Warning! Unity does not clamp the wieght 0 - 1 for you
					state = AnimState.userControl;
				}
			break;
			case AnimState.userControl:
				// Update wing animation based on arm angles
				updateWings();
			break;
			case AnimState.fadeOut:
				// Update wing animation based on arm angles
				updateWings();
				// Gradually decrease user control weight
				setUserControl(getUserControl() - fadeRate * Time.deltaTime);
				// Go to next state when end reached
				if (getUserControl() <= 0) {
					setUserControl(0f); // Warning! Unity does not clamp the wieght 0 - 1 for you
					state = AnimState.auto;
				}
			break;
		}
	}

	/// Helper Functions
	// Set the amount of control (0 - 1) that the user has over the wings' animation
	private void setUserControl(float weight) {
		// Set the full body idle animation weight as inverse
		anim.SetLayerWeight(2, 1f-weight);
		// Set the wings' weight
		anim.SetLayerWeight(0, weight);
		anim.SetLayerWeight(1, weight);
	}
	private float getUserControl() {
		return anim.GetLayerWeight(0);
	}

	// Update the wings' animation based on the user's arms
	private void updateWings() {
		/* Syntax is anim.Play(stateNameHash, layer, normalizedTime)
		 * where stateNameHash = 0 is the current animation
		 * 
		 * Linear transformation: (90 - tracker.data.leftArmAngle)/180
		 * Experimental transformation: (1 + Mathf.Cos( (tracker.data.leftArmAngle+90)*Mathf.PI/180) )/2
		 * */
		anim.Play(0, 0, (90 - tracker.data.leftArmAngle)/180);
		anim.Play(0, 1, (90 - tracker.data.rightArmAngle)/180);
	}

	/// Public Functions
	// Public method to transition to/from user-controlled animations
	public void SetUserAnimation(bool b) {
		if (b)
			state = AnimState.fadeIn;
		else
			state = AnimState.fadeOut;
	}

	// Public method to trigger death animation
	public void SetDeathAnimation(bool b) {
		if (b)
			anim.SetTrigger("die");
		else {
			anim.ResetTrigger("die");
			anim.Play("Idle");
		}
	}
}
