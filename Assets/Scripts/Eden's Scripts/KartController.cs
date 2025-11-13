using UnityEngine;

public class KartController : MonoBehaviour
{
    public float rotateSpeed = 120f;
    float yawInput;
    public Animator animator;

    public void SetYawInput(float v)
    {
        yawInput = Mathf.Clamp(v, -1f, 1f);
    }

    void Update()
    {
        if (Mathf.Abs(yawInput) > 0.0001f)
        {
            transform.Rotate(0f, yawInput * rotateSpeed * Time.deltaTime, 0f, Space.World);
        }

        if (yawInput < 0f)
        {
            animator.SetInteger("Int", 1);
        }
        else if (yawInput > 0f)
        {
            animator.SetInteger("Int", 2);
        }
        else
        {
            animator.SetInteger("Int", 0);
        }
    }
}
