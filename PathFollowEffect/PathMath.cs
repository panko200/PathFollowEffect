using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;

namespace PathFollowEffect
{
    /// <summary>
    /// パス上の位置と角度の計算結果
    /// </summary>
    internal struct PathPositionResult
    {
        public Vector2 Position;
        public float AngleRadians;
        public bool IsValid;
    }

    /// <summary>
    /// パス計算ユーティリティ
    /// 直線、二次ベジェ、三次ベジェの長さ・位置・接線角度を計算する
    /// </summary>
    internal static class PathMath
    {
        // ─────────────────────────────────────────────────────
        //  パスの総長を計算
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// パスの種類に応じた総長を計算する
        /// </summary>
        public static float CalculatePathLength(PathType pathType, PathPointCache[] caches)
        {
            switch (pathType)
            {
                case PathType.CubicBezier:
                    if (caches.All(c => c is CubicBezierPathPointCache))
                    {
                        var beziers = caches.Cast<CubicBezierPathPointCache>().ToArray();
                        var segments = CubicBezierPathPointCache.ToBezierSegments(beziers, out var start);
                        return CalculateCubicBezierLength(segments, start);
                    }
                    goto case PathType.Straight;

                case PathType.QuadraticBezier:
                    if (caches.Length >= 2)
                    {
                        var segments = PathPointCache.ToOpenQuadraticBezierSegments(caches, out var start);
                        return CalculateQuadraticBezierLength(segments, start);
                    }
                    goto case PathType.Straight;

                case PathType.Straight:
                default:
                    var points = PathPointCache.ToLinearLinePoints(caches);
                    return CalculateLinearLength(points);
            }
        }

        /// <summary>
        /// パス上の指定位置（0.0〜1.0）の座標と接線角度を取得する
        /// </summary>
        public static PathPositionResult GetPositionOnPath(
            PathType pathType, PathPointCache[] caches, float t)
        {
            float totalLength = CalculatePathLength(pathType, caches);
            float position = t * totalLength;

            switch (pathType)
            {
                case PathType.CubicBezier:
                    if (caches.All(c => c is CubicBezierPathPointCache))
                    {
                        var beziers = caches.Cast<CubicBezierPathPointCache>().ToArray();
                        var segments = CubicBezierPathPointCache.ToBezierSegments(beziers, out var start);
                        if (TryGetCubicBezierPositionAndAngle(segments, start, position, out var pos, out var angle))
                            return new PathPositionResult { Position = pos, AngleRadians = angle, IsValid = true };
                    }
                    break;

                case PathType.QuadraticBezier:
                    if (caches.Length >= 2)
                    {
                        var segments = PathPointCache.ToOpenQuadraticBezierSegments(caches, out var start);
                        if (TryGetQuadraticBezierPositionAndAngle(segments, start, position, out var pos, out var angle))
                            return new PathPositionResult { Position = pos, AngleRadians = angle, IsValid = true };
                    }
                    break;

                case PathType.Straight:
                default:
                    {
                        var points = PathPointCache.ToLinearLinePoints(caches);
                        if (TryGetLinearPositionAndAngle(points, position, out var pos, out var angle))
                            return new PathPositionResult { Position = pos, AngleRadians = angle, IsValid = true };
                    }
                    break;
            }

            // フォールバック: 最初のポイントか最後のポイント
            if (caches.Length > 0)
            {
                int idx = t <= 0f ? 0 : caches.Length - 1;
                return new PathPositionResult
                {
                    Position = caches[idx].Point,
                    AngleRadians = 0f,
                    IsValid = true
                };
            }

            return new PathPositionResult { IsValid = false };
        }

        // ─────────────────────────────────────────────────────
        //  直線パス
        // ─────────────────────────────────────────────────────

        public static float CalculateLinearLength(Vector2[] points)
        {
            float length = 0f;
            for (int i = 0; i < points.Length - 1; i++)
                length += Vector2.Distance(points[i], points[i + 1]);
            return length;
        }

        public static bool TryGetLinearPositionAndAngle(
            Vector2[] points, float position, out Vector2 vector, out float angle)
        {
            if (position < 0f)
            {
                // 先頭より前: 先頭を返し、先頭→次の角度を使う
                if (points.Length >= 2)
                {
                    vector = points[0];
                    angle = MathF.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);
                    return true;
                }
                vector = default;
                angle = 0f;
                return false;
            }

            float accumulated = 0f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                float segLength = Vector2.Distance(points[i], points[i + 1]);
                if (accumulated + segLength >= position)
                {
                    float t = (position - accumulated) / segLength;
                    vector = Vector2.Lerp(points[i], points[i + 1], t);
                    angle = MathF.Atan2(
                        points[i + 1].Y - points[i].Y,
                        points[i + 1].X - points[i].X);
                    return true;
                }
                accumulated += segLength;
            }

            // 末尾を超えた場合: 末尾を返す
            if (points.Length >= 2)
            {
                vector = points[^1];
                angle = MathF.Atan2(
                    points[^1].Y - points[^2].Y,
                    points[^1].X - points[^2].X);
                return true;
            }

            vector = default;
            angle = 0f;
            return false;
        }

        // ─────────────────────────────────────────────────────
        //  二次ベジェパス
        // ─────────────────────────────────────────────────────

        public static float CalculateQuadraticBezierLength(
            QuadraticBezierSegment[] segments, Vector2 startPoint)
        {
            float total = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                var p0 = i == 0 ? startPoint : segments[i - 1].Point2;
                total += CalculateQuadraticSegmentLength(p0, segments[i].Point1, segments[i].Point2);
            }
            return total;
        }

        private static float CalculateQuadraticSegmentLength(Vector2 p0, Vector2 p1, Vector2 p2)
        {
            float length = 0f;
            var prev = p0;
            for (float t = 0.01f; t <= 1.0f; t += 0.01f)
            {
                var current = QuadraticBezierPoint(p0, p1, p2, t);
                length += Vector2.Distance(prev, current);
                prev = current;
            }
            return length;
        }

        public static bool TryGetQuadraticBezierPositionAndAngle(
            QuadraticBezierSegment[] segments, Vector2 startPoint,
            float position, out Vector2 vector, out float angle)
        {
            if (position < 0f)
            {
                vector = startPoint;
                if (segments.Length > 0)
                    angle = MathF.Atan2(segments[0].Point1.Y - startPoint.Y, segments[0].Point1.X - startPoint.X);
                else
                    angle = 0f;
                return true;
            }

            float accumulated = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                var p0 = i == 0 ? startPoint : segments[i - 1].Point2;
                var p1 = segments[i].Point1;
                var p2 = segments[i].Point2;
                float segLength = CalculateQuadraticSegmentLength(p0, p1, p2);

                if (accumulated + segLength >= position)
                {
                    var prev = p0;
                    for (float t = 0.01f; t <= 1.0f; t += 0.01f)
                    {
                        var current = QuadraticBezierPoint(p0, p1, p2, t);
                        float dist = Vector2.Distance(prev, current);
                        if (accumulated + dist >= position)
                        {
                            float factor = dist == 0f ? 0f : (position - accumulated) / dist;
                            vector = Vector2.Lerp(prev, current, factor);
                            angle = MathF.Atan2(current.Y - prev.Y, current.X - prev.X);
                            return true;
                        }
                        accumulated += dist;
                        prev = current;
                    }
                }
                accumulated += segLength;
            }

            // 末尾を超えた場合
            if (segments.Length > 0)
            {
                vector = segments[^1].Point2;
                angle = 0f;
                return true;
            }

            vector = default;
            angle = 0f;
            return false;
        }

        private static Vector2 QuadraticBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        // ─────────────────────────────────────────────────────
        //  三次ベジェパス
        // ─────────────────────────────────────────────────────

        public static float CalculateCubicBezierLength(BezierSegment[] segments, Vector2 startPoint)
        {
            float total = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                var p0 = i == 0 ? startPoint : segments[i - 1].Point3;
                total += CalculateCubicSegmentLength(p0, segments[i].Point1, segments[i].Point2, segments[i].Point3);
            }
            return total;
        }

        private static float CalculateCubicSegmentLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float length = 0f;
            var prev = p0;
            for (float t = 0.01f; t <= 1.0f; t += 0.01f)
            {
                var current = CubicBezierPoint(p0, p1, p2, p3, t);
                length += Vector2.Distance(prev, current);
                prev = current;
            }
            return length;
        }

        public static bool TryGetCubicBezierPositionAndAngle(
            BezierSegment[] segments, Vector2 startPoint,
            float position, out Vector2 vector, out float angle)
        {
            if (position < 0f)
            {
                vector = startPoint;
                if (segments.Length > 0)
                    angle = MathF.Atan2(segments[0].Point1.Y - startPoint.Y, segments[0].Point1.X - startPoint.X);
                else
                    angle = 0f;
                return true;
            }

            float accumulated = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                var p0 = i == 0 ? startPoint : segments[i - 1].Point3;
                var p1 = segments[i].Point1;
                var p2 = segments[i].Point2;
                var p3 = segments[i].Point3;
                float segLength = CalculateCubicSegmentLength(p0, p1, p2, p3);

                if (accumulated + segLength >= position)
                {
                    var prev = p0;
                    for (float t = 0.01f; t <= 1.0f; t += 0.01f)
                    {
                        var current = CubicBezierPoint(p0, p1, p2, p3, t);
                        float dist = Vector2.Distance(prev, current);
                        if (accumulated + dist >= position)
                        {
                            float factor = dist == 0f ? 0f : (position - accumulated) / dist;
                            vector = Vector2.Lerp(prev, current, factor);
                            angle = MathF.Atan2(current.Y - prev.Y, current.X - prev.X);
                            return true;
                        }
                        accumulated += dist;
                        prev = current;
                    }
                }
                accumulated += segLength;
            }

            // 末尾を超えた場合
            if (segments.Length > 0)
            {
                vector = segments[^1].Point3;
                angle = 0f;
                return true;
            }

            vector = default;
            angle = 0f;
            return false;
        }

        private static Vector2 CubicBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0
                 + 3f * u * u * t * p1
                 + 3f * u * t * t * p2
                 + t * t * t * p3;
        }

        // ─────────────────────────────────────────────────────
        //  パスポイント配列 → キャッシュ配列変換
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// パスポイントリストをキャッシュ配列に変換する
        /// </summary>
        public static PathPointCache[] CreateCaches(
            ImmutableList<PathPoint> points, int frame, int length, int fps)
        {
            return points.Select<PathPoint, PathPointCache>(p =>
                p is CubicBezierPathPoint cbp
                    ? new CubicBezierPathPointCache(cbp, frame, length, fps)
                    : new PathPointCache(p, frame, length, fps)
            ).ToArray();
        }

        // ─────────────────────────────────────────────────────
        //  補間ユーティリティ
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// モーションの動き値 (0〜10) に基づいて補間する
        /// 0=リニア（カクカク）, 10=滑らか（Hermite補間）
        /// </summary>
        public static float SmoothInterpolate(float t, float smoothness)
        {
            if (smoothness <= 0f) return t;

            float factor = smoothness / 10f;
            // SmoothStep補間: 3t² - 2t³
            float smooth = t * t * (3f - 2f * t);
            return t + (smooth - t) * factor;
        }
    }
}
