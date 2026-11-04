using HarmonyLib;
using UnityEngine;

namespace HalfGripSlapPack
{
    // Run after the game's final hand placement, including hover/hold transition sprites. Changing
    // only handIndicatorCameraOffset misses the idle hand and cached transition positions.
    [HarmonyPatch(typeof(Grabber), "LateUpdate")]
    internal static class MirrorHandDepth
    {
        [HarmonyPostfix]
        private static void Postfix(Grabber __instance)
        {
            if (AdjustableMirror.Active) AdjustableMirror.Active.KeepHandInFront(__instance);
        }

        internal static void KeepInFront(Camera camera, SpriteRenderer[] mirrors, SpriteRenderer[] hands)
        {
            bool found = false;
            Rect mirrorRect = default;
            float nearestMirror = float.PositiveInfinity;
            foreach (SpriteRenderer mirror in mirrors)
            {
                if (!Visible(mirror) || !ProjectBounds(camera, mirror.bounds, out Rect rect)) continue;
                DepthRange(camera, mirror.bounds, out float near, out float far);
                nearestMirror = Mathf.Min(nearestMirror, near);
                mirrorRect = found ? Rect.MinMaxRect(Mathf.Min(mirrorRect.xMin, rect.xMin),
                    Mathf.Min(mirrorRect.yMin, rect.yMin), Mathf.Max(mirrorRect.xMax, rect.xMax),
                    Mathf.Max(mirrorRect.yMax, rect.yMax)) : rect;
                found = true;
            }
            if (!found) return;
            foreach (SpriteRenderer hand in hands)
            {
                if (!Visible(hand) || !ProjectBounds(camera, hand.bounds, out Rect rect) || !rect.Overlaps(mirrorRect)) continue;
                DepthRange(camera, hand.bounds, out float near, out float far);
                if (far <= nearestMirror - HandDepth.Clearance) continue;
                Vector3 screen = camera.WorldToViewportPoint(hand.transform.position);
                if (screen.z <= camera.nearClipPlane) continue;
                // Pull along the viewing ray: the hand stays at the same screen location, and the
                // entire camera-facing sprite clears even the nearest edge of the tilted mirror.
                screen.z = HandDepth.InFrontOfMirror(screen.z, far, nearestMirror, camera.nearClipPlane);
                hand.transform.position = camera.ViewportToWorldPoint(screen);
            }
        }

        private static bool Visible(SpriteRenderer sprite) => sprite && sprite.enabled && sprite.gameObject.activeInHierarchy;

        private static void DepthRange(Camera camera, Bounds bounds, out float near, out float far)
        {
            Vector3 forward = camera.transform.forward;
            float center = Vector3.Dot(bounds.center - camera.transform.position, forward);
            Vector3 extents = bounds.extents;
            float radius = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.y) * extents.y + Mathf.Abs(forward.z) * extents.z;
            near = center - radius;
            far = center + radius;
        }

        private static bool ProjectBounds(Camera camera, Bounds bounds, out Rect rect)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 screen = camera.WorldToViewportPoint(corner);
                if (screen.z <= 0) { rect = default; return false; }
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }
    }
}
