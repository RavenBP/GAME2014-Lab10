﻿using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cinemachine;

public class PlayerBehaviour : MonoBehaviour
{
    [Header("Controls")]
    public Joystick joystick;
    public float joystickHorizontalSensitivity;
    public float joystickVerticalSensitivity;
    public float horizontalForce;
    public float verticalForce;

    [Header("Platform Detection")]
    public bool isGrounded;
    public bool isJumping;
    public bool isCrouching;
    public Transform spawnPoint;
    public Transform lookAheadPoint;
    public Transform lookInFrontPoint;
    public LayerMask collisionGroundLayer;
    public LayerMask collisionWallLayer;
    public RampDirection rampDirection;
    public bool onRamp;
    public float rampForceSensitivity;

    [Header("Player Abilities")] 
    public int health;
    public int lives;
    public BarController healthBar;
    public Animator livesHUD;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] audioClips;

    [Header("Special FX")]
    public CinemachineVirtualCamera vcam1;
    public CinemachineBasicMultiChannelPerlin perlin;
    public float maxShakeTime = 0.3f;
    public float shakeTimer;
    public float shakeIntensity;
    public bool isCameraShaking;

    private Rigidbody2D m_rigidBody2D;
    private SpriteRenderer m_spriteRenderer;
    private Animator m_animator;
    private RaycastHit2D groundHit;
    private ParticleSystem m_particleSystem;

    // Start is called before the first frame update
    void Start()
    {
        health = 100;
        lives = 3;
        shakeTimer = maxShakeTime;

        m_rigidBody2D = GetComponent<Rigidbody2D>();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_animator = GetComponent<Animator>();
        m_particleSystem = GetComponentInChildren<ParticleSystem>();

        // For screen shake
        vcam1 = FindObjectOfType<CinemachineVirtualCamera>();
        perlin = vcam1.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        _LookInFront();
        _LookAhead();
        _Move();

        if (isCameraShaking == true)
        {
            shakeTimer -= Time.deltaTime;

            if (shakeTimer <= 0.0f)
            {
                perlin.m_AmplitudeGain = 0.0f;
                isCameraShaking = false;
                shakeTimer = maxShakeTime;
            }
        }
    }

    private void _LookInFront()
    {
        if (!isGrounded)
        {
            rampDirection = RampDirection.NONE;
            return;
        }

        var wallHit = Physics2D.Linecast(transform.position, lookInFrontPoint.position, collisionWallLayer);
        if (wallHit && isOnSlope())
        {
            rampDirection = RampDirection.UP;
        }
        else if (!wallHit && isOnSlope())
        {
            rampDirection = RampDirection.DOWN;
        }

        Debug.DrawLine(transform.position, lookInFrontPoint.position, Color.red);
    }

    private void _LookAhead()
    {
        groundHit = Physics2D.Linecast(transform.position, lookAheadPoint.position, collisionGroundLayer);

        isGrounded = (groundHit) ? true : false;

        Debug.DrawLine(transform.position, lookAheadPoint.position, Color.green);
    }

    private bool isOnSlope()
    {
        if (!isGrounded)
        {
            onRamp = false;
            return false;
        }

        if (groundHit.normal != Vector2.up)
        {
            onRamp = true;
            return true;
        }

        onRamp = false;
        return false;
    }

    void _Move()
    {
        if (isGrounded)
        {
            if (!isJumping && !isCrouching)
            {
                if (joystick.Horizontal > joystickHorizontalSensitivity)
                {
                    // move right
                    m_rigidBody2D.AddForce(Vector2.right * horizontalForce * Time.deltaTime);
                    transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                    if (onRamp && rampDirection == RampDirection.UP)
                    {
                        m_rigidBody2D.AddForce(Vector2.up * horizontalForce * rampForceSensitivity * Time.deltaTime);
                    }
                    else if (onRamp && rampDirection == RampDirection.DOWN)
                    {
                        m_rigidBody2D.AddForce(Vector2.down * horizontalForce * rampForceSensitivity * Time.deltaTime);
                    }

                    CreateDustTrail();

                    m_animator.SetInteger("AnimState", (int)PlayerAnimationType.RUN);
                }
                else if (joystick.Horizontal < -joystickHorizontalSensitivity)
                {
                    // move left
                    m_rigidBody2D.AddForce(Vector2.left * horizontalForce * Time.deltaTime);
                    transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
                    if (onRamp && rampDirection == RampDirection.UP)
                    {
                        m_rigidBody2D.AddForce(Vector2.up * horizontalForce * 0.5f * Time.deltaTime);
                    }
                    else if (onRamp && rampDirection == RampDirection.DOWN)
                    {
                        m_rigidBody2D.AddForce(Vector2.down * horizontalForce * 0.5f * Time.deltaTime);
                    }

                    CreateDustTrail();

                    m_animator.SetInteger("AnimState", (int)PlayerAnimationType.RUN);
                }
                else
                {
                    m_animator.SetInteger("AnimState", (int)PlayerAnimationType.IDLE);
                }
            }

            if ((joystick.Vertical > joystickVerticalSensitivity) && (!isJumping))
            {
                // jump
                m_rigidBody2D.AddForce(Vector2.up * verticalForce);
                m_animator.SetInteger("AnimState", (int) PlayerAnimationType.JUMP);
                CreateDustTrail();

                PlayJumpSound();

                isJumping = true;
            }
            else
            {
                isJumping = false;
            }

            if ((joystick.Vertical < -joystickVerticalSensitivity) && (!isCrouching))
            {
                m_animator.SetInteger("AnimState", (int)PlayerAnimationType.CROUCH);
                isCrouching = true;
            }
            else
            {
                isCrouching = false;
            }
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // respawn
        if (other.gameObject.CompareTag("DeathPlane"))
        {
            LoseLife();
        }

        if (other.gameObject.CompareTag("Bullet"))
        {
            TakeDamage(10);
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            TakeDamage(15);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // Delay enemy damage
            if (Time.frameCount % 20 == 0)
            {
                TakeDamage(5);
            }
        }
    }

    public void LoseLife()
    {
        lives -= 1;

        livesHUD.SetInteger("LivesState", lives);

        if (lives > 0)
        {
            health = 100;
            healthBar.SetValue(health);
            transform.position = spawnPoint.position;
        }
        else
        {
            SceneManager.LoadScene("End");
        }
        
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        healthBar.SetValue(health);

        // Plat hit sound
        PlayHitSound();

        // Shake Camera
        ShakeCamera();

        if (health <= 0)
        {
            LoseLife();
        }
    }

    private void CreateDustTrail()
    {
        m_particleSystem.Play();
    }

    private void PlayJumpSound()
    {
        audioSource.clip = audioClips[0];
        audioSource.Play();
    }

    private void PlayHitSound()
    {
        // Using one clip
        audioSource.clip = audioClips[1];
        audioSource.Play();

        // Using multiple clips
        //var randomSound = hitSounds[Random.Range(0, 4)];
        //randomSound.Play();
    }

    private void ShakeCamera()
    {
        perlin.m_AmplitudeGain = shakeIntensity;
        isCameraShaking = true;
    }
}
