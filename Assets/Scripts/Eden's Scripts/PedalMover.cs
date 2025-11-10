using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PedalMover : MonoBehaviour
{
    public float baseDeltaV = 2.5f;
    public float minDt = 0.15f;
    public float maxDt = 1.00f;

    public float idleDamping = 1.0f;
    public float driveDamping = 0.15f;

    Rigidbody rb;
    float dampingLerp;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = idleDamping;
    }

    public float ApplyPedalPush(float dt)
    {
        float t = Mathf.Clamp01(Mathf.InverseLerp(maxDt, minDt, dt));
        float deltaV = baseDeltaV * Mathf.Lerp(0.7f, 2.2f, t);

        rb.WakeUp();
        Vector3 fwd = transform.forward.normalized;
        rb.AddForce(fwd * deltaV, ForceMode.VelocityChange);

        dampingLerp = 1f;

        Debug.DrawRay(transform.position + Vector3.up * 0.5f, fwd * 2f, Color.cyan, 0.2f);
        Debug.Log($"Pedal push dt {dt:F2}, deltaV {deltaV:F2}, speed now {rb.linearVelocity.magnitude:F2}");

        return deltaV;
    }

    void FixedUpdate()
    {
        dampingLerp = Mathf.MoveTowards(dampingLerp, 0f, Time.fixedDeltaTime * 2f);
        float d = Mathf.Lerp(idleDamping, driveDamping, dampingLerp);
        rb.linearDamping = d;

        if (rb.linearVelocity.sqrMagnitude < 0.0004f)
            rb.linearVelocity = Vector3.zero;
    }
}
