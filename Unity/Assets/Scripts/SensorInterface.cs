using System;
using System.Collections.Generic;
using UnityEngine;
using nt = nuitrack;
using System.IO;

/// <summary>
/// This class process the data from Nuitrack and provides that data to the rest of the game.
/// </summary>
public class SensorInterface : MonoBehaviour
{
    // Editor Controls
    /// <summary>
    /// Whether to show user tracking status and arm angle onscreen
    /// </summary>
    public bool showDebugGUI = false;
    /// <summary>
    /// Whether to remove the small deadzone angles near 0 degrees 
    /// </summary>
    public bool clipSmallAngles = true;
    /// <summary>
    /// Whether to log detailed tracking data
    /// </summary>
    public bool logTrackingData = false;
    /// <summary>
    /// Angles below this threshold will be considered small for the purposes of filtering
    /// </summary>
    public float smallAngleThreshold = 9f;
    /// <summary>
    /// Maximum allowed joint velocity (in m/s)
    /// </summary>
    public float maxJointVelocity = 10f;
    /// <summary>
    /// Number of samples to use in the moving average for the UserData
    /// </summary>
    public int averagingSamples = 4;



    /// <summary>
    /// The data to export
    /// </summary>
    public UserData data = new UserData(0, 0, 0, 0);



    /// <summary>
    /// Message to show when debug GUI text is enabled 
    /// </summary>
    private string message = "";
    /// <summary>
    /// Moving average of UserData samples
    /// </summary>
    private MovingAverage av;
    /// <summary>
    /// Internal skeleton data 
    /// </summary>
    private Skelly sk;
    /// <summary>
    /// Logging object
    /// </summary>
    private NamedLogger nLog;
    /// <summary>
    /// List of all objects that need to be disposed of when the game stops running
    /// </summary>
    private DisposableList disList = new DisposableList();

    void Start()
    {
        av = new MovingAverage(averagingSamples, 0.1f, 300000f, logTrackingData);
        disList.Add(av);

        var logPath = Path.Combine(Application.dataPath, "Logs");

        // Create Logger
        nLog = new NamedLogger(
            new Dictionary<string, string> {
                { "rAngle", "rAngle" },
                { "lAngle", "lAngle" },
                { "rElev", "rElev" },
                { "rHorizX", "rHorizX" },
                { "rHorizZ", "rHorizZ" }
            },
            logTrackingData, logPath);

        // Add logger to list of objects to dispose of
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
        // Release all files
        disList.Dispose();
    }

    /// <summary>
    /// Get UserData from processed Nuitrack skeleton model after internal correction
    /// </summary>
    /// <param name="skel">Raw skeleton from Nuitrack</param>
    /// <returns>
    /// The UserData object computed from the Nuitrack skeleton and internal error correction models
    /// </returns>
    private UserData ProcessSkeleton(nt.Skeleton skel)
    {
        // Log Right Hand position
        nLog.Log("rElev", skel.GetJoint(nt.JointType.RightHand).Real.ToVector3().y);
        nLog.Log("rHorizX", skel.GetJoint(nt.JointType.RightHand).Real.ToVector3().x);
        nLog.Log("rHorizZ", skel.GetJoint(nt.JointType.RightHand).Real.ToVector3().z);

        // Update internal skeleton model
        sk.Update(skel);

        // Create vector from collar to torso
        Vector3 collarTorso = sk[nt.JointType.Torso].pos - sk[nt.JointType.RightCollar].pos;
        // Create vector from collar to shoulder
        Vector3 collarShoulder = sk[nt.JointType.RightShoulder].pos - sk[nt.JointType.RightCollar].pos;

        // Create normal vector for user's chest
        Vector3 bodyPlane = Vector3.Cross(collarTorso, collarShoulder);

        //Create vector from user's left shoulder to their right shoulder
        Vector3 shoulderVector = sk[nt.JointType.RightShoulder].pos - sk[nt.JointType.LeftShoulder].pos;

        // Get coordinates to use for the end of the user's arms based on whether the elbow or hand has higher tracking confidence
        Vector3 rightArmTip = GetPreferredJoint(sk[nt.JointType.RightHand], sk[nt.JointType.RightElbow]);
        Vector3 leftArmTip = GetPreferredJoint(sk[nt.JointType.LeftHand], sk[nt.JointType.LeftElbow]);

        // Create vectors for each arm from shoulder to hand
        Vector3 rightArmVec = rightArmTip - sk[nt.JointType.RightShoulder].pos;
        Vector3 leftArmVec = leftArmTip - sk[nt.JointType.LeftShoulder].pos;

        // Calculate user's arm angles and clip them
        float rightArmAngle = ClipSmallAngle(-Vector3.SignedAngle(rightArmVec, shoulderVector, bodyPlane));
        float leftArmAngle = ClipSmallAngle(Vector3.SignedAngle(leftArmVec, -shoulderVector, bodyPlane));

        // Log arm angles
        nLog.Log("rAngle", rightArmAngle);
        nLog.Log("lAngle", leftArmAngle);

        // Clamp angles to -90 to 90
        leftArmAngle = Mathf.Clamp(leftArmAngle, -90, 90);
        rightArmAngle = Mathf.Clamp(rightArmAngle, -90, 90);

        // Calculate maximum possible arm extensions based on the length of a users upper and lower arms
        float rightArmMaxExtension = Vector3.Magnitude(rightArmTip - sk[nt.JointType.RightElbow].pos) + Vector3.Magnitude(sk[nt.JointType.RightElbow].pos - sk[nt.JointType.RightShoulder].pos);
        float LeftArmMaxExtension = Vector3.Magnitude(leftArmTip - sk[nt.JointType.LeftElbow].pos) + Vector3.Magnitude(sk[nt.JointType.LeftElbow].pos - sk[nt.JointType.LeftShoulder].pos);

        // Calculate current arm extensions as a percent of maximum extension
        float rightArmExtension = Vector3.ProjectOnPlane(rightArmVec, bodyPlane).magnitude / rightArmMaxExtension;
        float leftArmExtension = Vector3.ProjectOnPlane(leftArmVec, bodyPlane).magnitude / LeftArmMaxExtension;

        // Create user data object with all the calculated values
        UserData userDataSample = new UserData(rightArmAngle, rightArmExtension, leftArmAngle, leftArmExtension);

        // Push the new user data object into the moving average
        av.PushSample(userDataSample);

        // Return the moving average of UserData
        return av.GetAverage();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="angle">The angle to clip</param>
    /// <returns>The clipped angle</returns>
    private float ClipSmallAngle(float angle)
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

    struct Joint
    {
        public Vector3 pos;
        public float confidence;

        public Joint(Vector3 pos, float confidence)
        {
            this.pos = pos;
            this.confidence = confidence;
        }
    }

    public struct UserData
    {
        public float rightArmAngle, rightArmExtension, leftArmAngle, leftArmExtension;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rightArmAngle">Angle of the user's right arm in degrees</param>
        /// <param name="rightArmExtension">Extension of the user's right arm as percent of max extension</param>
        /// <param name="leftArmAngle">Angle of the user's left arm in degrees</param>
        /// <param name="leftArmExtension">Extension of the user's left arm as percent of max extension</param>
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

    /// <summary>
    /// Gets position of the joint with the highest confidence
    /// </summary>
    /// <param name="hand">Hand joint</param>
    /// <param name="elbow">Elbow joint</param>
    /// <returns> The position of the joint with the highest confidence (defaulting to hand if tied)</returns>
    private Vector3 GetPreferredJoint(Joint hand, Joint elbow)
    {
        return (hand.confidence >= elbow.confidence) ? hand.pos : elbow.pos;
    }

    private class MovingAverage : IDisposable
    {
        private readonly float interpTime, errorThreshold;

        private int index = 0;
        private readonly int SampleCount;
        private UserData[] Samples;
        private UserData average;
        private bool upToDate = false;

        // Logging related variables
        private readonly bool log;
        private NamedLogger nLog;
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
                nLog = new NamedLogger(
                    new Dictionary<string, string> {
                        { "raLogRaw", "raLogRaw" },
                        { "raAvLog", "raAvLog" }
                    },
                    log, Path.Combine(Application.dataPath, "UserData", "Logs"));

                disList.Add(nLog);
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
                nLog.Log("raLogRaw", sample.rightArmAngle);
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
                nLog.Log("raAvLog", average.rightArmAngle);
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
            this.maxVelocity = 100f * maxVelocity;
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

                    if (log)
                    {
                        Debug.Log("Max joint velocity exceeded, interpolating");
                    }
                }
                else
                {
                    joints[jt] = new Joint(newJPos, newJConf);
                }
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
        private readonly bool enable;

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
                catch (Exception)
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

    /// <summary>
    /// This class enables multiple different values to be logged to different files, simply by passing a name with the log call
    /// </summary>
    class NamedLogger : IDisposable
    {
        private Dictionary<string, FileLogger> loggers = new Dictionary<string, FileLogger>();
        /// <summary>
        /// Whether to enable this logger
        /// </summary>
        private readonly bool enable;
        /// <summary>
        /// Whether to automatically add logs for unrecognized variables
        /// </summary>
        private readonly bool autoAdd;
        /// <summary>
        /// Path to prefix to all output file names
        /// </summary>
        private readonly string prefix;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targets">Dictionary of variables to log and what files to log them in</param>
        /// <param name="enable">Whether to enable logging (true to log, false to skip logging)</param>
        /// <param name="prefix">Path to prefix to all output file names</param>
        /// <param name="autoAdd">Whether to automatically add outputs for unrecognized variables</param>
        public NamedLogger(Dictionary<string, string> targets, bool enable = true, string prefix = "", bool autoAdd = false)
        {
            this.enable = enable;
            this.autoAdd = autoAdd;
            this.prefix = prefix;
            if (enable)
            {
                foreach (var f in targets)
                {
                    AddLogger(f.Key, f.Value);
                }
            }
        }

        /// <summary>
        /// Dispose of this logger
        /// </summary>
        public void Dispose()
        {
            if (enable)
            {
                foreach (var l in loggers)
                {
                    try { l.Value.Dispose(); } catch (Exception) { }
                }
            }
        }

        /// <summary>
        /// Log the given variable
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="value">Value of the variable</param>
        public void Log(string name, float value)
        {
            if (enable)
            {
                if (loggers.ContainsKey(name))
                {
                    loggers[name].Log(value);
                }
                else if (autoAdd)
                {
                    AddLogger(name, "auto_" + name);
                }
                else
                {
                    Debug.Log("Cannot log variable \"" + name + "\", no logger assigned");
                }
            }
        }

        /// <summary>
        /// Adds a logger for another variable
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        /// <param name="fileName">The file to output variable values to</param>
        private void AddLogger(string variableName, string fileName)
        {
            try
            {
                string logFilePath = Path.Combine(prefix, fileName + ".txt");
                loggers[variableName] = new FileLogger(logFilePath, enable);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }

        public FileLogger this[string name]
        {
            get { return loggers[name]; }
        }
    }

    /// <summary>
    /// Simple List that disposes of all its elements when it is disposed of
    /// </summary>
    class DisposableList : List<IDisposable>, IDisposable
    {
        /// <summary>
        /// Dispose of all objects in the list
        /// </summary>
        public void Dispose()
        {
            foreach (var o in this)
            {
                try { o.Dispose(); } catch (Exception) { }
            }
        }
    }
}

