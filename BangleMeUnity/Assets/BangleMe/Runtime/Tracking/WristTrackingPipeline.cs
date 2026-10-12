using System;
using UnityEngine;

namespace BangleMe
{
    // Camera frame -> hand landmarks -> wrist pose -> forearm fit -> filters -> bracelet stack.
    public sealed class WristTrackingPipeline : MonoBehaviour
    {
        const int BytesPerPixel = 4;
        const int LightSampleStride = 16;

        [SerializeField] CameraFeed cameraFeed;
        [SerializeField] ArCameraRig cameraRig;
        [SerializeField] BraceletStack braceletStack;
        [SerializeField] TextAsset handLandmarkerModel;
        [SerializeField] Light keyLight;

        [Header("Smoothing")]
        [SerializeField] float positionMinCutoff = 1.5f;
        [SerializeField] float positionBeta = 15f;
        [SerializeField] float rotationMinCutoff = 2f;
        [SerializeField] float rotationBeta = 1.5f;

        [Header("Lighting from the camera image")]
        [SerializeField] float darkSceneReflections = 0.55f;
        [SerializeField] float brightSceneReflections = 1.25f;
        [SerializeField] float keyLightIntensity = 1.2f;

        readonly HandObservation observation = new HandObservation();
        readonly WristFitEstimator fitEstimator = new WristFitEstimator();
        HandTracker tracker;
        OneEuroFilter filterX, filterY, filterZ;
        RotationFilter rotationFilter;
        TrackingState trackingState;
        WristFit lastFit = new WristFit(Vector3.zero, Quaternion.identity, 0.027f, 0.02f, 2f);
        float sceneBrightness = 0.5f;

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
            cameraRig.Configure(image);
            if (cameraFeed.DidUpdateThisFrame)
            {
                tracker.Submit(image, Time.realtimeSinceStartupAsDouble);
            }

            if (tracker.TryGetLatest(observation))
            {
                Process(new Vector2(image.width, image.height));
            }

            braceletStack.Apply(lastFit, IsTracking);
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
            if (tracker.TryGetFrame(observation.TimestampMs, out var pixels, out var width, out var height))
            {
                var rgba = pixels.AsReadOnlySpan();
                measurement = WristMeasurer.Measure(rgba, width, height, observation.ImagePoints, observation.WorldPoints);
                MatchSceneLighting(rgba);
            }

            var fit = fitEstimator.Estimate(pose, measurement, timestamp);
            var position = new Vector3(
                filterX.Filter(fit.Position.x, timestamp),
                filterY.Filter(fit.Position.y, timestamp),
                filterZ.Filter(fit.Position.z, timestamp));
            var rotation = rotationFilter.Filter(fit.Rotation, timestamp);

            lastFit = new WristFit(position, rotation, fit.HalfWidth, fit.HalfThickness, fit.GrowthPerMeter);
            trackingState.Update(true, timestamp);
        }

        // Gold shows whatever surrounds it, so a bracelet lit by a bright studio looks pasted on
        // in a dim room. Scale its reflections with how bright the camera image is.
        void MatchSceneLighting(ReadOnlySpan<byte> rgba)
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
            sceneBrightness = Mathf.Lerp(sceneBrightness, brightness, 0.1f);
            var level = Mathf.Lerp(darkSceneReflections, brightSceneReflections, Mathf.InverseLerp(0.15f, 0.75f, sceneBrightness));
            RenderSettings.reflectionIntensity = level;
            keyLight.intensity = keyLightIntensity * level;
        }

        void ResetFilters()
        {
            filterX = new OneEuroFilter(positionMinCutoff, positionBeta);
            filterY = new OneEuroFilter(positionMinCutoff, positionBeta);
            filterZ = new OneEuroFilter(positionMinCutoff, positionBeta);
            rotationFilter = new RotationFilter(rotationMinCutoff, rotationBeta);
        }

        void OnDestroy()
        {
            tracker?.Dispose();
        }
    }
}
