using System;
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace BangleMe
{
    // Runs MediaPipe's hand landmarker on an upright camera texture.
    public sealed class HandTracker : IDisposable
    {
        const int BytesPerPixel = 4;
        // MediaPipe reads the pixels on its own thread after Submit returns, so frames rotate
        // through a few buffers instead of reusing one that may still be in flight.
        const int FrameBufferCount = 4;

        readonly HandLandmarker landmarker;
        readonly int maxImageSide;
        readonly object gate = new object();
        readonly HandObservation pending = new HandObservation();
        bool hasPending;

        RenderTexture detectionTexture;
        NativeArray<byte>[] frameBuffers;
        readonly long[] frameTimestamps = new long[FrameBufferCount];
        int frameWidth, frameHeight;
        int nextFrameBuffer;
        bool readbackInFlight;
        long lastTimestampMs = -1;
        bool disposed;

        public HandTracker(byte[] modelData, bool liveStream, int maxImageSide = 512)
        {
            this.maxImageSide = maxImageSide;
            var options = new HandLandmarkerOptions(
                new BaseOptions(BaseOptions.Delegate.CPU, modelAssetBuffer: modelData),
                runningMode: liveStream ? RunningMode.LIVE_STREAM : RunningMode.IMAGE,
                numHands: 1,
                resultCallback: liveStream ? OnResult : null);
            landmarker = HandLandmarker.CreateFromOptions(options);
        }

        // Queues the frame for detection; the result arrives later through TryGetLatest.
        public void Submit(Texture uprightImage, double timestampSeconds)
        {
            if (disposed || readbackInFlight) return;

            var timestampMs = Math.Max((long)(timestampSeconds * 1000.0), lastTimestampMs + 1);
            lastTimestampMs = timestampMs;

            RenderDetectionTexture(uprightImage);
            readbackInFlight = true;
            AsyncGPUReadback.Request(detectionTexture, 0, TextureFormat.RGBA32, request =>
            {
                readbackInFlight = false;
                if (disposed || request.hasError) return;
                landmarker.DetectAsync(BuildImage(request, timestampMs), timestampMs);
            });
        }

        public bool TryGetLatest(HandObservation destination)
        {
            lock (gate)
            {
                if (!hasPending) return false;
                destination.CopyFrom(pending);
                hasPending = false;
                return true;
            }
        }

        // The pixels detection ran on for the observation with this timestamp: RGBA, rows top-down.
        // Only valid until the next few frames are submitted.
        public bool TryGetFrame(long timestampMs, out NativeArray<byte> pixels, out int width, out int height)
        {
            pixels = default;
            width = frameWidth;
            height = frameHeight;
            if (frameBuffers == null) return false;

            var index = Array.IndexOf(frameTimestamps, timestampMs);
            if (index < 0) return false;
            pixels = frameBuffers[index];
            return true;
        }

        // Blocking variant for still images and tests.
        public bool Detect(Texture uprightImage, HandObservation destination)
        {
            RenderDetectionTexture(uprightImage);
            var request = AsyncGPUReadback.Request(detectionTexture, 0, TextureFormat.RGBA32);
            request.WaitForCompletion();
            if (request.hasError) return false;

            var result = landmarker.Detect(BuildImage(request, 0));
            Fill(destination, result, 0);
            return destination.Detected;
        }

        // Readback returns rows bottom-up while MediaPipe expects the top row first, so the
        // downscaled copy is rendered vertically flipped.
        void RenderDetectionTexture(Texture uprightImage)
        {
            var scale = Mathf.Min(1f, maxImageSide / (float)Mathf.Max(uprightImage.width, uprightImage.height));
            var width = Mathf.Max(1, Mathf.RoundToInt(uprightImage.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(uprightImage.height * scale));

            if (detectionTexture == null || detectionTexture.width != width || detectionTexture.height != height)
            {
                ReleaseDetectionTexture();
                detectionTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "HandDetection"
                };
                detectionTexture.Create();
            }

            Graphics.Blit(uprightImage, detectionTexture, new Vector2(1, -1), new Vector2(0, 1));
        }

        Image BuildImage(AsyncGPUReadbackRequest request, long timestampMs)
        {
            var data = request.GetData<byte>();
            if (frameBuffers == null || frameBuffers[0].Length != data.Length)
            {
                DisposeFrameBuffers();
                frameBuffers = new NativeArray<byte>[FrameBufferCount];
                for (var i = 0; i < FrameBufferCount; i++)
                {
                    frameBuffers[i] = new NativeArray<byte>(data.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    frameTimestamps[i] = -1;
                }
            }

            frameWidth = request.width;
            frameHeight = request.height;
            frameTimestamps[nextFrameBuffer] = timestampMs;
            var buffer = frameBuffers[nextFrameBuffer];
            nextFrameBuffer = (nextFrameBuffer + 1) % FrameBufferCount;
            buffer.CopyFrom(data);
            return new Image(ImageFormat.Types.Format.Srgba, request.width, request.height, request.width * BytesPerPixel, buffer);
        }

        // Called on a MediaPipe thread.
        void OnResult(HandLandmarkerResult result, Image image, long timestampMs)
        {
            lock (gate)
            {
                Fill(pending, result, timestampMs);
                hasPending = true;
            }
        }

        static void Fill(HandObservation observation, HandLandmarkerResult result, long timestampMs)
        {
            observation.TimestampMs = timestampMs;
            observation.Detected = false;
            observation.Confidence = 0;

            if (result.handLandmarks == null || result.handLandmarks.Count == 0) return;
            if (result.handWorldLandmarks == null || result.handWorldLandmarks.Count == 0) return;

            var imageLandmarks = result.handLandmarks[0].landmarks;
            var worldLandmarks = result.handWorldLandmarks[0].landmarks;
            if (imageLandmarks.Count < HandLandmark.Count || worldLandmarks.Count < HandLandmark.Count) return;

            for (var i = 0; i < HandLandmark.Count; i++)
            {
                observation.ImagePoints[i] = new Vector2(imageLandmarks[i].x, imageLandmarks[i].y);
                observation.WorldPoints[i] = new Vector3(worldLandmarks[i].x, worldLandmarks[i].y, worldLandmarks[i].z);
            }

            observation.Detected = true;
            observation.Confidence = result.handedness != null && result.handedness.Count > 0 && result.handedness[0].categories.Count > 0
                ? result.handedness[0].categories[0].score
                : 1f;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            // Close drains MediaPipe's queue, after which nothing reads the frame buffers.
            landmarker.Close();
            ((IDisposable)landmarker).Dispose();
            AsyncGPUReadback.WaitAllRequests();
            DisposeFrameBuffers();
            ReleaseDetectionTexture();
        }

        void DisposeFrameBuffers()
        {
            if (frameBuffers == null) return;
            foreach (var buffer in frameBuffers)
            {
                if (buffer.IsCreated) buffer.Dispose();
            }
            frameBuffers = null;
        }

        void ReleaseDetectionTexture()
        {
            if (detectionTexture == null) return;
            detectionTexture.Release();
            UnityEngine.Object.DestroyImmediate(detectionTexture);
            detectionTexture = null;
        }
    }
}
