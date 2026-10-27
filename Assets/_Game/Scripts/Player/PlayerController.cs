using System.Collections;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>First-person controller. GameDirector decides when movement and look are allowed.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera cam;
        public Transform head;
        public float walkSpeed = 2.1f;
        [Tooltip("Degrees per pixel of mouse movement.")]
        public float sensitivity = 0.11f;
        public float fieldOfView = 68f;
        public float bobAmount = 0.022f;

        public bool MoveEnabled { get; set; }
        public bool LookEnabled { get; set; }
        public bool InView => viewAnchor != null;

        CharacterController cc;
        float yaw, pitch, vy, bobT, stepDistance;
        Vector3 headBase;
        Transform viewAnchor;
        float viewYaw, viewPitch;
        Vector2 viewLimits;
        float shakeTime, shakeDuration, shakeMagnitude;
        bool scripted;

        public const string SensitivityKey = "da_sensitivity";

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            headBase = head.localPosition;
            yaw = transform.eulerAngles.y;
            sensitivity = PlayerPrefs.GetFloat(SensitivityKey, sensitivity);
            cam.fieldOfView = fieldOfView;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (viewAnchor != null)
            {
                if (LookEnabled)
                {
                    var d = GameInput.Look * sensitivity * 0.45f;
                    viewYaw = Mathf.Clamp(viewYaw + d.x, -viewLimits.x, viewLimits.x);
                    viewPitch = Mathf.Clamp(viewPitch - d.y, -viewLimits.y, viewLimits.y);
                }
                cam.transform.localRotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
                return;
            }

            if (scripted) return;

            if (LookEnabled)
            {
                var d = GameInput.Look * sensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -82f, 82f);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector3 move = Vector3.zero;
            if (MoveEnabled)
            {
                var m = GameInput.Move;
                move = (transform.right * m.x + transform.forward * m.y) * walkSpeed;
            }
            if (cc.isGrounded && vy < 0f) vy = -1f;
            else vy += Physics.gravity.y * dt;
            cc.Move((move + Vector3.up * vy) * dt);

            var flat = cc.velocity; flat.y = 0f;
            float speed = flat.magnitude;
            if (speed > 0.2f)
            {
                bobT += dt * speed * 3.4f;
                stepDistance += speed * dt;
                if (stepDistance > 0.75f)
                {
                    stepDistance = 0f;
                    if (AudioManager.Instance != null) AudioManager.Instance.Footstep(transform.position);
                }
            }
            else
            {
                bobT = Mathf.Lerp(bobT, Mathf.Round(bobT / Mathf.PI) * Mathf.PI, dt * 5f);
            }
            head.localPosition = headBase + new Vector3(0f, Mathf.Sin(bobT * 2f) * bobAmount * Mathf.Clamp01(speed), 0f);
        }

        void LateUpdate()
        {
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                float k = Mathf.Clamp01(shakeTime / Mathf.Max(0.01f, shakeDuration));
                cam.transform.localPosition = Random.insideUnitSphere * shakeMagnitude * k;
            }
            else if (cam.transform.localPosition != Vector3.zero)
            {
                cam.transform.localPosition = Vector3.zero;
            }
        }

        public void Teleport(Vector3 position, float newYaw, float newPitch = 0f)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;
            yaw = newYaw;
            pitch = newPitch;
            vy = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Snap the camera into a fixed view (the peephole) with a little free look.</summary>
        public void EnterView(Transform anchor, float fov, Vector2 limits)
        {
            viewAnchor = anchor;
            viewYaw = viewPitch = 0f;
            viewLimits = limits;
            cam.transform.SetParent(anchor, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = fov;
        }

        public void ExitView()
        {
            viewAnchor = null;
            cam.transform.SetParent(head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = fieldOfView;
        }

        /// <summary>Scripted walk/turn used by cutscenes.</summary>
        public IEnumerator MoveTo(Vector3 position, float targetYaw, float targetPitch, float duration)
        {
            scripted = true;
            cc.enabled = false;
            Vector3 startPos = transform.position;
            float startYaw = yaw, startPitch = pitch;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                float k = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(startPos, position, k);
                yaw = Mathf.LerpAngle(startYaw, targetYaw, k);
                pitch = Mathf.Lerp(startPitch, targetPitch, k);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                yield return null;
            }
            cc.enabled = true;
            scripted = false;
        }

        /// <summary>Recover from an interrupted cutscene (scripted move, shake).</summary>
        public void ResetState()
        {
            StopAllCoroutines();
            scripted = false;
            if (cc != null) cc.enabled = true;
            shakeTime = 0f;
            if (cam != null) cam.transform.localPosition = Vector3.zero;
        }

        public void Shake(float magnitude, float duration)
        {
            if (shakeTime > 0f && magnitude < shakeMagnitude) return;
            shakeMagnitude = magnitude;
            shakeDuration = duration;
            shakeTime = duration;
        }

        public void SetSensitivity(float value)
        {
            sensitivity = value;
            PlayerPrefs.SetFloat(SensitivityKey, value);
        }
    }
}
