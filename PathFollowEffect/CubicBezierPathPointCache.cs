using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Vortice.Direct2D1;

namespace PathFollowEffect
{
    /// <summary>
    /// 三次ベジェ用のパスポイントキャッシュ
    /// </summary>
    internal record CubicBezierPathPointCache : PathPointCache
    {
        public float Angle { get; init; }
        public float Length1 { get; init; }
        public float Length2 { get; init; }

        /// <summary>
        /// 前方制御点（ポイントの手前側）
        /// </summary>
        public Vector2 ControlPoint1
        {
            get
            {
                return Point + Vector2.Transform(
                    new Vector2(-Length1, 0f),
                    Matrix3x2.CreateRotation((float)(Angle * Math.PI / 180.0)));
            }
        }

        /// <summary>
        /// 後方制御点（ポイントの先側）
        /// </summary>
        public Vector2 ControlPoint2
        {
            get
            {
                return Point + Vector2.Transform(
                    new Vector2(-Length2, 0f),
                    Matrix3x2.CreateRotation((float)(Angle * Math.PI / 180.0 + Math.PI)));
            }
        }

        public CubicBezierPathPointCache(CubicBezierPathPoint point, int frame, int length, int fps)
            : base(point, frame, length, fps)
        {
            Angle = (float)point.Angle.GetValue(frame, length, fps);
            Length1 = (float)point.Length1.GetValue(frame, length, fps);
            Length2 = (float)point.Length2.GetValue(frame, length, fps);
        }

        /// <summary>
        /// 三次ベジェセグメント配列を生成
        /// </summary>
        public static BezierSegment[] ToBezierSegments(
            CubicBezierPathPointCache[] points, out Vector2 startPoint)
        {
            startPoint = points[0].Point;
            var segments = new List<BezierSegment>();

            for (int i = 0; i < points.Length - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                segments.Add(new BezierSegment
                {
                    Point1 = p1.ControlPoint2,
                    Point2 = p2.ControlPoint1,
                    Point3 = p2.Point
                });
            }

            return segments.ToArray();
        }
    }
}
