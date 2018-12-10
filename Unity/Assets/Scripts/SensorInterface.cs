using System;
using System.Collections.Generic;
using UnityEngine;
using nt = nuitrack;
using System.IO;

public class SensorInterface : MonoBehaviour
{
    // The data to export
    public UserData data = new UserData(0, 0, 0, 0);
    string message = "";
    public bool showDebugGUI = false;
    MovingAverage av;
    Skelly sk;
    FileLogger rElev;
    DisposableList disList = new DisposableList();

    void Start()
    {
        av = new MovingAverage(4, 0.1f, 300000f);
        disList.Add(av);

        sk = new Skelly(500f, true);

        rElev = new FileLogger("C:\\\\Users\\noah-\\Desktop\\rElev.txt");
        disList.Add(rElev);
    }

    void Update()
    {
        if (CurrentUserTracker.CurrentUser != 0)
        {
            // Update the user data
            data = ProcessSkeleton(CurrentUserTracker.CurrentSkeleton);
            if (showDebugGUI)
            {
                message = "User found: " + data;
            }
        }
        else
        {
            if (showDebugGUI)
            {
                message = "User not found";
            }
        }
    }

    /// DEBUG Display the message on the screen
    void OnGUI()
    {
        GUI.color = Color.red;
        GUI.skin.label.fontSize = 50;
        GUILayout.Label(message);
    }

    void OnDestroy()
    {
        disList.Dispose();
        //av.Dispose();
    }

    // Update is called once per frame
    UserData ProcessSkeleton(nt.Skeleton skel)
    {
        Dictionary<nt.JointType, Vector3> convSkel = ConvertSkeleton(skel);

        sk.Update(skel);


        Vector3 collarTorso = sk[nt.JointType.Torso].pos - sk[nt.JointType.RightCollar].pos;
        Vector3 collarShoulder = sk[nt.JointType.RightShoulder].pos - sk[nt.JointType.RightCollar].pos;

        var bodyPlane = Vector3.Cross(collarTorso, collarShoulder);


        Vector3 shoulderVector = sk[nt.JointType.RightShoulder].pos - sk[nt.JointType.LeftShoulder].pos;

        Vector3 rightArmTip = GetPreferredJoint(sk[nt.JointType.RightHand], sk[nt.JointType.RightElbow]);
        Vector3 leftArmTip = GetPreferredJoint(sk[nt.JointType.LeftHand], sk[nt.JointType.LeftElbow]);
        Vector3 rightArmVec = rightArmTip - sk[nt.JointType.RightShoulder].pos;
        Vector3 leftArmVec = leftArmTip - sk[nt.JointType.LeftShoulder].pos;

        float rightArmAngle = -Vector3.SignedAngle(rightArmVec, shoulderVector, bodyPlane);
        float leftArmAngle = Vector3.SignedAngle(leftArmVec, -shoulderVector, bodyPlane);


        float rightArmMaxExtension = Vector3.Magnitude(rightArmTip - sk[nt.JointType.RightElbow].pos) + Vector3.Magnitude(sk[nt.JointType.RightElbow].pos - sk[nt.JointType.RightShoulder].pos);
        float LeftArmMaxExtension = Vector3.Magnitude(leftArmTip - sk[nt.JointType.LeftElbow].pos) + Vector3.Magnitude(sk[nt.JointType.LeftElbow].pos - sk[nt.JointType.LeftShoulder].pos);

        float rightArmExtension = Vector3.ProjectOnPlane(rightArmVec, bodyPlane).magnitude / rightArmMaxExtension;
        float leftArmExtension = Vector3.ProjectOnPlane(leftArmVec, bodyPlane).magnitude / LeftArmMaxExtension;

        UserData output = new UserData(rightArmAngle, rightArmExtension, leftArmAngle, leftArmExtension);

        av.PushSample(output);

        return av.GetAverage();
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
            return "LA: " + Mathf.Round(leftArmAngle) + "\nRA: " + Mathf.Round(rightArmAngle);
        }

        public void Add(UserData a)
        {
            rightArmAngle += a.rightArmAngle;
            rightArmExtension += a.rightArmExtension;
            leftArmAngle += a.leftArmAngle;
            leftArmExtension += a.leftArmExtension;
        }

        public void Divide(int a)
        {
            rightArmAngle /= a;
            rightArmExtension /= a;
            leftArmAngle /= a;
            leftArmExtension /= a;
        }

        public void Interp(UserData a, float t)
        {
            rightArmAngle = Mathf.Lerp(rightArmAngle, a.rightArmAngle, t);
            leftArmAngle = Mathf.Lerp(leftArmAngle, a.leftArmAngle, t);
            rightArmExtension = Mathf.Lerp(rightArmExtension, a.rightArmExtension, t);
            leftArmExtension = Mathf.Lerp(leftArmExtension, a.leftArmExtension, t);
        }

        public static UserData operator *(float a, UserData b)
        {
            return new UserData(b.rightArmAngle * a, b.rightArmExtension * a, b.leftArmAngle * a, b.leftArmExtension * a);
        }

        public static UserData operator +(UserData a, UserData b)
        {
            return new UserData(a.rightArmAngle + b.rightArmAngle, a.rightArmExtension + b.rightArmExtension, a.leftArmAngle + b.leftArmAngle, a.leftArmExtension + b.leftArmExtension);
        }
    }

    private Vector3 GetPreferredJoint(nt.Joint hand, nt.Joint elbow)
    {
        return (hand.Confidence >= elbow.Confidence ? hand : elbow).Real.ToVector3();
    }

    private Vector3 GetPreferredJoint(Joint hand, Joint elbow)
    {
        return (hand.confidence >= elbow.confidence) ? hand.pos : elbow.pos;
    }

    private class MovingAverage : IDisposable
    {
        private FileLogger raLogRaw, raAvLog;
        private int SampleCount;
        private UserData[] Samples;
        private bool upToDate = false, log;
        private int index = 0;
        private float interpTime, errorThreshold;
        private UserData average;
        private DisposableList disList = new DisposableList();

        public MovingAverage(int sampleCount, float interpTime = 0f, float errorThreshold = float.PositiveInfinity, bool log = false)
        {
            SampleCount = sampleCount;
            Samples = new UserData[SampleCount];
            this.interpTime = interpTime;
            this.errorThreshold = errorThreshold;
            this.log = log;
            if (log)
            {
                raLogRaw = new FileLogger(Path.Combine(Application.dataPath, "raRaw.txt"));
                disList.Add(raLogRaw);
                raAvLog = new FileLogger(Path.Combine(Application.dataPath, "raAv.txt"));
                disList.Add(raAvLog);
            }

        }

        public void PushSample(UserData sample)
        {
            if (Mathf.Abs(Samples[index].leftArmAngle - sample.leftArmAngle) > errorThreshold || Mathf.Abs(Samples[index].rightArmAngle - sample.rightArmAngle) > errorThreshold)
            {
                //DEBUG
                //print("Position error detected, smoothing...");
                var t = Samples[index];
                t.Interp(sample, interpTime);
                sample = t;
            }
            Samples[index] = sample;
            index = (index + 1) % Samples.Length;
            upToDate = false;

            if (log)
            {
                raLogRaw.Log(sample.rightArmAngle);
            }
        }

        public UserData GetAverage()
        {
            if (!upToDate)
            {
                average = new UserData(0, 0, 0, 0);
                foreach (var a in Samples)
                {
                    average.Add(a);
                }
                average.Divide(SampleCount);
            }

            if (log)
            {
                raAvLog.Log(average.rightArmAngle);
            }
            return average;
        }

        public void Dispose()
        {
            if (log)
            {
                disList.Dispose();
                //raAvLog.Dispose();
                //raLogRaw.Dispose();
            }
        }
    }

    class Vector3MovingAverage
    {
        private Vector3[] samples;
        private int index = 0;
        private Vector3 average;
        bool upToDate;

        public Vector3MovingAverage(int size)
        {
            samples = new Vector3[size];
            upToDate = false;
        }

        public Vector3 PushSample(Vector3 samp)
        {
            samples[index] = samp;
            index = (index + 1) % samples.Length;
            upToDate = false;
            return samp;
        }

        public Vector3 GetAverage()
        {
            if (!upToDate)
            {
                var output = new Vector3();
                foreach (var s in samples)
                {
                    output += s;
                }
                output /= samples.Length;
            }

            return average;
        }
    }

    class Skelly
    {
        private Dictionary<nt.JointType, Joint> joints;
        private float maxVelocity;
        private bool log;

        public Skelly(float maxVelocity, bool log = false)
        {
            this.maxVelocity = maxVelocity;
            this.log = log;

            joints = new Dictionary<nt.JointType, Joint>();

            foreach (nuitrack.JointType jt in (nuitrack.JointType[])System.Enum.GetValues(typeof(nuitrack.JointType)))
            {
                joints[jt] = new Joint(new Vector3(0, 0, 0), 0f);
            }
        }

        public Joint this[nt.JointType j]
        {
            get { return joints[j]; }
            set { joints[j] = value; }
        }

        public bool Update(nt.Skeleton newData)
        {
            bool error = false;

            foreach (nuitrack.JointType jt in (nuitrack.JointType[])System.Enum.GetValues(typeof(nuitrack.JointType)))
            {
                Vector3 newJPos = newData.GetJoint(jt).Real.ToVector3();
                float newJConf = newData.GetJoint(jt).Confidence;

                if (DistanceTraveled(joints[jt], newJPos) > maxVelocity * Time.deltaTime)
                {
                    joints[jt] = new Joint(joints[jt].pos + Vector3.Normalize(newJPos - joints[jt].pos) * maxVelocity * Time.deltaTime, newJConf);
                    error = true;
                }
                else
                {
                    joints[jt] = new Joint(newJPos, newJConf);
                }
            }

            if (log)
            {
                Debug.Log("Max joint velocity exceeded, interpolating");
            }

            return error;
        }

        public Dictionary<nt.JointType, Joint> GetValue()
        {
            return new Dictionary<nt.JointType, Joint>(joints);
        }

        private static float DistanceTraveled(Joint a, Vector3 b)
        {
            return Vector3.Magnitude(b - a.pos);
        }
    }

    class FileLogger : IDisposable
    {
        private StreamWriter stream;

        public FileLogger(string file)
        {
            stream = new StreamWriter(new BufferedStream(new FileStream(file, FileMode.Append)));
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        public void Log(float f)
        {
            stream.WriteLine(f);
        }
    }

    class DisposableList : List<IDisposable>, IDisposable
    {
        public void Dispose()
        {
            foreach (var o in this)
            {
                try
                {
                    o.Dispose();
                }
                catch (Exception e)
                {

                }
            }
        }
    }
}

