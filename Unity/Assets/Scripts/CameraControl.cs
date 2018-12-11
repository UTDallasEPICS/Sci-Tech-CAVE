using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class CameraControl : MonoBehaviour
{
    DisplayConfig displayConfig = new DisplayConfig(), defaultConfig = new DisplayConfig();
    List<Camera> cameras = new List<Camera>();
    bool trackHead = false;
    public static bool drawDebug = false;

    // Use this for initialization
    void Start()
    {
        defaultConfig = LoadDisplayConfig();

        if (!trackHead)
        {
            displayConfig = defaultConfig;
        }

        CreateCameras();
        UpdateCameras();
    }

    // Update is called once per frame
    void Update()
    {
        //if (!trackHead)
        //{
        //    return;
        //}

        //Vector3 translationVec = new Vector3(); //TODO: Obtain info from tracking SDK
        //displayConfig = defaultConfig.translate(translationVec);
        UpdateCameras();
    }

    void CreateCameras()
    {
        //var baseCamera = GameObject.FindWithTag("MainCamera");


        for (int i = 0; i < displayConfig.displays.Count; i++)
        {
            var cameraGameObject = new GameObject("Camera_A" + i);
            cameraGameObject.transform.parent = gameObject.transform;
            //cameraGameObject.transform.Translate(gameObject.transform.transform.position);
			cameraGameObject.transform.localPosition = new Vector3(0, 0, 0);
            var camera = cameraGameObject.AddComponent<Camera>();
            camera.ResetProjectionMatrix();
            cameras.Add(camera);
            camera.targetDisplay = i;

        }
    }

    /// <summary>
    /// Update existing camera projection matrices based on new display configuration
    /// </summary>
    /// <remarks>
    /// Experimental. Does not replace or create cameras. 
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

    static Vector3[] GetDisplayCorners(Display d)
    {
        Vector3 sizeVector = new Vector3(d.width / 2, d.height / 2, 0);

        var upRight = d.centerPosition + d.totalRot * sizeVector;
        var lowRight = d.centerPosition + d.totalRot * (Vector3.Scale(sizeVector, new Vector3(1, -1, 1)));
        var lowLeft = d.centerPosition + d.totalRot * (Vector3.Scale(sizeVector, new Vector3(-1, -1, 1)));
        var upLeft = d.centerPosition + d.totalRot * (Vector3.Scale(sizeVector, new Vector3(-1, 1, 1)));

        return new Vector3[] { upRight, lowRight, lowLeft, upLeft };
    }

    //TODO: Compute Matrix
    static Matrix4x4 GetProjectionMatrix(Display d)
    {
        //Vector representing the offset to a corner of the display
        Vector3 sizeVector = new Vector3(d.width / 2, d.height / 2, 0);

        //Planes to project camera corners on the compute FOV targets
        var rotatedHorizontalPlane = d.totalRot * (new Vector3(0, 1, 0));
        var rotatedVerticalPlane = d.totalRot * (new Vector3(1, 0, 0));


        Vector3[] displayCorners = GetDisplayCorners(d);

        Vector3[] hDisplayCorners = new Vector3[4];

        for (int corner = 0; corner < hDisplayCorners.Length; corner++)
        {
            hDisplayCorners[corner] = Vector3.ProjectOnPlane(displayCorners[corner], rotatedHorizontalPlane);
        }

        Vector3[] vDisplayCorners = new Vector3[4];

        for (int corner = 0; corner < vDisplayCorners.Length; corner++)
        {
            vDisplayCorners[corner] = Vector3.ProjectOnPlane(displayCorners[corner], rotatedVerticalPlane);
        }

        float horizFOVTarget = System.Math.Max(
            Vector3.Angle(hDisplayCorners[0], hDisplayCorners[3]),
            Vector3.Angle(hDisplayCorners[1], hDisplayCorners[2])
            );

        float vertFOVTarget = System.Math.Max(
            Vector3.Angle(vDisplayCorners[0], vDisplayCorners[1]),
            Vector3.Angle(vDisplayCorners[2], vDisplayCorners[3])
            );

        if (drawDebug)
        {
            Debug.Log("VertFOVTarget: " + vertFOVTarget);
            Debug.Log("HorizFOXTarget: " + horizFOVTarget);
        }

        //TODO: Clip with display plane to make oblique frustrum
        var projMat = Matrix4x4.Perspective(vertFOVTarget, Mathf.Tan(vertFOVTarget * 0.5f) / Mathf.Tan(horizFOVTarget * 0.5f), 0.1f, 1000f);
        var planeNormal = -Vector3.Normalize((d.displayRot * d.centerPosition));

        if (drawDebug)
        {
            for (int i = 0; i < displayCorners.Length; i++)
            {
                Debug.DrawLine(displayCorners[i], displayCorners[(i + 1) % 4], Color.blue);
            }
            Debug.DrawRay(d.centerPosition, planeNormal, Color.red, 2);
        }


        planeNormal = projMat * planeNormal;
        var newCenter = projMat * d.centerPosition;

        if (drawDebug)
        {
            Debug.DrawRay(d.centerPosition, planeNormal);
        }

        //var plane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, -Vector3.Dot(planeNormal, newCenter));
        //CalculateObliqueMatrix(ref projMat, plane);
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

    static Matrix4x4 GetDefaultProjectionMatrix(Display d)
    {
        return new Matrix4x4();
    }

    /// <summary>
    /// Load display configuration. All locations are relative to a preset reference point.
    /// </summary>
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




[System.Serializable]
public class Display : ISerializationCallbackReceiver
{
    public Vector3 direction;
    public float height;
    public Vector3 centerPosition;
    public float width;
    [System.NonSerialized] public Quaternion displayRot;
    [System.NonSerialized] public Quaternion lookRot;
    [System.NonSerialized] public Quaternion totalRot;


    public Display(Vector3 _centerPosition, Vector3 _direction, float _width, float _height)
    {
        centerPosition = _centerPosition;
        direction = _direction;
        width = _width;
        height = _height;
    }

    public void OnAfterDeserialize()
    {
        SwapVectorCoords();

        displayRot = Quaternion.Euler(direction - new Vector3(90, 90, 90));
        lookRot = Quaternion.LookRotation(centerPosition);
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




[System.Serializable]
public class DisplayConfig
{
    public List<Display> displays;

    public DisplayConfig()
    {
        displays = new List<Display>();
    }

    public DisplayConfig(List<Display> _displays)
    {
        displays = _displays;
    }

    public override bool Equals(object obj)
    {
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public override string ToString()
    {
        return displays.Count.ToString();
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

    public Display this[int i]
    {
        get { return displays[i]; }
        set { displays[i] = value; }
    }
}