using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;
using DG.Tweening;
using UnityEngine.Rendering.PostProcessing;
using System;

public class ShooterController : MonoBehaviour
{
    private MovementInput input;
    private Animator anim;

    [Header("Cinemachine")]
    public CinemachineFreeLook thirdPersonCam;
    private CinemachineImpulseSource impulse;
    private PostProcessVolume postVolume;
    private PostProcessProfile postProfile;
    private ColorGrading colorG;
    public Color deadEyeColor;
    private Color currentColor = Color.white;

    [Space]

    [Header("Booleans")]
    public bool aiming = false;
    public bool deadeye = false;

    [Space]

    [Header("Settings")]
    private float originalZoom;
    public float originalOffsetAmount;
    public float zoomOffsetAmount;
    public float aimTime;

    [Header("Targets")]
    public List<Transform> targets = new List<Transform>();
    [Space]

    [Header("UI")]
    public GameObject aimPrefab;
    public List<Transform> crossList = new List<Transform>();
    public Transform canvas;
    public Image reticle;

    [Space]

    [Header("Gun")]
    public Transform gun;
    private Vector3 gunIdlePos;
    private Vector3 gunIdleRot;
    private Vector3 gunAimPos = new Vector3(0.3273799f, -0.03389892f, -0.08808608f);
    private Vector3 gunAimRot = new Vector3(-1.763f, -266.143f, -263.152f);

    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animator>();
        input = GetComponent<MovementInput>();
        originalZoom = thirdPersonCam.m_Orbits[1].m_Radius;
        impulse = thirdPersonCam.GetComponent<CinemachineImpulseSource>();
        postVolume = Camera.main.GetComponent<PostProcessVolume>();
        postProfile = postVolume.profile;
        colorG = postProfile.GetSetting<ColorGrading>();

        gunIdlePos = gun.localPosition;
        gunIdleRot = gun.localEulerAngles;

        Cursor.visible = false;

        HorizontalOffset(originalOffsetAmount);
    }

    // Update is called once per frame
    void Update()
    {
        //Red cross positioning
        if (aiming)
        {
            if (targets.Count > 0)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    crossList[i].position = Camera.main.WorldToScreenPoint(targets[i].position);
                }
            }
        }

        //Dead Eye Animation Block
        if (deadeye)
            return;

        anim.SetFloat("speed", input.Speed);

        if(!aiming)
            WeaponPosition();

        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
        }

        if (Input.GetMouseButtonDown(1) && !deadeye)
        {
            Aim(true);
        }

        if (Input.GetMouseButtonUp(1) && aiming)
        {
            if (targets.Count > 0)
            {

                DeadEye(true);

                Sequence s = DOTween.Sequence();
                for (int i = 0; i < targets.Count; i++)
                {
                    s.Append(transform.DOLookAt(targets[i].GetComponentInParent<EnemyScript>().transform.position, .05f).SetUpdate(true));
                    s.AppendCallback(() => anim.SetTrigger("fire"));
                    int x = i;
                    s.AppendInterval(.05f);
                    s.AppendCallback(()=>FirePolish());
                    s.AppendCallback(() => targets[x].GetComponentInParent<EnemyScript>().Ragdoll(true, targets[x]));
                    s.AppendCallback(() => crossList[x].GetComponent<Image>().color = Color.clear);
                    s.AppendInterval(.35f);
                }
                s.AppendCallback(() => Aim(false));
                s.AppendCallback(() => DeadEye(false));
            }
            else
            {

                Aim(false);
            }
        }

        if (aiming)
        {
            input.LookAt(Camera.main.transform.forward + (Camera.main.transform.right * .1f));

            RaycastHit hit;
            Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit);

            if (!deadeye)
                reticle.color = Color.white;

            if (hit.transform == null)
                return;

            if (!hit.collider.CompareTag("Enemy"))
                return;

            reticle.color = Color.red;

            if (!targets.Contains(hit.transform) && !hit.transform.GetComponentInParent<EnemyScript>().aimed)
            {
                hit.transform.GetComponentInParent<EnemyScript>().aimed = true;
                targets.Add(hit.transform);

                Vector3 convertedPos = Camera.main.WorldToScreenPoint(hit.transform.position);
                GameObject cross = Instantiate(aimPrefab, canvas);
                cross.transform.position = convertedPos;
                crossList.Add(cross.transform);
            }
        }
    }

    private void WeaponPosition()
    {
        bool state = input.Speed > 0;

        Vector3 pos = state ? gunAimPos : gunIdlePos;
        Vector3 rot = state ? gunAimRot : gunIdleRot;
        gun.DOLocalMove(pos, .3f);
        gun.DOLocalRotate(rot, .3f);
    }

    private void FixedUpdate()
    {
        colorG.colorFilter.value = Color.Lerp(colorG.colorFilter.value, currentColor, .2f);
    }

    private void FirePolish()
    {
        impulse.GenerateImpulse();

        foreach(ParticleSystem p in gun.GetComponentsInChildren<ParticleSystem>())
        {
            p.Play();
        }
    }

    public void DeadEye(bool state)
    {
        deadeye = state;
        //thirdPersonCam.enabled = !state;
        float animationSpeed = state ? 2 : 1;
        anim.speed = animationSpeed;

        if (state)
        {
            reticle.DOColor(Color.clear, .05f);
        }

        if (!state)
        {
            targets.Clear();

            foreach (Transform t in crossList)
            {
                Destroy(t.gameObject);
            }
            crossList.Clear();

        }

        input.enabled = !state;
    }

    public void Aim(bool state)
    {
        aiming = state;

        float xOrigOffset = state ? originalOffsetAmount : zoomOffsetAmount;
        float xCurrentOffset = state ? zoomOffsetAmount : originalOffsetAmount;
        float yOrigOffset = state ? 1.5f : 1.5f -.1f;
        float yCurrentOffset = state ? 1.5f -.1f : 1.5f;
        float zoom = state ? 20 : 30;

        DOVirtual.Float(xOrigOffset, xCurrentOffset, aimTime, HorizontalOffset);
        DOVirtual.Float(thirdPersonCam.m_Lens.FieldOfView, zoom, aimTime, CameraZoom);

        anim.SetBool("aiming", state);

        float timeScale = state ? .3f : 1;
        float origTimeScale = state ? 1 : .3f;
        DOVirtual.Float(origTimeScale, timeScale, .2f, SetTimeScale);

        if (state == false)
            transform.DORotate(new Vector3(0, transform.eulerAngles.y, transform.eulerAngles.z), .2f);

        Vector3 pos = state ? gunAimPos : gunIdlePos;
        Vector3 rot = state ? gunAimRot : gunIdleRot;
        gun.DOComplete();
        gun.DOLocalMove(pos, .1f);
        gun.DOLocalRotate(rot, .1f);

        //Polish

        float origChromatic = state ? 0 : .4f;
        float newChromatic = state ? .4f : 0;
        float origVignette = state ? 0 : .4f;
        float newVignette = state ? .4f : 0;
        currentColor = state ? deadEyeColor : Color.white;

        DOVirtual.Float(origChromatic, newChromatic, .1f, AberrationAmount);
        DOVirtual.Float(origChromatic, newChromatic, .1f, VignetteAmount);

        Color c = state ? Color.white : Color.clear;
        reticle.color = c;

    }

    void CameraZoom(float x)
    {
        thirdPersonCam.m_Lens.FieldOfView = x;
    }

    void HorizontalOffset(float x)
    {
        for (int i = 0; i < 3; i++)
        {
            CinemachineComposer c = thirdPersonCam.GetRig(i).GetCinemachineComponent<CinemachineComposer>();
            c.m_TrackedObjectOffset.x = x;
        }
    }

    void VerticalOffset(float y)
    {
        for (int i = 0; i < 3; i++)
        {
            CinemachineComposer c = thirdPersonCam.GetRig(i).GetCinemachineComponent<CinemachineComposer>();
            c.m_TrackedObjectOffset.y = y;
        }
    }

    void SetTimeScale(float x)
    {
        Time.timeScale = x;
    }

    void ColorFilter(Color c)
    {
        postProfile.GetSetting<ColorGrading>().colorFilter.value = c;
    }

    void AberrationAmount(float x)
    {
        postProfile.GetSetting<ChromaticAberration>().intensity.value = x;
    }

    void VignetteAmount(float x)
    {
        postProfile.GetSetting<Vignette>().intensity.value = x;
    }
}
