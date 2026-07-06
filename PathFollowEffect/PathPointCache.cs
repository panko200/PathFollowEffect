using System;
using System.Numerics;
using Vortice.Direct2D1;

namespace PathFollowEffect
{
    /// <summary>
    /// フレームごとのパスポイントキャッシュ
    /// </summary>
    internal record PathPointCache
    {
        public Vector2 Point { get; init; }

        public PathPointCache(PathPoint point, int frame, int length, int fps)
        {
            Point = new Vector2(
                (float)point.X.GetValue(frame, length, fps),
                (float)point.Y.GetValue(frame, length, fps));
        }

        /// <summary>
        /// 直線用のポイント配列を生成
        /// </summary>
        public static Vector2[] ToLinearLinePoints(PathPointCache[] points)
        {
            var result = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = points[i].Point;
            return result;
        }

        /// <summary>
        /// 二次ベジェセグメントを生成（開いたパス）
        /// </summary>
        public static QuadraticBezierSegment[] ToOpenQuadraticBezierSegments(
            PathPointCache[] points, out Vector2 startPoint)
        {
            startPoint = points[0].Point;
            var segments = new QuadraticBezierSegment[Math.Max(1, points.Length - 2)];

            for (int i = 1; i < points.Length - 2; i++)
            {
                var p1 = points[i].Point;
                var p2 = points[i + 1].Point;
                segments[i - 1] = new QuadraticBezierSegment
                {
                    Point1 = p1,
                    Point2 = p1 + (p2 - p1) / 2f
                };
            }

            // 最後のセグメント
            segments[^1] = new QuadraticBezierSegment
            {
                Point1 = points[^2].Point,
                Point2 = points[^1].Point
            };

            return segments;
        }
    }
}
