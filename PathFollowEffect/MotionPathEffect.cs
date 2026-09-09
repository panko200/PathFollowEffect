using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.UndoRedo;

namespace PathFollowEffect
{
    /// <summary>
    /// モーションパス映像エフェクト
    /// アイテムをパスに沿って移動させる
    /// </summary>
    [VideoEffect("モーションパス", ["アニメーション"], ["パス", "モーション", "path", "motion", "追従", "移動"], IsAviUtlSupported = false)]
    internal class MotionPathEffect : VideoEffectBase
    {
        public override string Label => "モーションパス";

        // ─────────────────────────────────────────
        //  パス設定
        // ─────────────────────────────────────────

        [Display(GroupName = "モーションパス", Name = "線の種類", Description = "パスの曲線の種類を選択します")]
        [EnumComboBox]
        public PathType PathType
        {
            get => pathType;
            set => Set(ref pathType, value);
        }
        PathType pathType = PathType.Straight;

        // ─────────────────────────────────────────
        //  進行設定
        // ─────────────────────────────────────────

        [Display(GroupName = "進行設定", Name = "指定方法", Description = "パス上の移動の進行方法を選択します")]
        [EnumComboBox]
        public MotionProgressMode ProgressMode
        {
            get => progressMode;
            set => Set(ref progressMode, value);
        }
        MotionProgressMode progressMode = MotionProgressMode.Keyframe;

        [Display(GroupName = "進行設定", Name = "モーションの滑らかさ", Description = "0=カクカク動く、10=滑らかに動く")]
        [ShowPropertyEditorWhen(nameof(ProgressMode), MotionProgressMode.Keyframe)]
        [AnimationSlider("F1", "", 0, 10)]
        public Animation Smoothness { get; } = new Animation(5, 0, 10);

        [Display(GroupName = "進行設定", Name = "イージング", Description = "イージングの種類を選択します")]
        [ShowPropertyEditorWhen(nameof(ProgressMode), MotionProgressMode.Easing)]
        [EnumComboBox]
        public EasingType EasingType
        {
            get => easingType;
            set => Set(ref easingType, value);
        }
        EasingType easingType = EasingType.Cubic;

        [Display(GroupName = "進行設定", Name = "モード", Description = "イージングの方向を選択します")]
        [ShowPropertyEditorWhen(nameof(ProgressMode), MotionProgressMode.Easing)]
        [EnumComboBox]
        public EasingMode EasingMode
        {
            get => easingMode;
            set => Set(ref easingMode, value);
        }
        EasingMode easingMode = EasingMode.InOut;

        [Display(GroupName = "進行設定", Name = "反転", Description = "ONにするとパスの終点から始点に向かって移動します")]
        [ShowPropertyEditorWhen(nameof(ProgressMode), MotionProgressMode.Easing)]
        [ToggleSlider]
        public bool Reverse
        {
            get => reverse;
            set => Set(ref reverse, value);
        }
        bool reverse = false;

        [Display(GroupName = "進行設定", Description = "進行速度のベジェ曲線を編集します")]
        [ShowPropertyEditorWhen(nameof(ProgressMode), MotionProgressMode.Bezier)]
        [MotionBezierAnimationEditor]
        public BezierAnimation Bezier { get; } = new BezierAnimation();

        public MotionPathEffect()
        {
            SubscribeChildUndoRedoable((IUndoRedoable)Bezier);
        }

        // ベジェポイントのドラッグ中にUndoRedoCommandCreatedがBezierAnimationまで
        // 伝搬しないため、ポイント単位で購読しエフェクトに転送する。
        private void SubscribeBezierPoints()
        {
            foreach (var p in Bezier.Points)
                p.UndoRedoCommandCreated += BezierPoint_UndoRedoCommandCreated;
        }

        private void UnsubscribeBezierPoints()
        {
            foreach (var p in Bezier.Points)
                p.UndoRedoCommandCreated -= BezierPoint_UndoRedoCommandCreated;
        }

        private void BezierPoint_UndoRedoCommandCreated(object? sender, UndoRedoEventArgs e)
        {
            RaiseUndoRedoPointCreatedEvent(sender, e);
        }

        // ─────────────────────────────────────────
        //  パス全体の変換
        // ─────────────────────────────────────────

        [Display(GroupName = "パス全体", Name = "X座標", Description = "パス全体のX座標オフセット")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation PathOffsetX { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "パス全体", Name = "Y座標", Description = "パス全体のY座標オフセット")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation PathOffsetY { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "パス全体", Name = "拡大率", Description = "パス座標の拡大率")]
        [AnimationSlider("F1", "%", 0, 400)]
        [DefaultValue(100.0)]
        public Animation PathScale { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = "パス全体", Name = "回転", Description = "パス全体の回転角度")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation PathRotation { get; } = new Animation(0, -100000, 100000);

        // ─────────────────────────────────────────
        //  回転設定
        // ─────────────────────────────────────────

        [Display(GroupName = "回転", Name = "進行方向に回転", Description = "ONにするとアイテムがパスの進行方向を向くように回転します")]
        [ToggleSlider]
        public bool RotateAlongPath
        {
            get => rotateAlongPath;
            set => Set(ref rotateAlongPath, value);
        }
        bool rotateAlongPath = false;

        [Display(GroupName = "回転", Name = "回転のオフセット", Description = "進行方向に加算する回転角度のオフセット")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationOffset { get; } = new Animation(0, -100000, 100000);

        // ─────────────────────────────────────────
        //  キーフレーム
        // ─────────────────────────────────────────

        [Display(GroupName = "キーフレームの配置")]
        [MotionKeyframeEditor]
        public ImmutableList<MotionKeyframe> Keyframes
        {
            get => keyframes;
            set => Set(ref keyframes, value);
        }
        ImmutableList<MotionKeyframe> keyframes = ImmutableList.Create(
            new MotionKeyframe(0, -100, 100),
            new MotionKeyframe(100, 100, -100)
        );

        // ─────────────────────────────────────────
        //  線の種類変更時のポイント変換
        // ─────────────────────────────────────────

        private PathType oldPathType;

        public override void BeginEdit()
        {
            base.BeginEdit();
            oldPathType = PathType;
            SubscribeBezierPoints();
        }

        public override ValueTask EndEditAsync()
        {
            UnsubscribeBezierPoints();
            if (oldPathType != PathType)
            {
                // 線の種類が変更された場合、キーフレームのポイントを変換
                // 三次ベジェに変更時は角度・長さを自動計算
                // 直線/二次に変更時は角度・長さ情報を無視（値はそのまま保持）
            }
            return base.EndEditAsync();
        }

        // ─────────────────────────────────────────
        //  YMM4 インターフェース実装
        // ─────────────────────────────────────────

        [Newtonsoft.Json.JsonIgnore]
        public System.Numerics.Vector3 CurrentNewDraw { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public System.Numerics.Vector3 CurrentNewRotation { get; set; }

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new MotionPathEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            var list = new List<IAnimatable>
            {
                Smoothness,
                PathOffsetX, PathOffsetY, PathScale, PathRotation,
                RotationOffset
            };
            foreach (var kf in Keyframes)
                list.Add(kf);
            return list;
        }
    }
}
