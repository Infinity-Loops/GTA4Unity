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
        return Instance.transform.position;
    }

    private void Update()
    {
        if (target != null)
        {
            transform.position = target.position;
        }
    }
}
