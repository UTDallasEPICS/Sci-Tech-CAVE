using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

/// <summary>
/// This class setups up in game cameras that correspond to real world displays
/// specified in a configuration file.
/// </summary>
[ExecuteInEditMode]
public class CameraControl : MonoBehaviour
{
    /// <summary>
    /// The configuration of the displays in the real world
    /// </summary>
    private DisplayConfig displayConfig = new DisplayConfig(), defaultConfig = new DisplayConfig();
    /// <summary>
    /// List of cameras
    /// </summary>
    private List<Camera> cameras = new List<Camera>();
    /// <summary>
    /// Whether to track the User's head. Not currently functional
    /// </summary>
    private bool trackHead = false;
    /// <summary>
    /// Whether to draw debugging vectors in the world
    /// </summary>
    public bool drawDebug = false;

    // Use this for initialization
    /// <summary>
    /// 
    /// </summary>
    void Start()
    {
        defaultConfig = LoadDisplayConfig();

        if (!trackHead)
        {
            displayConfig = defaultConfig;
        }

        DeleteExistingCameras();
        CreateCameras();
        UpdateCameras();
    }

    // Update is called once per frame
    void Update() { }

    /// <summary>
    /// Creates all the necessary camera's, but does not setup their projection matrices
    /// </summary>
    private void CreateCameras()
    {
        for (int i = 0; i < displayConfig.displays.Count; i++)
        {
            // Object to attach the camera to
            GameObject cameraGameObject = new GameObject("Camera_A" + i);
            // Set the parent of the camera's game object to this game object
            cameraGameObject.transform.parent = gameObject.transform;
            // Set the local position to the local origin
            cameraGameObject.transform.localPosition = new Vector3(0, 0, 0);
            // Add camera to the newly created object
            Camera currentCamera = cameraGameObject.AddComponent<Camera>();
            // Set the camera's projection matrix to a default value
            currentCamera.ResetProjectionMatrix();
            // Add camera object to list of cameras
            cameras.Add(currentCamera);
            // Set the target display for the camera
            currentCamera.targetDisplay = i;
        }
    }

    /// <summary>
    /// Deletes all existing cameras created by this script.
    /// </summary>
    private void DeleteExistingCameras()
    {
        int temp = transform.childCount;

        for (int i = 0; i < temp; temp++)
        {
            Destroy(transform.GetChild(0).gameObject);
        }
    }

    /// <summary>
    /// Get number of displays
    /// </summary>
    /// <returns>The number of displays</returns>
    public int NumberOfDisplays()
    {
        return displayConfig.displays.Count;
    }

    /// <summary>
    /// Update existing camera projection matrices based on new display configuration
    /// </summary>
    /// <remarks>
    /// Does not replace or create cameras. 
    /// </remarks>
    void UpdateCameras()
    {
        for (int i = 0; i < displayConfig.displays.Count; i++)
        {
            Camera cam = cameras[i];
            Display d = displayConfig.displays[i];

            cam.transform.localRotation = d.lookRot;

            cam.projectionMatrix = GetProjectionMatrix(d);
        }
    }

    /// <summary>
    /// Calculate the corners of the display (ignoring any rotations)
    /// </summary>
    /// <param name="d">The display to calculate corners for</param>
    /// <returns>The corners of the display in clockwise order staring at the upper right corner</returns>
    static Vector3[] GetDisplayCorners(Display d)
    {
        // Vector representing the offset to a corner of the display
        Vector3 sizeVector = new Vector3(d.width / 2, d.height / 2, 0);

        // Vector with same length as the display's distance from (0,0,0) but lying only in the +z direction
        Vector3 alternateCenterVector = d.centerPosition.magnitude * new Vector3(0, 0, 1);

        // Calculate the position of the corners of the display
        Vector3 upRight = alternateCenterVector + sizeVector;
        Vector3 lowRight = alternateCenterVector + (Vector3.Scale(sizeVector, new Vector3(1, -1, 1)));
        Vector3 lowLeft = alternateCenterVector + (Vector3.Scale(sizeVector, new Vector3(-1, -1, 1)));
        Vector3 upLeft = alternateCenterVector + (Vector3.Scale(sizeVector, new Vector3(-1, 1, 1)));

        // Return the values
        return new Vector3[] { upRight, lowRight, lowLeft, upLeft };
    }

    //TODO: Compute Matrix
    /// <summary>
    /// Get projection matrix for the passed Display
    /// </summary>
    /// <param name="d">The Display to calculate the projection matrix for</param>
    /// <returns>The projection matrix for the passed display</returns>
    Matrix4x4 GetProjectionMatrix(Display d)
    {
        //Planes to project camera corners on to compute FOV targets
        var rotatedHorizontalPlane = (new Vector3(0, 1, 0));
        var rotatedVerticalPlane = (new Vector3(1, 0, 0));

        Vector3[] displayCorners = GetDisplayCorners(d);

        // Display corners projected onto the horizontal plane
        Vector3[] hDisplayCorners = new Vector3[4];

        for (int corner = 0; corner < hDisplayCorners.Length; corner++)
        {
            hDisplayCorners[corner] = Vector3.ProjectOnPlane(displayCorners[corner], rotatedHorizontalPlane);
        }

        // Display corners projected onto the  vertical plane
        Vector3[] vDisplayCorners = new Vector3[4];

        for (int corner = 0; corner < vDisplayCorners.Length; corner++)
        {
            vDisplayCorners[corner] = Vector3.ProjectOnPlane(displayCorners[corner], rotatedVerticalPlane);
        }

        
        float horizontalFOV = System.Math.Max(
            Vector3.Angle(hDisplayCorners[0], hDisplayCorners[3]),
            Vector3.Angle(hDisplayCorners[1], hDisplayCorners[2])
            );

        float verticalFOV = System.Math.Max(
            Vector3.Angle(vDisplayCorners[0], vDisplayCorners[1]),
            Vector3.Angle(vDisplayCorners[2], vDisplayCorners[3])
            );

        if (drawDebug)
        {
            Debug.Log("VertFOVTarget: " + verticalFOV);
            Debug.Log("HorizFOXTarget: " + horizontalFOV);
        }

        //TODO: Clip with display plane to make oblique frustrum
        var projMat = Matrix4x4.Perspective(
            verticalFOV,
            Mathf.Tan(horizontalFOV * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(verticalFOV * 0.5f * Mathf.Deg2Rad),
            0.1f, 1000f
            );


        if (drawDebug)
        {
            for (int i = 0; i < displayCorners.Length; i++)
            {
                Debug.DrawLine(d.lookRot * displayCorners[i], d.lookRot * displayCorners[(i + 1) % 4], Color.blue);
            }

            var planeNormal = -Vector3.Normalize((d.displayRot * d.centerPosition));
            Debug.DrawRay(d.centerPosition, planeNormal, Color.red, 2);
        }

        return projMat;
    }

    // modifies projection matrix in place
    // clipPlane is in camera space
    static void CalculateObliqueMatrix(ref Matrix4x4 projection, Vector4 clipPlane)
    {

        Vector4 q = projection.inverse * new Vector4(
            Mathf.Sign(clipPlane.x),
            Mathf.Sign(clipPlane.y),
            1.0f,
            1.0f
        );

        Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
        // third row = clip plane - fourth row
        projection[2] = c.x - projection[3];
        projection[6] = c.y - projection[7];
        projection[10] = c.z - projection[11];
        projection[14] = c.w - projection[15];
    }

    /// <summary>
    /// Load display configuration. All locations are relative to a preset reference point.
    /// </summary>
    /// <returns>The loaded diplsay configuration</returns>
    static DisplayConfig LoadDisplayConfig()
    {
        string configPath = Path.Combine(Path.Combine(Application.dataPath, "Config"), "DisplayConfig.test.json");
        try
        {
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(Path.Combine(Application.dataPath, "Config"), "DisplayConfig.json");
                Debug.Log("Display testing config not found, attempting to load production config");
            }
            else
            {
                Debug.Log("Display testing config loaded");
            }

            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(Path.Combine(Application.dataPath, "Config"), "DisplayConfig.default.json");
                Debug.Log("Production config not found, loading default config");
            }

            string rawConfig = System.IO.File.ReadAllText(configPath);
            DisplayConfig displayConfig = JsonUtility.FromJson<DisplayConfig>(rawConfig);
            Debug.Log(JsonUtility.ToJson(displayConfig));

            if (UnityEngine.Display.displays.Length < displayConfig.displays.Count)
            {
                Debug.LogWarning("<color=orange>Warning:</color> Number of active displays less than configured. \nAssuming display one is primary.");
            }

            return displayConfig;
        }
        catch (IOException e)
        {
            Debug.LogError("<color=red>Fatal error:</color> Unable to load display config");
            Debug.LogError(e);
            return null;
        }
    }
}



/// <summary>
/// This class contains all the information about a display and ensures it can be serialized/deserialized in a reasonable format.
/// </summary>
[System.Serializable]
public class Display : ISerializationCallbackReceiver
{
    /// <summary>
    /// The direction of the display as a Euluer angle
    /// </summary>
    public Vector3 direction;
    /// <summary>
    /// The height of the display (in meters)
    /// </summary>
    public float height;
    /// <summary>
    /// The positon of the display
    /// </summary>
    public Vector3 centerPosition;
    /// <summary>
    /// The width of the display of (in meters)
    /// </summary>
    public float width;

    /// <summary>
    /// Rotation of the display
    /// </summary>
    [System.NonSerialized] public Quaternion displayRot;
    /// <summary>
    /// Rotation that points in the direction the display is facing
    /// </summary>
    [System.NonSerialized] public Quaternion lookRot;
    /// <summary>
    /// The total rotation (display rotation and looking rotation)
    /// </summary>
    [System.NonSerialized] public Quaternion totalRot;


    public Display(Vector3 centerPosition, Vector3 direction, float width, float height)
    {
        this.centerPosition = centerPosition;
        this.direction = direction;
        this.width = width;
        this.height = height;
    }

    public void OnAfterDeserialize()
    {
        SwapVectorCoords();

        lookRot = Quaternion.LookRotation(centerPosition);
        displayRot = Quaternion.Euler(direction - new Vector3(90, 90, 90));
        totalRot = lookRot * displayRot;
    }

    public void OnBeforeSerialize()
    {
        SwapVectorCoords();
    }

    private void SwapVectorCoords()
    {
        Matrix4x4 coordConversionMat = Matrix4x4.zero; //This matrix is used basically like this matrix
        coordConversionMat[0, 0] = 1f;                 // 1 0 0
        coordConversionMat[1, 2] = 1f;                 // 0 0 1
        coordConversionMat[2, 1] = 1f;                 // 0 1 0
        coordConversionMat[3, 3] = 1f;                 //In effect it just swaps the y and z components

        centerPosition = coordConversionMat.MultiplyPoint3x4(centerPosition);
        direction = coordConversionMat.MultiplyPoint3x4(direction);
    }

    public string Validate()
    {
        string output = "";

        if (direction.x != 90f || direction.y != 90f || direction.z != 90f)
        {
            output += "Displays not pointing directly towards the user are unsupported\n";
        }

        if (height <= 0)
        {
            output += "The height of the display must be greater than zero\n";
        }

        if (width <= 0)
        {
            output += "The width of the display must be greater than zero\n";
        }

        if (output.Equals(""))
        {
            return null;
        }
        else
        {
            return output;
        }
    }

    /// <summary>
    /// Translate this display
    /// </summary>
    /// <param name="translation">Translation to apply</param>
    /// <returns>
    /// New display that has been translated by the given vector
    /// </returns>
    public Display translate(Vector3 translation)
    {
        return new Display(centerPosition + translation, direction, width, height);
    }


}



/// <summary>
/// This class contains all the information about the setup of the displays
/// </summary>
[System.Serializable]
public class DisplayConfig : ISerializationCallbackReceiver
{
    /// <summary>
    /// List of displays
    /// </summary>
    public List<Display> displays;

    public DisplayConfig()
    {
        displays = new List<Display>();
    }

    public DisplayConfig(List<Display> displays)
    {
        this.displays = displays;
    }

    /// <summary>
    /// Translate all displays in this group.
    /// </summary>
    /// <param name="translation">Translation to apply</param>
    /// <returns>
    /// A new DisplayConfig where all displays have been translated by the given vector.
    /// </returns>
    public DisplayConfig translate(Vector3 translation)
    {
        List<Display> ds = new List<Display>(this.displays.Count);

        for (int i = 0; i < ds.Count; i++)
        {
            ds[i] = displays[i].translate(translation);
        }

        return new DisplayConfig(ds);
    }

    public void OnBeforeSerialize() { }

    /// <summary>
    /// Validate all the displays after they have been deserialized from the config file
    /// </summary>
    public void OnAfterDeserialize()
    {
        for (int i = 0; i < displays.Count; i++)
        {
            string validationResult = displays[i].Validate();

            if (validationResult != null)
            {
                Debug.Log("Validation error for display " + (i + 1));
                Debug.Log(validationResult);
            }
        }
    }
    
    public Display this[int i]
    {
        get { return displays[i]; }
        set { displays[i] = value; }
    }
}