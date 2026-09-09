using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace PathFollowEffect
{
    /// <summary>
    /// モーションパスのキーフレームデータ
    /// フレーム番号と座標を持つ
    /// </summary>
    public class MotionKeyframe : Animatable
    {
        [Display(Name = "フレーム", Description = "キーフレームのフレーム番号（※キーフレーム時刻モード時のみ使用）")]
        [AnimationSlider("F0", "f", 0, 300)]
        public Animation Frame { get; } = new Animation(0, 0, 1000000);

        [Display(Name = "X座標", Description = "キーフレームのX座標")]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation X { get; } = new Animation(0.0, -100000.0, 100000.0);

        [Display(Name = "Y座標", Description = "キーフレームのY座標")]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation Y { get; } = new Animation(0.0, -100000.0, 100000.0);

        [Display(Name = "角度", Description = "三次ベジェ曲線の制御点角度")]
        [AnimationSlider("F1", "°", -360.0, 360.0)]
        public Animation Angle { get; } = new Animation(0.0, -100000.0, 100000.0);

        [Display(Name = "長さ1", Description = "三次ベジェ曲線の前方制御点の長さ")]
        [AnimationSlider("F1", "px", 0.0, 100.0)]
        public Animation Length1 { get; } = new Animation(25.0, 0.0, 100000.0);

        [Display(Name = "長さ2", Description = "三次ベジェ曲線の後方制御点の長さ")]
        [AnimationSlider("F1", "px", 0.0, 100.0)]
        public Animation Length2 { get; } = new Animation(25.0, 0.0, 100000.0);

        public MotionKeyframe()
        {
        }

        public MotionKeyframe(int frame, double x, double y)
        {
            Frame.ActiveValues.First().Value = frame;
            X.ActiveValues.First().Value = x;
            Y.ActiveValues.First().Value = y;
        }

        public MotionKeyframe(int frame, double x, double y, double angle, double length1, double length2)
            : this(frame, x, y)
        {
            Angle.ActiveValues.First().Value = angle;
            Length1.ActiveValues.First().Value = length1;
            Length2.ActiveValues.First().Value = length2;
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            return [Frame, X, Y, Angle, Length1, Length2];
        }
    }
}
