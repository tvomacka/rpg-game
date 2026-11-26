using System;
using UnityEngine;

namespace RpgGame.CameraControl
{
    /// <summary>Min/max orthographic size and how much one scroll notch changes it.</summary>
    [Serializable]
    public struct ZoomLimits
    {
        [Min(0.01f)] public float MinSize;
        [Min(0.01f)] public float MaxSize;
        [Min(0.01f)] public float Step;

        public ZoomLimits(float minSize, float maxSize, float step)
        {
            if (minSize <= 0f) throw new ArgumentOutOfRangeException(nameof(minSize), "Min size must be positive.");
            if (maxSize < minSize) throw new ArgumentException("Max size must be >= min size.", nameof(maxSize));
            if (step <= 0f) throw new ArgumentOutOfRangeException(nameof(step), "Step must be positive.");
            MinSize = minSize;
            MaxSize = maxSize;
            Step = step;
        }

        public float Clamp(float size) => Mathf.Clamp(size, MinSize, MaxSize);
    }

    /// <summary>Pure zoom maths for an orthographic camera: no Unity objects, EditMode-testable.</summary>
    public static class OrthoZoom
    {
        /// <summary>
        /// New target size after a scroll. Only the sign of <paramref name="scrollDelta"/> matters
        /// (one notch = one step), because raw wheel values differ per device and OS.
        /// Scrolling up (positive) zooms in, i.e. makes the orthographic size smaller.
        /// </summary>
        public static float ApplyScroll(float currentSize, float scrollDelta, ZoomLimits limits)
        {
            float next = currentSize - Math.Sign(scrollDelta) * limits.Step;
            return limits.Clamp(next);
        }

        /// <summary>
        /// Frame-rate independent exponential approach from <paramref name="current"/> to <paramref name="target"/>.
        /// </summary>
        public static float Smooth(float current, float target, float sharpness, float deltaTime)
        {
            if (sharpness <= 0f || deltaTime <= 0f) return sharpness <= 0f ? target : current;
            float t = 1f - Mathf.Exp(-sharpness * deltaTime);
            return Mathf.Lerp(current, target, t);
        }
    }
}
