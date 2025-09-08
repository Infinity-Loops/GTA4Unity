using UnityEngine;

public class FocusPoint : MonoBehaviour
{
    public static FocusPoint Instance;

    private Transform target;
    private void Awake()
    {
        Instance = this;
    }

    public static void SetTarget(Transform target)
    {
        Instance.target = target;
    }

    public static Vector3 GetPosition()
    {
        if (Instance == null) return Vector3.zero;
        
        // If we have a target, use its position directly (don't wait for Update)
        if (Instance.target != null)
        {
            return Instance.target.position;
        }
        
        return Instance.transform.position;
    }
    
    public static void SetPosition(Vector3 position)
    {
        if (Instance != null)
        {
            Instance.transform.position = position;
            Debug.Log($"FocusPoint position set to {position}");
        }
    }

    private void Update()
    {
        if (target != null)
        {
            transform.position = target.position;
        }
    }
}
