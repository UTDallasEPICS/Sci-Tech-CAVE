using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionHandler : MonoBehaviour {
	// Reference to the state controller
	StateController stateController;

	// Use this for initialization
	void Start () {
		// Get the state controller reference
		stateController = GameObject.Find("Game State").GetComponent<StateController>();
	}

	// Called on object collision
	void OnCollisionEnter(Collision collision) {
		// Tell the state controller when the brid crashes
		stateController.BirdCrash();
    }
}
