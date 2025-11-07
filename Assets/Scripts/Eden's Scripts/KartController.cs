using UnityEngine;

public class KartController : MonoBehaviour
{
    public float accelForce = 20f;
    public float steerSpeed = 90f;
    public Transform camFollow;

    float steer;
    float throttle;
    float brake;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public void SetInput(float steerInput, float throttleInput, float brakeInput)
    {
        steer = Mathf.Clamp(steerInput, -1f, 1f);
        throttle = Mathf.Clamp01(throttleInput);
        brake = Mathf.Clamp01(brakeInput);
    }

    void FixedUpdate()
    {
        float speed = rb.linearVelocity.magnitude;

        float turn = steer * steerSpeed * Mathf.Deg2Rad;
        Vector3 fwd = transform.forward;

        if (speed > 0.1f)
        {
            Quaternion turnRot = Quaternion.Euler(0f, steer * steerSpeed * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turnRot);
        }

        Vector3 force = transform.forward * (throttle - brake) * accelForce;
        rb.AddForce(force, ForceMode.Acceleration);
    }

}
