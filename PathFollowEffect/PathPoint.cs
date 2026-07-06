using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace PathFollowEffect
{
    /// <summary>
    /// パス上の基本ポイント（直線・二次ベジェ用）
    /// YMM4の LineShapePoint と同等の構造
    /// </summary>
    public class PathPoint : Animatable
    {
        [Display(Name = "X座標", Description = "ポイントのX座標")]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation X { get; } = new Animation(0.0, -100000.0, 100000.0);

        [Display(Name = "Y座標", Description = "ポイントのY座標")]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation Y { get; } = new Animation(0.0, -100000.0, 100000.0);

        public PathPoint()
        {
        }

        public PathPoint(double x, double y)
        {
            X.ActiveValues.First().Value = x;
            Y.ActiveValues.First().Value = y;
        }

        public PathPoint(PathPoint point)
        {
            X.CopyFrom(point.X);
            Y.CopyFrom(point.Y);
        }

        public static IEnumerable<PathPoint> FromPoints(IEnumerable<PathPoint> points)
        {
            return points.Select(p => new PathPoint(p));
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            return [X, Y];
        }
    }
}
