using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationController : MonoBehaviour {
	// Reference to user input data
	private SensorInterface tracker;
	// Reference to Animator
	private Animator anim;

	// Initialization
	void Start () {
		// Get the tracker / reference to sensor info
		tracker = GameObject.Find("Tracker").GetComponent<SensorInterface>();
		// Get a reference to the Animator component
		anim = GetComponent<Animator>();
	}
	
	// Update is called once per frame
	void Update () {
		// Update wing animation based on arm angles
		anim.Play("Flap", 0, (90 - tracker.data.leftArmAngle)/180);
		anim.Play("Flap", 1, (90 - tracker.data.rightArmAngle)/180);
	}
}
