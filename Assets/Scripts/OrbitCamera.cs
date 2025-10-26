using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target to orbit around")]
    public Transform target; 

    [Header("Orbit parameters")]
    public float distance = 0.4f;
    public float azimuthSpeed = 20f;
    public bool autoRotate = true;

    [Header("Elevation (vertical angle)")]
    public float elevationDeg = 20f;  
    public float minElevationDeg = -10f;
    public float maxElevationDeg = 80f;

    [Header("Manual control (mouse)")]
    public bool allowMouseControl = true;
    public float mouseXSensitivity = 100f;
    public float mouseYSensitivity = 80f;
    public float scrollSensitivity = 1.0f;

    private float _azimuthDeg;

    private bool _isDragging = false;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("[OrbitCamera] No target assigned. Please set the brain object center.");
            return;
        }

        Vector3 dir = (transform.position - target.position).normalized;
        Vector3 dirXZ = new Vector3(dir.x, 0f, dir.z);

        if (dirXZ.sqrMagnitude < 1e-6f)
            _azimuthDeg = 0f;
        else
            _azimuthDeg = Mathf.Atan2(dirXZ.z, dirXZ.x) * Mathf.Rad2Deg;

        elevationDeg = Mathf.Asin(dir.y) * Mathf.Rad2Deg;
    }

    void Update()
    {
        if (target == null)
            return;

        HandleInputNewSystem();
        UpdateCameraPose();
    }

    void HandleInputNewSystem()
    {
        if (autoRotate)
        {
            _azimuthDeg += azimuthSpeed * Time.deltaTime;
        }

        if (!allowMouseControl || Mouse.current == null)
        {
            elevationDeg = Mathf.Clamp(elevationDeg, minElevationDeg, maxElevationDeg);
            return;
        }

        bool leftDown   = Mouse.current.leftButton.isPressed;
        bool leftJustUp = Mouse.current.leftButton.wasReleasedThisFrame;

        if (leftDown && !_isDragging)
        {
            _isDragging = true;
        }
        if (leftJustUp)
        {
            _isDragging = false;
        }

        if (_isDragging)
        {
            Vector2 delta = Mouse.current.delta.ReadValue(); // pixels/frame

            _azimuthDeg += delta.x * mouseXSensitivity * Time.deltaTime;

            elevationDeg -= delta.y * mouseYSensitivity * Time.deltaTime;
        }

        Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
        float scrollY = scrollDelta.y;

        if (Mathf.Abs(scrollY) > 0.0001f)
        {
            distance *= Mathf.Pow(0.9f, scrollY * scrollSensitivity * Time.deltaTime);
            distance = Mathf.Clamp(distance, 0.05f, 5f);
        }

        elevationDeg = Mathf.Clamp(elevationDeg, minElevationDeg, maxElevationDeg);
    }

    void UpdateCameraPose()
    {
        float azRad = _azimuthDeg * Mathf.Deg2Rad;
        float elRad = elevationDeg * Mathf.Deg2Rad;

        Vector3 dir;
        dir.x = Mathf.Cos(elRad) * Mathf.Cos(azRad);
        dir.y = Mathf.Sin(elRad);
        dir.z = Mathf.Cos(elRad) * Mathf.Sin(azRad);

        Vector3 camPos = target.position + dir * distance;

        transform.position = camPos;
        transform.LookAt(target.position, Vector3.up);
    }
}
