using UnityEditor;
using UnityEngine;
using PizzaVR.Customers;
using PizzaVR.Core;
using PizzaVR.Gameplay;
using PizzaVR.XR;

// Exercises CustomerSpawner's placement math directly in Edit Mode (no Play Mode, no
// scene, no Input System) so it can be checked quickly and reliably in batch mode.
public static class SpawnLogicSmokeTest
{
    [MenuItem("Tools/Pizza VR/Run Spawn Logic Smoke Test")]
    public static void Run()
    {
        var go = new GameObject("__SpawnLogicSmokeTest");
        var spawner = go.AddComponent<CustomerSpawner>();
        spawner.config = GameBalanceConfigBuilder.EnsureConfig();

        for (int sample = 0; sample < 2; sample++)
        {
            for (int sector = 0; sector < spawner.config.sectorCount; sector++)
            {
                for (int d = 0; d < spawner.config.distanceTiers.Length; d++)
                {
                    spawner.ComputeSpawnPose(sector, d, out var position, out var direction);
                    float radius = new Vector2(position.x, position.z).magnitude;
                    float angle = Mathf.Atan2(position.x, position.z) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;
                    var tier = spawner.config.distanceTiers[d];
                    Debug.Log($"SPAWN_POSE sample={sample} sector={sector} distIdx={d} angle={angle:F1} radius={radius:F2} tierRange=[{tier.minRadius:F1},{tier.maxRadius:F1}] pos=({position.x:F2},{position.y:F2},{position.z:F2})");
                }
            }
        }

        Object.DestroyImmediate(go);

        RunCustomerStateThresholds();
        RunPizzaThrowTest();
        RunFrisbeeAerodynamicsTest();
        RunPeakVelocityTest();
        RunStalledReleaseFallbackTest();
        RunDeliveryEvaluationTest();
        RunThrowBackMarkerTest();
        RunThrowYawCorrectionTest();
        RunAimLinePreviewTest();
        RunDrawPreviewTest();
        RunReleaseVelocityBlendTest();

        Debug.Log("SPAWN_POSE_TEST_DONE");
    }

    static void RunCustomerStateThresholds()
    {
        float[] samples = { 0f, 9.9f, 10f, 24.9f, 25f, 39.9f };
        foreach (var t in samples)
        {
            var state = Customer.ComputeState(t, 10f, 25f);
            Debug.Log($"CUSTOMER_STATE waitTime={t:F1} -> {state}");
        }
    }

    static void RunPizzaThrowTest()
    {
        var parent = new GameObject("__PizzaTestParent");
        var pizzaGO = PrefabUtility.InstantiatePrefab(PizzaPrefabBuilder.EnsurePrefab()) as GameObject;
        pizzaGO.transform.SetParent(parent.transform);
        var projectile = pizzaGO.GetComponent<PizzaProjectile>();
        var rb = pizzaGO.GetComponent<Rigidbody>();

        projectile.SetFlavor(PizzaFlavor.Hawaiian);
        Debug.Log($"PIZZA_TEST before flavor={projectile.Flavor} isKinematic={rb.isKinematic} hasParent={pizzaGO.transform.parent != null}");

        var throwVelocity = new Vector3(1f, 2f, 3f);
        var throwAngular = new Vector3(0f, 5f, 0f);
        projectile.Throw(throwVelocity, throwAngular);

        Debug.Log($"PIZZA_TEST after flavor={projectile.Flavor} isKinematic={rb.isKinematic} hasParent={pizzaGO.transform.parent != null} thrown={projectile.Thrown} velocity={rb.velocity} angularVelocity={rb.angularVelocity}");

        Object.DestroyImmediate(parent);
        Object.DestroyImmediate(pizzaGO);
    }

    static void RunFrisbeeAerodynamicsTest()
    {
        var discNormal = Vector3.up; // disc held flat
        float minStableSpin = 5f;
        float stabilization = 4f;
        float liftCoefficient = 0.15f;

        // Fast spin around the disc's own normal -> should resist tumble strongly (stability ~1).
        var fastSpin = new Vector3(0f, 20f, 0f);
        var tumbleInput = new Vector3(2f, 20f, 0f); // 2 rad/s of "wobble" perpendicular to the spin axis
        var stabilizedFast = PizzaProjectile.ComputeStabilizedAngularVelocity(tumbleInput, discNormal, minStableSpin, stabilization, 0.02f);
        Debug.Log($"FRISBEE_TEST fastSpin tumbleBefore={(tumbleInput - discNormal * Vector3.Dot(tumbleInput, discNormal)).magnitude:F3} tumbleAfter={(stabilizedFast - discNormal * Vector3.Dot(stabilizedFast, discNormal)).magnitude:F3}");

        // Slow/near-zero spin -> stability ~0, tumble should barely be damped.
        var slowSpin = new Vector3(2f, 0.1f, 0f);
        var stabilizedSlow = PizzaProjectile.ComputeStabilizedAngularVelocity(slowSpin, discNormal, minStableSpin, stabilization, 0.02f);
        Debug.Log($"FRISBEE_TEST slowSpin tumbleBefore={(slowSpin - discNormal * Vector3.Dot(slowSpin, discNormal)).magnitude:F3} tumbleAfter={(stabilizedSlow - discNormal * Vector3.Dot(stabilizedSlow, discNormal)).magnitude:F3}");

        // Flat + fast + spinning -> should get meaningful upward lift.
        var liftFlat = PizzaProjectile.ComputeLiftForce(new Vector3(0f, 0f, 5f), fastSpin, discNormal, liftCoefficient, minStableSpin);
        Debug.Log($"FRISBEE_TEST liftFlat={liftFlat}");

        // On its edge (normal sideways, not facing up) -> lift should be ~zero regardless of spin.
        var liftEdge = PizzaProjectile.ComputeLiftForce(new Vector3(0f, 0f, 5f), fastSpin, Vector3.forward, liftCoefficient, minStableSpin);
        Debug.Log($"FRISBEE_TEST liftEdge={liftEdge}");

        // Flat but spin has decayed below minStableSpin -> lift should fade toward zero too.
        var liftSpent = PizzaProjectile.ComputeLiftForce(new Vector3(0f, 0f, 5f), new Vector3(0f, 0.05f, 0f), discNormal, liftCoefficient, minStableSpin);
        Debug.Log($"FRISBEE_TEST liftSpent={liftSpent}");
    }

    static void RunPeakVelocityTest()
    {
        var go = new GameObject("__PeakVelocityTest");
        var input = go.AddComponent<XRControllerInput>();
        input.velocitySampleWindow = 6;
        input.recentDirectionFrames = 3;

        // Simulate a real frisbee-style swing: fast and diagonal mid-swing (peak speed, but
        // sideways), then settling into a straight-forward direction right before release.
        var samples = new[]
        {
            new Vector3(0f, 0, 1f),
            new Vector3(5f, 0, 5f),   // <- peak speed (~7.07), but diagonal/sideways
            new Vector3(1f, 0, 3f),
            new Vector3(0f, 0, 3f),   // <- last 3 frames settle into pure-forward aim
            new Vector3(0f, 0, 2.5f),
            new Vector3(0f, 0, 2f),   // <- release frame
        };
        foreach (var v in samples)
            input.RecordVelocitySample(v, Vector3.zero);

        Debug.Log($"PEAK_VELOCITY_TEST peakSpeed={input.PeakSpeed:F2} (expected ~7.07, from the diagonal mid-swing frame) recentDirection={input.RecentDirection} (expected (0,0,1) - pure forward, not steered by the diagonal peak)");

        Object.DestroyImmediate(go);
    }

    static void RunStalledReleaseFallbackTest()
    {
        var go = new GameObject("__StalledReleaseTest");
        var input = go.AddComponent<XRControllerInput>();
        input.velocitySampleWindow = 6;
        input.recentDirectionFrames = 3;
        input.minRecentSpeedForDirection = 0.15f;

        // A real swing: clear forward peak, then the hand nearly stops for the last few
        // frames right before letting go (settling into release) - this used to make
        // RecentDirection collapse to zero, so every throw just dropped straight down
        // regardless of swing direction.
        var samples = new[]
        {
            new Vector3(0f, 0, 1f),
            new Vector3(0f, 0, 6f),    // <- peak, clearly forward
            new Vector3(0f, 0, 3f),
            new Vector3(0f, 0, 0.05f), // <- last 3 frames: hand basically stopped
            new Vector3(0f, 0, -0.02f),
            new Vector3(0f, 0, 0.01f),
        };
        foreach (var v in samples)
            input.RecordVelocitySample(v, Vector3.zero);

        Debug.Log($"STALLED_RELEASE_TEST peakSpeed={input.PeakSpeed:F2} recentDirection={input.RecentDirection} (expected recentDirection ~(0,0,1) - falls back to the peak's forward direction, NOT (0,0,0))");

        Object.DestroyImmediate(go);
    }

    static void RunDeliveryEvaluationTest()
    {
        var correct = Customer.EvaluateDelivery(PizzaFlavor.Margherita, PizzaFlavor.Margherita, hitPlate: true);
        Debug.Log($"DELIVERY_TEST correctFlavorOnPlate -> {correct} (expected Success)");

        var wrong = Customer.EvaluateDelivery(PizzaFlavor.Pepperoni, PizzaFlavor.Margherita, hitPlate: true);
        Debug.Log($"DELIVERY_TEST wrongFlavorOnPlate -> {wrong} (expected WrongFlavor)");

        var missed = Customer.EvaluateDelivery(PizzaFlavor.Margherita, PizzaFlavor.Margherita, hitPlate: false);
        Debug.Log($"DELIVERY_TEST correctFlavorButHitBody -> {missed} (expected MissedPlate, since aim mattered even though flavor was right)");
    }

    static void RunThrowBackMarkerTest()
    {
        var pizzaGO = PrefabUtility.InstantiatePrefab(PizzaPrefabBuilder.EnsurePrefab()) as GameObject;
        var projectile = pizzaGO.GetComponent<PizzaProjectile>();

        // Mirrors what Customer.ThrowBackPizza does: mark it as a return throw before Throw().
        projectile.SetFlavor(PizzaFlavor.Pepperoni);
        projectile.IsReturnThrow = true;
        projectile.Throw(new Vector3(0f, 0f, -4f), Vector3.zero);

        Debug.Log($"THROWBACK_TEST isReturnThrow={projectile.IsReturnThrow} thrown={projectile.Thrown} (expected both true - PlayerHitDetector relies on IsReturnThrow surviving Throw())");

        Object.DestroyImmediate(pizzaGO);
    }

    static void RunThrowYawCorrectionTest()
    {
        var forward = Vector3.forward;

        var noCorrection = PizzaThrower.ComputeThrowVelocity(forward, 5f, 1f, 0f);
        Debug.Log($"YAW_TEST noCorrection={noCorrection} (expected (0,0,5) - straight forward)");

        var leftCorrection = PizzaThrower.ComputeThrowVelocity(forward, 5f, 1f, -10f);
        Debug.Log($"YAW_TEST leftCorrection(-10deg)={leftCorrection} (expected x < 0 - nudged left)");

        var rightCorrection = PizzaThrower.ComputeThrowVelocity(forward, 5f, 1f, 10f);
        Debug.Log($"YAW_TEST rightCorrection(+10deg)={rightCorrection} (expected x > 0 - nudged right, for comparison)");
    }

    static void RunAimLinePreviewTest()
    {
        var config = GameBalanceConfigBuilder.EnsureConfig();
        var start = Vector3.zero;
        var velocity = new Vector3(0f, 0f, 5f);

        // No spin -> lift is zero (already proven in RunFrisbeeAerodynamicsTest), so this
        // should fall back to plain gravity: y strictly decreasing, z strictly increasing.
        var points = PizzaThrower.SimulatePreview(start, velocity, Vector3.zero, Vector3.up, config,
            config.aimLineSegments, config.aimLineSegmentDuration);

        var first = points[0];
        var last = points[points.Length - 1];
        Debug.Log($"AIMLINE_TEST pointCount={points.Length} first={first} last={last} (expected first=(0,0,0), last.y<0 from gravity with no spin/lift, last.z>0 from forward motion)");
    }

    static void RunDrawPreviewTest()
    {
        var forward = Vector3.forward;

        var noDraw = PizzaThrower.ComputeDrawPreviewVelocity(forward, 0f, 8f, 10f, 0f);
        Debug.Log($"DRAW_TEST noDraw(0m)={noDraw} (expected (0,0,0) - no wind-up yet, no preview)");

        var smallDraw = PizzaThrower.ComputeDrawPreviewVelocity(forward, 0.5f, 8f, 10f, 0f);
        Debug.Log($"DRAW_TEST smallDraw(0.5m)={smallDraw} (expected (0,0,4) - speed scales with draw distance)");

        var bigDraw = PizzaThrower.ComputeDrawPreviewVelocity(forward, 5f, 8f, 10f, 0f);
        Debug.Log($"DRAW_TEST bigDraw(5m)={bigDraw} (expected (0,0,10) - clamped at maxSpeed, not (0,0,40))");
    }

    static void RunReleaseVelocityBlendTest()
    {
        // Big draw, gentle/weak actual swing at release (e.g. relying on the preview instead
        // of really whipping the arm forward) - the throw should still get the drawn power.
        var weakSwing = new Vector3(0f, 0f, 0.3f);
        var bigDraw = new Vector3(0f, 0f, 8f);
        var gentleRelease = PizzaThrower.ComputeReleaseVelocity(weakSwing, bigDraw);
        Debug.Log($"RELEASE_BLEND_TEST weakSwing+bigDraw -> {gentleRelease} (expected (0,0,8) - draw wins, throw isn't weaker than what the aim line promised)");

        // A genuinely fast real swing should still be able to exceed a small/no draw.
        var fastSwing = new Vector3(0f, 0f, 9f);
        var smallDraw = new Vector3(0f, 0f, 1f);
        var realSwingRelease = PizzaThrower.ComputeReleaseVelocity(fastSwing, smallDraw);
        Debug.Log($"RELEASE_BLEND_TEST fastSwing+smallDraw -> {realSwingRelease} (expected (0,0,9) - a real fast swing still wins)");
    }
}
