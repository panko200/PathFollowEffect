using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace PathFollowEffect
{
    /// <summary>
    /// 三次ベジェ曲線用パスポイント
    /// YMM4の CubicBezierLineShapePoint と同等の構造
    /// </summary>
    public class CubicBezierPathPoint : PathPoint
    {
        [Display(Name = "角度", Description = "制御点の角度")]
        [AnimationSlider("F1", "°", -360.0, 360.0)]
        public Animation Angle { get; } = new Animation(0.0, -100000.0, 100000.0);

        [Display(Name = "長さ1", Description = "前方制御点の長さ")]
        [AnimationSlider("F1", "px", 0.0, 100.0)]
        public Animation Length1 { get; } = new Animation(25.0, 0.0, 100000.0);

        [Display(Name = "長さ2", Description = "後方制御点の長さ")]
        [AnimationSlider("F1", "px", 0.0, 100.0)]
        public Animation Length2 { get; } = new Animation(25.0, 0.0, 100000.0);

        public CubicBezierPathPoint()
        {
        }

        public CubicBezierPathPoint(PathPoint point, double angle, double lengthA, double lengthB)
        {
            X.CopyFrom(point.X);
            Y.CopyFrom(point.Y);
            Angle.ActiveValues.First().Value = angle;
            Length1.ActiveValues.First().Value = lengthA;
            Length2.ActiveValues.First().Value = lengthB;
        }

        /// <summary>
        /// 既存のPathPointリストから三次ベジェポイントリストに変換する
        /// </summary>
        public new static IEnumerable<PathPoint> FromPoints(IEnumerable<PathPoint> points)
        {
            var pointList = points.ToList();
            var result = new List<CubicBezierPathPoint>();

            for (int i = 0; i < pointList.Count; i++)
            {
                var current = pointList[i];
                var prev = pointList[Math.Max(0, i - 1)];
                var next = pointList[Math.Min(pointList.Count - 1, i + 1)];

                var currentPos = new Vector2(
                    (float)current.X.GetValue(0L, 1L, 30),
                    (float)current.Y.GetValue(0L, 1L, 30));
                var prevPos = new Vector2(
                    (float)prev.X.GetValue(0L, 1L, 30),
                    (float)prev.Y.GetValue(0L, 1L, 30));
                var nextPos = new Vector2(
                    (float)next.X.GetValue(0L, 1L, 30),
                    (float)next.Y.GetValue(0L, 1L, 30));

                double angle = Math.Atan2(nextPos.Y - prevPos.Y, nextPos.X - prevPos.X) * 180.0 / Math.PI;
                float lengthA = (prevPos - currentPos).Length() / 3f;
                float lengthB = (nextPos - currentPos).Length() / 3f;

                if (i == 0) lengthA = lengthB;
                if (i == pointList.Count - 1) lengthB = lengthA;

                result.Add(new CubicBezierPathPoint(current, angle, lengthA, lengthB));
            }

            return result;
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            var list = new List<IAnimatable>(base.GetAnimatables());
            list.Add(Angle);
            list.Add(Length1);
            list.Add(Length2);
            return list;
        }
    }
}
