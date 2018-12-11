using System;
using System.Collections.Generic;
using UnityEngine;
using nt = nuitrack;
using System.IO;

public class SensorInterface : MonoBehaviour
{
    // The data to export
    public UserData data = new UserData(0, 0, 0, 0);

    // Editor Controls
    public bool showDebugGUI = false, clipSmallAngles = true, logTrackingData = false;
    public float smallAngleThreshold = 9f;
    public float maxJointVelocity = 1000f;

    private string message = "";
    private string logPath;
    private MovingAverage av;
    private Skelly sk;
    private NamedLogger nLog;
    private DisposableList disList = new DisposableList();

    void Start()
    {
        av = new MovingAverage(4, 0.1f, 300000f, logTrackingData);
        disList.Add(av);

        logPath = Path.Combine(Application.dataPath, "Logs");


        nLog = new NamedLogger(
            new Dictionary<string, string> {
                { "rAngle", "rAngle" },
                { "lAngle", "lAngle" },
                { "rElev", "rElev" },
                { "rHorizX", "rHorizX" },
                { "rHorizZ", "rHorizZ" }
            },
            logTrackingData, logPath);

        disList.Add(nLog);

        sk = new Skelly(maxJointVelocity, true);
    }

    void Update()
    {
        if (CurrentUserTracker.CurrentUser != 0)
        {
            // Update the user data
            data = ProcessSkeleton(CurrentUserTracker.CurrentSkeleton);
            message = "User found: " + data;
        }
        else
        {
            message = "User not found";
        }
    }

    /// DEBUG Display the message on the screen
    void OnGUI()
    {
        if (showDebugGUI)
        {
            GUI.color = Color.red;
            GUI.skin.label.fontSize = 50;
            GUILayout.Label(message);
        }
    }

    void OnDestroy()
    {
        disList.Dispose();
    }

    // Update is called once per frame
    UserData ProcessSkeleton(nt.Skeleton skel)
    {
        nLog.Log("rElev", skel.GetJoint(nt.JointType.RightHand).Real.ToVector3().y);
        nLog.Log("rHorizX", skel.GetJoint(nt.JointType.RightHand).Real.ToVector3().x);
        nLog.Log("rHorizZ", skel.GetJoint(nt.JointType.RightHand).Real.ToVector3().z);

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

        float rightArmAngle = clipSmallAngle(-Vector3.SignedAngle(rightArmVec, shoulderVector, bodyPlane));
        float leftArmAngle = clipSmallAngle(Vector3.SignedAngle(leftArmVec, -shoulderVector, bodyPlane));

        nLog.Log("rAngle", rightArmAngle);
        nLog.Log("lAngle", leftArmAngle);
		// Clamp angles to -90 to 90
		leftArmAngle = Mathf.Clamp(leftArmAngle, -90, 90);
		rightArmAngle = Mathf.Clamp(rightArmAngle, -90, 90);

        float rightArmMaxExtension = Vector3.Magnitude(rightArmTip - sk[nt.JointType.RightElbow].pos) + Vector3.Magnitude(sk[nt.JointType.RightElbow].pos - sk[nt.JointType.RightShoulder].pos);
        float LeftArmMaxExtension = Vector3.Magnitude(leftArmTip - sk[nt.JointType.LeftElbow].pos) + Vector3.Magnitude(sk[nt.JointType.LeftElbow].pos - sk[nt.JointType.LeftShoulder].pos);

        float rightArmExtension = Vector3.ProjectOnPlane(rightArmVec, bodyPlane).magnitude / rightArmMaxExtension;
        float leftArmExtension = Vector3.ProjectOnPlane(leftArmVec, bodyPlane).magnitude / LeftArmMaxExtension;

        UserData output = new UserData(rightArmAngle, rightArmExtension, leftArmAngle, leftArmExtension);
		//return output;
        av.PushSample(output);

        return av.GetAverage();
    }

    private float clipSmallAngle(float angle)
    {
        if (!clipSmallAngles)
        {
            return angle;
        }

        if (angle > -smallAngleThreshold && angle < smallAngleThreshold)
        {
            return 0;
        }
        else if (angle < 0)
        {
            return angle + smallAngleThreshold;
        }
        else
        {
            return angle - smallAngleThreshold;
        }
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
        private NamedLogger nLog;

        public MovingAverage(int sampleCount, float interpTime = 0f, float errorThreshold = float.PositiveInfinity, bool log = false)
        {
            SampleCount = sampleCount;
            Samples = new UserData[SampleCount];


            this.interpTime = interpTime;
            this.errorThreshold = errorThreshold;
            this.log = log;

            if (log)
            {
                nLog = new NamedLogger(
                    new Dictionary<string, string> {
                        { "raLogRaw", "raLogRaw" },
                        { "raAvLog", "raAvLog" }
                    },
                    log, Path.Combine(Application.dataPath, "Logs"));

                disList.Add(nLog);

                raLogRaw = nLog["raLogRaw"];
                raAvLog = nLog["raLogRaw"];
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
            }
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
        private bool enable;

        public FileLogger(string file, bool enable = true)
        {
            this.enable = enable;
            if (enable)
            {
                try
                {
                    (new FileInfo(file)).Directory.Create();
                    stream = new StreamWriter(new BufferedStream(new FileStream(file, FileMode.Append)));
                }
                catch (Exception e)
                {
                    Debug.Log("Unable to start logger for: " + file);
                    enable = false;
                    stream = null;
                }
            }
            else
            {
                stream = null;
            }
        }

        public void Dispose()
        {
            if (enable)
            {
                stream.Dispose();
            }
        }

        public void Log(float f)
        {
            if (enable)
            {
                stream.WriteLine(f);
            }
        }
    }

    class NamedLogger : IDisposable
    {
        private Dictionary<string, FileLogger> loggers = new Dictionary<string, FileLogger>();
        private bool enable;

        public NamedLogger(Dictionary<string, string> targets, bool enable = true, string prefix = "")
        {
            this.enable = enable;
            if (enable)
            {
                foreach (var f in targets)
                {
                    try
                    {
                        string logFilePath = Path.Combine(prefix, f.Value + ".txt");
                        loggers[f.Key] = new FileLogger(logFilePath, enable);
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!enable)
            {
                return;
            }

            foreach (var l in loggers)
            {
                try
                {
                    l.Value.Dispose();
                }
                catch (Exception e)
                {

                }
            }
        }

        public void Log(string name, float value)
        {
            if (!enable)
            {
                return;
            }
            loggers[name].Log(value);
        }

        public FileLogger this[string name]
        {
            get { return loggers[name]; }
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

