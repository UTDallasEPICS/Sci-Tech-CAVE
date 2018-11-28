
/* Velocity From Position
 * 
 * This class is for an object that is not affected by physics and whose position
 * is set explicitly every frame. This class has a 'velocity' member based on distance / time
 * (calculated per frame) so that other components that need velocity of this object can obtain it
 * even though the velocity for this object is not calculated by the physics engine.
 * 
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VelocityFromPosition : MonoBehaviour {
    // The custom velocity component
    public Vector3 velocity = new Vector3(0, 0, 0);

    // Object's past position
    private Vector3 pPos;

    /// Moving average
    // Max number of velocities to average
    private const int MAX_VELS = 10;
    // Array of past velocities to average (auto initialized to zero)
    private Vector3[] vels = new Vector3[MAX_VELS];
	// Times at which the velocities were recorded
	private float[] times = new float[MAX_VELS];
	// Time period from which to take velocities (s) (current time - TIME_PD to current time)
	private const float TIME_PD = 0.2f;

	// Current index in the array(s)
	int marker = 0;

	// Initialization
	void Start () {
        pPos = this.transform.position;
    }
	
	// Update is called once per frame
	void Update () {
        // Get current position
		Vector3 pos = transform.position;
        
        // Calculate the velocity from delta position / delta time
        if (Time.deltaTime > 0) {
			vels[marker] = (pos - pPos) / Time.deltaTime;
        }
        else {
            vels[marker] = new Vector3(0, 0, 0);
        }
		times[marker] = Time.time;

		// Get moving average of past velocities within time period and number limit
		velocity = new Vector3(0, 0, 0); // reset velocity
		int len = 0; // number of velocities averaged
		for (int i = MAX_VELS - 1; i > marker; i--) {
			if (Time.time - times[i] > TIME_PD)
				break;
			velocity += vels[i];
			len++;
		}
		for (int i = marker; i > 0; i--) {
			if (Time.time - times[i] > TIME_PD)
				break;
			velocity += vels[i];
			len++;
		}
		velocity /= len;

		// Increase marker
		marker++;
		if (marker >= MAX_VELS)
			marker = 0;
        
        // Set past position
        pPos = pos;
	}
}



// Basic calculation without smoothing
// Note: No smoothing, so relies on object path to be smooth (like spline)
/*
public class VelocityFromPosition : MonoBehaviour {
    // The velocity component
    public Vector3 velocity = new Vector3(0, 0, 0);

    private Vector3 pPos;

	// Initialization
	void Start () {
        pPos = this.transform.position;
    }
	
	// Update is called once per frame
	void Update () {
        // Get current position
		Vector3 pos = transform.position;
        
        // Calculate the velocity from delta position / delta time
        if (Time.deltaTime > 0) {
            velocity = (pos - pPos) / Time.deltaTime;
        }
        else {
            velocity = new Vector3(0, 0, 0);
        }
        
        // Set past position
        pPos = pos;
	}
}
*/
