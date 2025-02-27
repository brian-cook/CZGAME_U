using UnityEngine;

namespace CZ.Core.VFX
{
    /// <summary>
    /// Factory class to create hit particle effects at runtime
    /// </summary>
    public static class HitParticleFactory
    {
        private static ParticleSystem cachedHitParticlesPrefab;

        /// <summary>
        /// Creates a hit particle effect prefab at runtime
        /// </summary>
        /// <returns>A particle system prefab for hit effects</returns>
        public static ParticleSystem CreateHitParticlesPrefab()
        {
            // Return cached prefab if already created
            if (cachedHitParticlesPrefab != null)
                return cachedHitParticlesPrefab;

            // Create a new game object for the particle system
            GameObject particleObject = new GameObject("HitParticlesPrefab");
            particleObject.SetActive(false); // Ensure it's inactive as it's a prefab

            // Add particle system component
            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            
            // Add particle system renderer
            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            
            // Configure main module
            var main = particleSystem.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.3f;
            main.startColor = new Color(1f, 0.3f, 0.3f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;
            
            // Configure emission module
            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[] { 
                new ParticleSystem.Burst(0f, 15) 
            });
            
            // Configure shape module
            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            
            // Configure velocity over lifetime
            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            
            // Configure size over lifetime
            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeOverLifetimeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLifetimeCurve);
            
            // Configure color over lifetime
            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient colorGradient = new Gradient();
            colorGradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0f), 1f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f) 
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);
            
            // Configure renderer
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            
            // Add the hit particle effect component
            particleObject.AddComponent<HitParticleEffect>();
            
            // Cache and return the prefab
            cachedHitParticlesPrefab = particleSystem;
            return particleSystem;
        }
    }
} 