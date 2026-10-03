using System;
using UnityEngine;

public class LandingVisual : MonoBehaviour
{
    [SerializeField] private ParticleSystem leftThrusterParticles;
    [SerializeField] private ParticleSystem middleThrusterParticles;
    [SerializeField] private ParticleSystem rightThrusterParticles;

    private Lander _lander;

    private void Awake ()
    {
        _lander = GetComponent <Lander> ();
        _lander.OnUpForce += Lander_OnUpForce;
        _lander.OnLeftForce += Lander_OnLeftForce;
        _lander.OnRightForce += Lander_OnRightForce;
        _lander.OnBeforeForce += Lander_OnBeforeForce;
        

        SetEnableThrusterParticleSystem (leftThrusterParticles, false);
        SetEnableThrusterParticleSystem (middleThrusterParticles, false);
        SetEnableThrusterParticleSystem (rightThrusterParticles, false);
    }

    private void Lander_OnBeforeForce (object sender, EventArgs eventArgs)
    {
        SetEnableThrusterParticleSystem (leftThrusterParticles, false);
        SetEnableThrusterParticleSystem (middleThrusterParticles, false);
        SetEnableThrusterParticleSystem (rightThrusterParticles, false);
    }
    
    private void Lander_OnUpForce (object sender, EventArgs eventArgs)
    {
        SetEnableThrusterParticleSystem (leftThrusterParticles, true);
        SetEnableThrusterParticleSystem (middleThrusterParticles, true);
        SetEnableThrusterParticleSystem (rightThrusterParticles, true);
    }
    private void Lander_OnLeftForce (object sender, EventArgs eventArgs)
    {
        SetEnableThrusterParticleSystem (leftThrusterParticles, true);
    }
    private void Lander_OnRightForce (object sender, EventArgs eventArgs)
    {
        SetEnableThrusterParticleSystem (rightThrusterParticles, true);
    }

    private void SetEnableThrusterParticleSystem (ParticleSystem particleSystem, bool shouldEnable)
    {
        ParticleSystem.EmissionModule emissionModule = particleSystem.emission;
        emissionModule.enabled = shouldEnable;
    }
}