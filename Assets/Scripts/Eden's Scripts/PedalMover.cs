using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PedalMover : MonoBehaviour
{
    [Header("Pedal strength")]
    public float baseDeltaV = 1.2f;        // core speed gain per valid pedal
    public float minDt = 0.20f;            // fastest typical alternation
    public float maxDt = 1.00f;            // slowest alternation that still counts

    [Header("Damping")]
    public float idleDamping = 1.2f;       // coasting damping
    public float driveDamping = 0.35f;     // damping right after a push
    public float driveDampingHold = 0.5f;  // seconds to blend back to idle

    [Header("Speed limit")]
    public float maxForwardSpeed = 6.0f;   // firm ceiling
    public float softCapStartRatio = 0.75f;
    public float softCapCurve = 2.0f;

    private Rigidbody rb;
    private float dampingTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        rb.linearDamping = idleDamping;
        if (rb.angularDamping < 0.05f) rb.angularDamping = 0.05f;
    }

    public float ApplyPedalPush(float dt)
    {
        if (dt <= 0f) dt = 0.001f;

        // cadence factor between 0 and 1
        float t = Mathf.Clamp01(Mathf.InverseLerp(maxDt, minDt, dt));
        // mild gain range based on cadence
        float cadenceMul = Mathf.Lerp(0.9f, 1.5f, t);

        // forward component of current speed
        Vector3 fwd = transform.forward.normalized;
        float vFwd = Vector3.Dot(rb.linearVelocity, fwd);

        // soft cap scale from current speed
        float startSpeed = maxForwardSpeed * Mathf.Clamp01(softCapStartRatio);
        float room = Mathf.Max(0f, maxForwardSpeed - Mathf.Max(vFwd, 0f));

        float softRatio;
        if (vFwd <= startSpeed)
        {
            softRatio = 1f;
        }
        else
        {
            float range = Mathf.Max(0.001f, maxForwardSpeed - startSpeed);
            float near = Mathf.Clamp01((maxForwardSpeed - vFwd) / range); // 1 far, 0 at max
            softRatio = Mathf.Pow(near, Mathf.Max(0.5f, softCapCurve));
        }

        float desiredDeltaV = baseDeltaV * cadenceMul * softRatio;
        float appliedDeltaV = Mathf.Min(desiredDeltaV, room);

        if (appliedDeltaV > 0f)
        {
            rb.WakeUp();
            rb.AddForce(fwd * appliedDeltaV, ForceMode.VelocityChange);
            dampingTimer = driveDampingHold;
        }

        ClampForwardSpeed();
        return appliedDeltaV;
    }

    void FixedUpdate()
    {
        // blend damping from drive back to idle
        float target = dampingTimer > 0f ? driveDamping : idleDamping;
        float current = rb.linearDamping;
        rb.linearDamping = Mathf.MoveTowards(current, target, Time.fixedDeltaTime * 4f);
        if (dampingTimer > 0f) dampingTimer -= Time.fixedDeltaTime;

        // tiny velocity cleanup
        if (rb.linearVelocity.sqrMagnitude < 0.0004f)
            rb.linearVelocity = Vector3.zero;

        // enforce hard cap each physics step
        ClampForwardSpeed();
    }

    void ClampForwardSpeed()
    {
        Vector3 fwd = transform.forward;
        Vector3 v = rb.linearVelocity;
        float vFwd = Vector3.Dot(v, fwd);

        if (vFwd > maxForwardSpeed)
        {
            Vector3 lateral = v - fwd * vFwd;
            rb.linearVelocity = lateral + fwd * maxForwardSpeed;
        }
    }
}
