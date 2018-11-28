using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using nt = nuitrack;

public class SensorInterface : MonoBehaviour
{

    string message = "";

    void Update()
    {
        if (CurrentUserTracker.CurrentUser != 0)
        {
            var convSkel = ConvertSkeleton(CurrentUserTracker.CurrentSkeleton);
            message = "User found: " + ProcessSkeleton(CurrentUserTracker.CurrentSkeleton);
            
            //Debug.Log(ConvertSkeleton(CurrentUserTracker.CurrentSkeleton));
        }
        else
        {
            message = "User not found";
        }
    }

    // Display the message on the screen
    void OnGUI()
    {
        GUI.color = Color.red;
        GUI.skin.label.fontSize = 50;
        GUILayout.Label(message);
    }

    // Update is called once per frame
    UserData ProcessSkeleton(nuitrack.Skeleton skel)
    {
        //var lHand = skel.GetJoint(nt.JointType.LeftHand);
        //var rHand = skel.GetJoint(nt.JointType.RightHand);

        Dictionary<nt.JointType, Vector3> convSkel = ConvertSkeleton(skel);

        Vector3 collarTorso = convSkel[nt.JointType.Torso] - convSkel[nt.JointType.RightCollar];
        Vector3 collarShoulder = convSkel[nt.JointType.RightShoulder] - convSkel[nt.JointType.RightCollar];

        var bodyPlane = Vector3.Cross(collarTorso, collarShoulder);


        Vector3 shoulderVector = convSkel[nt.JointType.RightShoulder] - convSkel[nt.JointType.LeftShoulder];

        if (skel.GetJoint(nt.JointType.RightHand).Confidence >= skel.GetJoint(nt.JointType.RightElbow).Confidence)
        {
            
        }

        Vector3 rightArmTip = GetPreferredJoint(skel.GetJoint(nt.JointType.RightHand), skel.GetJoint(nt.JointType.RightElbow));
        Vector3 leftArmTip = GetPreferredJoint(skel.GetJoint(nt.JointType.LeftHand), skel.GetJoint(nt.JointType.LeftElbow));
        Vector3 rightArmVec = rightArmTip - convSkel[nt.JointType.RightShoulder];
        Vector3 leftArmVec = leftArmTip - convSkel[nt.JointType.LeftShoulder];

        float rightArmAngle = -Vector3.SignedAngle(rightArmVec, shoulderVector, bodyPlane);
        float leftArmAngle = Vector3.SignedAngle(leftArmVec, -shoulderVector, bodyPlane);


        float rightArmMaxExtension = Vector3.Magnitude(rightArmTip - convSkel[nt.JointType.RightElbow]) + Vector3.Magnitude(convSkel[nt.JointType.RightElbow] - convSkel[nt.JointType.RightShoulder]);
        float LeftArmMaxExtension = Vector3.Magnitude(leftArmTip - convSkel[nt.JointType.LeftElbow]) + Vector3.Magnitude(convSkel[nt.JointType.LeftElbow] - convSkel[nt.JointType.LeftShoulder]);

        float rightArmExtension = Vector3.ProjectOnPlane(rightArmVec, bodyPlane).magnitude / rightArmMaxExtension;
        float leftArmExtension = Vector3.ProjectOnPlane(leftArmVec, bodyPlane).magnitude / LeftArmMaxExtension;

        UserData output = new UserData(rightArmAngle, rightArmExtension, leftArmAngle, leftArmExtension);
        return output;
        
        //return Vector3.Magnitude(rHand.Real.ToVector3() - lHand.Real.ToVector3());
        //return 0;
    }

    Dictionary<nuitrack.JointType, Vector3> ConvertSkeleton(nuitrack.Skeleton skel)
    {
        Dictionary<nuitrack.JointType, Vector3> output = new Dictionary<nuitrack.JointType, Vector3>();
        foreach (nuitrack.JointType jt in (nuitrack.JointType[])System.Enum.GetValues(typeof(nuitrack.JointType)))
        {
            nt.Joint rawJoint = skel.GetJoint(jt);
            output[jt] = rawJoint.Real.ToVector3();

            var J = new Joint(rawJoint.Real.ToVector3(), rawJoint.Confidence);
        }
        return output;
    }

    struct Joint : IComparable<Joint>
    {
        public Vector3 pos;
        public float confidence;

        public Joint(Vector3 pos, float confidence)
        {
            this.pos = pos;
            this.confidence = confidence;
        }

        public int CompareTo(Joint other)
        {
            return confidence.CompareTo(other.confidence);
        }
    }

    private T Max<T>(T o1, T o2) where T:IComparable<T>
    {
        return (o1.CompareTo(o2) > 0 ? o1 : o2);
    }

    public struct UserData
    {
        public float rightArmAngle, rightArmExtension, leftArmAngle, leftArmExtension;

        public UserData(float rightArmAngle, float rightArmExtension, float leftArmAngle, float leftArmExtension)
        {
            this.rightArmAngle = rightArmAngle;
            this.rightArmExtension = rightArmExtension;
            this.leftArmAngle = leftArmAngle;
            this.leftArmExtension = leftArmExtension;
        }

        public override string ToString()
        {
            return $"\nLA:{leftArmAngle:0.##}\n RA:{rightArmAngle:0.##}\n LE:{leftArmExtension:0.##}\n RE:{rightArmExtension:0.##}";
        }
    }

    private Vector3 GetPreferredJoint(nt.Joint hand, nt.Joint elbow)
    {
        return (hand.Confidence >= elbow.Confidence ? hand : elbow).Real.ToVector3();
    }
}
