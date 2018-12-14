using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using nt = nuitrack;

/// <summary>
/// This class process the skeleton data from Nuitrack and provides that data to the rest of the game.
/// </summary>
public class SensorInterface : MonoBehaviour
{
    // Editor Controls
    /// <summary>
    /// Whether to show user tracking status and arm angle onscreen
    /// </summary>
    [Tooltip("Show basic debug information")]
    public bool showDebugGUI = false;
    /// <summary>
    /// Whether to remove the small deadzone angles near 0 degrees 
    /// </summary>
    [Tooltip("Whether to remove small angles near 0")]
    public bool clipSmallAngles = true;
    /// <summary>
    /// Whether to log detailed tracking data
    /// </summary>
    [Tooltip("Whether to log detailed tracking data to a file")]
    public bool logTrackingData = false;
    /// <summary>
    /// Angles below this threshold will be considered small for the purposes of filtering
    /// </summary>
    [Tooltip("The range of angles near 0 degrees to remove")]
    public float smallAngleThreshold = 9f;
    /// <summary>
    /// Maximum allowed joint velocity (in m/s)
    /// </summary>
    [Tooltip("The maximum joint velocity allowed to occur (in m/s)")]
    public float maxJointVelocity = 10f;
    /// <summary>
    /// Number of samples to use in the moving average for the UserData
    /// </summary>
    [Tooltip("The size of the moving average window")]
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
    private ISkeleton sk;
    /// <summary>
    /// Logging object
    /// </summary>
    private NamedLogger nLog;
    /// <summary>
    /// List of all objects that need to be disposed of when the game stops running
    /// </summary>
    private DisposableList disList = new DisposableList();

    /// <summary>
    /// Initialize tracking system. Called once on game startup.
    /// </summary>
    void Start()
    {
        av = new MovingAverage(averagingSamples, logTrackingData);
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

    /// <summary>
    /// Update tracking data. Called once per game tick.
    /// </summary>
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

    /// <summary>
    /// Draw GUI elements
    /// </summary>
    void OnGUI()
    {
        if (showDebugGUI)
        {
            GUI.color = Color.red;
            GUI.skin.label.fontSize = 50;
            GUILayout.Label(message);
        }
    }

    /// <summary>
    /// Cleanup logging file handles. Called once when game stops.
    /// </summary>
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
    /// Clip out the small angle deadzone if enabled
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

    /// <summary>
    /// Simple struct storing joint positon and tracking confidence
    /// </summary>
    struct Joint
    {
        /// <summary>
        /// The position of the joint in world space (in units of cm)
        /// </summary>
        public Vector3 pos;
        /// <summary>
        /// The confidence in the joint position
        /// </summary>
        public float confidence;
        /// <summary>
        /// The type of joint this is
        /// </summary>
        public nt.JointType jointType;

        /// <summary>
        /// Creates a new joint with the given values
        /// </summary>
        /// <param name="pos">Joint position</param>
        /// <param name="confidence">Tracking confidence</param>
        /// <param name="jointType">Which joint this is</param>
        public Joint(Vector3 pos, float confidence, nt.JointType jointType = nt.JointType.None)
        {
            this.pos = pos;
            this.confidence = confidence;
            this.jointType = jointType;
        }
    }

    /// <summary>
    /// A simple struct to store user tracking data (arm angles and extensions)
    /// </summary>
    public struct UserData
    {
        /// <summary>
        /// The user's right arm angle in degrees
        /// </summary>
        public float rightArmAngle;
        /// <summary>
        /// The user's right arm extensions as a percent of maximum extension
        /// </summary>
        public float rightArmExtension;
        /// <summary>
        /// The user's left arm angle in degrees
        /// </summary>
        public float leftArmAngle;
        /// <summary>
        /// The user's left arm extension as a percent of maximum extension
        /// </summary>
        public float leftArmExtension;

        /// <summary>
        /// Construct a UserData object with the provided information
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

        /// <summary>
        /// Add the values from another UserData object to this object
        /// </summary>
        /// <param name="a">The UserData object to add</param>
        public void Add(UserData a)
        {
            rightArmAngle += a.rightArmAngle;
            rightArmExtension += a.rightArmExtension;
            leftArmAngle += a.leftArmAngle;
            leftArmExtension += a.leftArmExtension;
        }

        /// <summary>
        /// Divide this objects values by the passed value
        /// </summary>
        /// <param name="a">The value to divide by</param>
        public void Divide(int a)
        {
            rightArmAngle /= a;
            rightArmExtension /= a;
            leftArmAngle /= a;
            leftArmExtension /= a;
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

    /// <summary>
    /// This class implements a moving average of UserData objects over the specified period
    /// </summary>
    private class MovingAverage : IDisposable
    {
        /// <summary>
        /// Position of the oldest sample
        /// </summary>
        private int index = 0;
        /// <summary>
        /// Period of the moving average
        /// </summary>
        private readonly int SampleCount;
        /// <summary>
        /// Moving average sample data
        /// </summary>
        private UserData[] Samples;
        /// <summary>
        /// Computed average
        /// </summary>
        private UserData average;
        /// <summary>
        /// Whether the average needs to be recalculated
        /// </summary>
        private bool upToDate = false;

        // Logging related variables
        /// <summary>
        /// Whether logging is enabled
        /// </summary>
        private readonly bool log;
        /// <summary>
        /// The logger
        /// </summary>
        private NamedLogger nLog;
        /// <summary>
        /// List of all objects that need to be disposed of
        /// </summary>
        private DisposableList disList = new DisposableList();

        /// <summary>
        /// Constructs a MovingAverage object with the specified period with a flag to control logging
        /// </summary>
        /// <param name="sampleCount">Moving average period</param>
        /// <param name="log">Whether to log data</param>
        public MovingAverage(int sampleCount, bool log = false)
        {
            SampleCount = sampleCount;
            Samples = new UserData[SampleCount];

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

        /// <summary>
        /// Add a new sample to rolling average
        /// </summary>
        /// <param name="sample">The sample to add</param>
        public void PushSample(UserData sample)
        {
            // Replace oldest sample
            Samples[index] = sample;

            // Update position of oldest sample
            index = (index + 1) % Samples.Length;

            // Set flag to indicate that the average needs to be recalculated
            upToDate = false;

            // Log the raw value if logging is enabled
            if (log)
            {
                nLog.Log("raLogRaw", sample.rightArmAngle);
            }
        }

        /// <summary>
        /// Get the rolling average
        /// </summary>
        /// <returns>The current average</returns>
        public UserData GetAverage()
        {
            // Recalculate the average if necessary
            if (!upToDate)
            {
                average = new UserData(0, 0, 0, 0);

                // Add each sample
                foreach (var a in Samples)
                {
                    average.Add(a);
                }

                // Divide by number of samples
                average.Divide(SampleCount);

                // Log the average if enabled
                if (log)
                {
                    nLog.Log("raAvLog", average.rightArmAngle);
                }

            }

            // Return the average
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

    /// <summary>
    /// Skeleton model that prevents joints from moving to quickly and interpolates their location if they do.
    /// </summary>
    class Skelly : ISkeleton
    {
        /// <summary>
        /// The tracked skeleton joints
        /// </summary>
        private Dictionary<nt.JointType, Joint> joints;
        /// <summary>
        /// The maximum allowed joint velocity (in cm/s)
        /// </summary>
        private float maxVelocity;
        /// <summary>
        /// Whether to log tracking data
        /// </summary>
        private bool log;

        /// <summary>
        /// Construct a new Skeleton model with the given parameters
        /// </summary>
        /// <param name="maxVelocity">Maximum allowed joint velocity (in m/s)</param>
        /// <param name="log">Whether to log tracking data</param>
        public Skelly(float maxVelocity, bool log = false)
        {
            this.maxVelocity = 100f * maxVelocity;
            this.log = log;

            joints = new Dictionary<nt.JointType, Joint>();

            foreach (nuitrack.JointType jt in (nuitrack.JointType[])System.Enum.GetValues(typeof(nuitrack.JointType)))
            {
                joints[jt] = new Joint(new Vector3(0, 0, 0), 0f, jt);
            }
        }

        public Joint this[nt.JointType j]
        {
            get { return joints[j]; }
            set { joints[j] = value; }
        }

        /// <summary>
        /// Update the skeleton model based on new data from Nuitrack
        /// </summary>
        /// <param name="newData">The new skeleton data</param>
        /// <returns>Indicator if interpolation was performed</returns>
        public bool Update(nt.Skeleton newData)
        {
            bool error = false;

            foreach (nuitrack.JointType jt in (nuitrack.JointType[])System.Enum.GetValues(typeof(nuitrack.JointType)))
            {
                //New position of the joint being processed
                Vector3 newJPos = newData.GetJoint(jt).Real.ToVector3();
                //Confidence of the new joint being looked at
                float newJConf = newData.GetJoint(jt).Confidence;

                if (DistanceTraveled(joints[jt], newJPos) > maxVelocity * Time.deltaTime)
                {
                    joints[jt] = new Joint(joints[jt].pos + Vector3.Normalize(newJPos - joints[jt].pos) * maxVelocity * Time.deltaTime, newJConf, jt);
                    error = true;

                    if (log)
                    {
                        Debug.Log("Max joint velocity exceeded, interpolating");
                    }
                }
                else
                {
                    joints[jt] = new Joint(newJPos, newJConf, jt);
                }
            }

            return error;
        }

        public Dictionary<nt.JointType, Joint> GetValue()
        {
            return new Dictionary<nt.JointType, Joint>(joints);
        }

        /// <summary>
        /// Calculate distance traveled between two joint positions
        /// </summary>
        /// <param name="a">The current joint from the skeleton model</param>
        /// <param name="b">The new joint position from Nuitrack</param>
        /// <returns>The distance between the two joint positions</returns>
        private static float DistanceTraveled(Joint a, Vector3 b)
        {
            return Vector3.Magnitude(b - a.pos);
        }
    }

    /// <summary>
    /// This class provides the ability to log floating point data into a file
    /// </summary>
    class FileLogger : IDisposable
    {
        /// <summary>
        /// The output file stream
        /// </summary>
        private StreamWriter stream;
        /// <summary>
        /// Whether this logger is enabled
        /// </summary>
        private readonly bool enable;

        /// <summary>
        /// Construct a logger that outputs to the given file
        /// </summary>
        /// <param name="file">The output log file</param>
        /// <param name="enable">Whether this logger is enabled</param>
        public FileLogger(string file, bool enable = true)
        {
            this.enable = enable;
            if (enable)
            {
                try
                {
                    (new FileInfo(file)).Directory.Create(); // Create all the necessary directories
                    // Create the output stream
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

        /// <summary>
        /// Dispose of the object
        /// </summary>
        public void Dispose()
        {
            if (enable)
            {
                // Release the file stream
                stream.Dispose();
            }
        }

        /// <summary>
        /// Log a value
        /// </summary>
        /// <param name="f">The value to log</param>
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

        /// <summary>
        /// Get the logger associated with the given variable
        /// </summary>
        /// <param name="name">Variable name</param>
        /// <returns>The logger associated with the given variable</returns>
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

    /// <summary>
    /// Simple interface that all skeleton objects must satisfy for them to function
    /// </summary>
    interface ISkeleton
    {
        /// <summary>
        /// Update the internal skeleton model
        /// </summary>
        /// <param name="skel">Raw data sample from Nuitrack</param>
        /// <returns>Boolean indicating whether joint interpolation/error correction was necessary</returns>
        bool Update(nt.Skeleton skel);

        /// <summary>
        /// Get the given joint
        /// </summary>
        /// <param name="j">The type of joint to get</param>
        /// <returns>The requested joint</returns>
        Joint this[nt.JointType j]
        {
            get;
        }

        /// <summary>
        /// Get a full copy of the skeleton data
        /// </summary>
        /// <returns>Dictionary with all the skeleton joints</returns>
        Dictionary<nt.JointType, Joint> GetValue();
    }

    /// <summary>
    /// WIP class that would determine if a joint movement was valid by measuring it's veloity relative to it's parent joint.
    /// This class is not complete as it was deemed uneccesary at the time of this software's development.
    /// However, if later it is deemed necessary to have a more accurate internal skeleton model this provides a starting point.
    /// </summary>
    class HighAccuracySkeleton : ISkeleton
    {
        private Dictionary<nt.JointType, Joint> joints;
        private Dictionary<nt.JointType, nt.JointType> jointParents;
        private Dictionary<nt.JointType, float> maxJointSpeeds;

        public HighAccuracySkeleton()
        {

        }

        public HighAccuracySkeleton(Dictionary<nt.JointType, float> maxJointSpeeds)
        {

        }

        public Joint this[nt.JointType t]
        {
            get { return joints[t]; }
        }

        public Dictionary<nt.JointType, Joint> GetValue()
        {
            throw new NotImplementedException();
        }

        public bool Update(nt.Skeleton skel)
        {
            throw new NotImplementedException();
        }
    }
}

