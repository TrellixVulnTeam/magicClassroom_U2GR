﻿using MagicLeapTools;using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.MagicLeap;

public class Paddle : MonoBehaviour
{
    //Physics variables
    public float strength = 10f;
    private float velocity = 0;
    private Vector3 posLastFrame = Vector3.zero;

    //Feedback variables
    ControlInput cInput = null;

    private void Awake()
    {
        cInput = GameObject.Find("ControlPointer").GetComponent<ControlInput>();
        Assert.IsNotNull(cInput, "Control input could not be found");
    }

    void Start()
    {
        posLastFrame = transform.position;
    }

    void Update()
    {
        velocity = (transform.position - posLastFrame).magnitude;
        posLastFrame = transform.position;
    }

    //Use velocity to calculate the force to apply to the ball on collision
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Ball")
        {
            //Ball physics
            ContactPoint contact = collision.GetContact(0);
            Vector3 force = contact.normal * velocity * strength;
            Debug.Log("velocity: " + velocity.ToString() + "and the force: " + force.ToString());
            collision.collider.attachedRigidbody.AddForce(force, ForceMode.Impulse);

            //Feedback
            cInput.Control.StartFeedbackPatternVibe(MLInput.Controller.FeedbackPatternVibe.Bump, MLInput.Controller.FeedbackIntensity.Medium);
        }
    }
}
