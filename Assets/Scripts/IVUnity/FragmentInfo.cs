using UnityEngine;
using RageLib.Models.Resource;

/// <summary>
/// Stores information about a fragment child for physics and damage system
/// </summary>
public class FragmentInfo : MonoBehaviour
{
    // Fragment identification
    public int childIndex;
    public FragTypeModel parentFragModel;
    
    // Physics properties
    public float mass = 1.0f;
    public float damagedMass = 1.0f;
    
    // Damage properties
    public byte health = 100;
    public byte minDamageForce = 10;
    
    // Bone attachment
    public ushort boneId = 0xFFFF; // 0xFFFF means not attached to bone
    
    // Runtime state
    public bool isDamaged = false;
    public float currentHealth;
    
    void Start()
    {
        currentHealth = health;
        
        // Set up physics if not attached to bone
        if (boneId == 0xFFFF)
        {
            // Add Rigidbody for physics simulation
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            
            rb.mass = mass;
            rb.isKinematic = true; // Start kinematic, enable physics when damaged
        }
    }
    
    /// <summary>
    /// Apply damage to this fragment
    /// </summary>
    public void ApplyDamage(float damageAmount, Vector3 hitPoint, Vector3 hitForce)
    {
        if (isDamaged) return;
        
        // Check if force exceeds minimum threshold
        if (hitForce.magnitude < minDamageForce) return;
        
        currentHealth -= damageAmount;
        
        if (currentHealth <= 0)
        {
            BreakFragment(hitPoint, hitForce);
        }
    }
    
    /// <summary>
    /// Break this fragment piece
    /// </summary>
    void BreakFragment(Vector3 hitPoint, Vector3 hitForce)
    {
        isDamaged = true;
        
        // Enable physics
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.mass = damagedMass;
            
            // Apply break force
            rb.AddForceAtPosition(hitForce, hitPoint, ForceMode.Impulse);
            
            // Add some torque for rotation
            rb.AddTorque(Random.insideUnitSphere * hitForce.magnitude * 0.1f, ForceMode.Impulse);
        }
        
        // Detach from parent to allow free movement
        transform.parent = null;
        
        // Optional: Destroy after some time
        Destroy(gameObject, 30f);
    }
    
    /// <summary>
    /// Get the local transform from the fragment model
    /// </summary>
    public Matrix4x4 GetLocalTransform()
    {
        // The reverted FragTypeModel doesn't have transform data
        // Return the GameObject's local transform instead
        if (transform != null)
        {
            return Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
        }
        return Matrix4x4.identity;
    }
    
    /// <summary>
    /// Check if this fragment is attached to a skeleton bone
    /// </summary>
    public bool IsBoneAttached()
    {
        return boneId != 0xFFFF;
    }
}