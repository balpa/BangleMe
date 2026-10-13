using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BangleMe
{
    // Camera frame -> hand landmarks -> wrist pose -> forearm fit -> filters -> bracelet stack.
    public sealed class WristTrackingPipeline : MonoBehaviour
    {
        const int BytesPerPixel = 4;
        const int LightSampleStride = 16;
        const int ReflectionRefreshFrames = 4;

        [SerializeField] CameraFeed cameraFeed;
        [SerializeField] ArCameraRig cameraRig;
        [SerializeField] BraceletStack braceletStack;
        [SerializeField] TextAsset handLandmarkerModel;
        [SerializeField] Light keyLight;
        [SerializeField] SceneReflection sceneReflection;

        [Header("Smoothing")]
        [SerializeField] float positionMinCutoff = 5f;
        [SerializeField] float positionBeta = 15f;
        [SerializeField] float rotationMinCutoff = 2f;
        [SerializeField] float rotationBeta = 1.5f;

        [Header("Lighting from the camera image")]
        [SerializeField] float darkSceneReflections = 0.55f;
        [SerializeField] float brightSceneReflections = 1.25f;
        [SerializeField] float keyLightIntensity = 2.6f;
        [Tooltip("Where the light is taken to come from when the arm is evenly lit, in camera space.")]
        [SerializeField] Vector3 assumedLightDirection = new Vector3(0f, 0.5f, -0.85f);
        [Tooltip("Sideways light per unit of brightness difference across the arm.")]
        [SerializeField] float shadingToSideways = 4f;
        [SerializeField] float hardLightBoost = 1.5f;

        readonly HandObservation observation = new HandObservation();
        readonly WristFitEstimator fitEstimator = new WristFitEstimator();
        HandTracker tracker;
        OneEuroFilter filterX, filterY, filterZ;
        RotationFilter rotationFilter;
        MotionPredictor motion;
        TrackingState trackingState;
        WristFit lastFit = new WristFit(Vector3.zero, Quaternion.identity, 0.027f, 0.02f, 2f);
        float sceneBrightness = 0.5f;
        float lightLevel = 1f;
        Vector3 lightSide;
        Vector3 lightDirection = new Vector3(0f, 0.5f, -0.85f).normalized;

        public bool IsTracking => trackingState.Opacity > 0.01f;
        public string Error => cameraFeed.Error;

        void Start()
        {
            tracker = new HandTracker(handLandmarkerModel.bytes, liveStream: true);
            ResetFilters();
        }

        void Update()
        {
            if (!cameraFeed.IsReady)
            {
                braceletStack.Apply(lastFit, false);
                return;
            }

            var image = cameraFeed.Texture;
            cameraRig.Configure(image, cameraFeed.LongSideFovDegrees);
            if (cameraFeed.DidUpdateThisFrame)
            {
                tracker.Submit(image, Time.realtimeSinceStartupAsDouble);
                if (Time.frameCount % ReflectionRefreshFrames == 0) RefreshReflections(image, lastFit);
            }

            if (tracker.TryGetLatest(observation))
            {
                Process(new Vector2(image.width, image.height));
            }

            braceletStack.Gravity = ReadGravity();
            var drawn = motion.Predict(Time.realtimeSinceStartupAsDouble);
            braceletStack.Apply(new WristFit(drawn, lastFit.Rotation, lastFit.HalfWidth, lastFit.HalfThickness, lastFit.GrowthPerMeter), IsTracking);
        }

        void Process(Vector2 imageSize)
        {
            var timestamp = observation.TimestampSeconds;
            var pose = observation.Detected
                ? new WristPoseSolver(cameraRig.FocalLengthPx, imageSize).Solve(observation.ImagePoints, observation.WorldPoints, observation.Confidence)
                : WristPose.Identity;

            if (pose.Confidence <= 0)
            {
                trackingState.Update(false, timestamp);
                // A hand that reappears elsewhere must not be dragged in from the old spot.
                if (!IsTracking)
                {
                    ResetFilters();
                    fitEstimator.ResetPose();
                }
                return;
            }

            var measurement = default(WristMeasurement);
            var measuredFocalLengthPx = cameraRig.FocalLengthPx;
            if (tracker.TryGetFrame(observation.TimestampMs, out var pixels, out var width, out var height))
            {
                measuredFocalLengthPx *= width / imageSize.x;
                var rgba = pixels.AsReadOnlySpan();
                measurement = WristMeasurer.Measure(rgba, width, height, observation.ImagePoints, observation.WorldPoints);
                MatchSceneLighting(rgba, measurement);
            }

            var fit = fitEstimator.Estimate(pose, measurement, timestamp, measuredFocalLengthPx);
            var position = new Vector3(
                filterX.Filter(fit.Position.x, timestamp),
                filterY.Filter(fit.Position.y, timestamp),
                filterZ.Filter(fit.Position.z, timestamp));
            var rotation = rotationFilter.Filter(fit.Rotation, timestamp);

            lastFit = new WristFit(position, rotation, fit.HalfWidth, fit.HalfThickness, fit.GrowthPerMeter);
            motion.Observe(position, timestamp);
            trackingState.Update(true, timestamp);
        }

        // Gold shows whatever surrounds it, so a bracelet lit by a bright studio looks pasted on
        // in a dim room. Scale its lights with how bright the camera image is.
        // blend: how far to move toward this frame's lighting; 1 for a still image.
        public void MatchSceneLighting(ReadOnlySpan<byte> rgba, in WristMeasurement measurement, float blend = 0.1f)
        {
            long sum = 0;
            var samples = 0;
            for (var i = 0; i < rgba.Length; i += BytesPerPixel * LightSampleStride)
            {
                sum += rgba[i] * 3 + rgba[i + 1] * 6 + rgba[i + 2];
                samples++;
            }
            if (samples == 0) return;

            var brightness = sum / (samples * 10f * 255f);
            sceneBrightness = Mathf.Lerp(sceneBrightness, brightness, blend);
            lightLevel = Mathf.Lerp(darkSceneReflections, brightSceneReflections, Mathf.InverseLerp(0.15f, 0.75f, sceneBrightness));

            // The arm is the one object in view whose shape is known: the side of it that is
            // brighter is the side the light comes from. Up and toward the camera are assumed.
            if (measurement.Valid)
            {
                var side = new Vector3(measurement.Shading.x, -measurement.Shading.y, 0f) * shadingToSideways;
                lightSide = Vector3.Lerp(lightSide, Vector3.ClampMagnitude(side, 1f), blend);
            }
            lightDirection = (lightSide + assumedLightDirection.normalized).normalized;

            // A light that shades the arm this clearly is a hard one: sun, a lamp, a window.
            keyLight.intensity = keyLightIntensity * lightLevel * (1f + hardLightBoost * Mathf.Clamp01(lightSide.magnitude));
            keyLight.transform.rotation = Quaternion.LookRotation(-lightDirection);
            braceletStack.LightDirection = lightDirection;
        }

        public void RefreshReflections(Texture image, WristFit fit)
        {
            sceneReflection.Refresh(image, lightLevel, lightDirection, Mathf.Clamp01(lightSide.magnitude), braceletStack.FinishColor, fit.Position, fit.Rotation * Vector3.up, braceletStack.Crowding);
        }

        // Without a sensor (the editor, a desktop camera) the image's own down is the best guess.
        Vector3 ReadGravity()
        {
            var sensor = Accelerometer.current;
            if (sensor == null) return Vector3.down;
            if (!sensor.enabled) InputSystem.EnableDevice(sensor);
            return DeviceGravity.InView(sensor.acceleration.ReadValue(), Screen.orientation, cameraFeed.IsMirrored);
        }

        void ResetFilters()
        {
            filterX = new OneEuroFilter(positionMinCutoff, positionBeta);
            filterY = new OneEuroFilter(positionMinCutoff, positionBeta);
            filterZ = new OneEuroFilter(positionMinCutoff, positionBeta);
            rotationFilter = new RotationFilter(rotationMinCutoff, rotationBeta);
            motion.Reset();
        }

        void OnDestroy()
        {
            tracker?.Dispose();
        }
    }
}
