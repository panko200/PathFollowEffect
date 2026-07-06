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

namespace PathFollowEffect
{
    /// <summary>
    /// テキストのパスのオプション映像エフェクト
    /// テキストの各文字をパスに沿って配置する
    /// </summary>
    [VideoEffect("テキストのパスのオプション", ["テキスト"], ["パス", "テキスト", "path", "text", "曲線", "配置"], IsAviUtlSupported = false)]
    internal class TextPathEffect : VideoEffectBase
    {
        public override string Label => "テキストのパスのオプション";

        // ─────────────────────────────────────────
        //  パス設定
        // ─────────────────────────────────────────

        [Display(GroupName = "テキストパス", Name = "線の種類", Description = "パスの曲線の種類を選択します")]
        [EnumComboBox]
        public PathType PathType
        {
            get => pathType;
            set => Set(ref pathType, value);
        }
        PathType pathType = PathType.Straight;

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

        [Display(GroupName = "回転", Name = "進行方向に回転", Description = "ONにすると文字がパスの進行方向を向くように回転します")]
        [ToggleSlider]
        public bool RotateAlongPath
        {
            get => rotateAlongPath;
            set => Set(ref rotateAlongPath, value);
        }
        bool rotateAlongPath = true;

        [Display(GroupName = "回転", Name = "回転のオフセット", Description = "進行方向に加算する回転角度のオフセット")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationOffset { get; } = new Animation(0, -100000, 100000);

        // ─────────────────────────────────────────
        //  テキスト配置設定
        // ─────────────────────────────────────────

        [Display(GroupName = "テキスト配置", Name = "相対位置（長さ）", Description = "接線方向からのズレの距離（px）")]
        [AnimationSlider("F1", "px", -500, 500)]
        [DefaultValue(0.0)]
        public Animation TextSpread { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "テキスト配置", Name = "相対位置（角度）", Description = "接線方向からのズレの角度（°）")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation TextAngleOffset { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "テキスト配置", Name = "配置位置", Description = "テキスト全体をパスに沿って移動させます")]
        [AnimationSlider("F1", "%", -100, 100)]
        [DefaultValue(0.0)]
        public Animation TextOffset { get; } = new Animation(0, -100, 100);

        [Display(GroupName = "テキスト配置", Name = "最初のマージン", Description = "パス先頭からの余白（%）")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation MarginStart { get; } = new Animation(0.5f, -100, 100);

        [Display(GroupName = "テキスト配置", Name = "最後のマージン", Description = "パス末尾の余白（%）")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation MarginEnd { get; } = new Animation(0.5f, -100, 100);

        // ─────────────────────────────────────────
        //  パスポイント
        // ─────────────────────────────────────────

        [Display(GroupName = "パスポイントの配置")]
        [PathPointEditor]
        public ImmutableList<PathPoint> Points
        {
            get => points;
            set => Set(ref points, value);
        }
        ImmutableList<PathPoint> points = ImmutableList.Create<PathPoint>(
            new PathPoint(-100, 100),
            new PathPoint(100, -100)
        );

        // ─────────────────────────────────────────
        //  線の種類変更時のポイント変換
        // ─────────────────────────────────────────

        private PathType oldPathType;

        public override void BeginEdit()
        {
            base.BeginEdit();
            oldPathType = PathType;
        }

        public override ValueTask EndEditAsync()
        {
            if (oldPathType != PathType)
            {
                // 線の種類が変更された場合、ポイントを変換
                ImmutableList<PathPoint> converted;
                switch (PathType)
                {
                    case PathType.Straight:
                    case PathType.QuadraticBezier:
                        converted = ImmutableList.CreateRange(
                            PathPoint.FromPoints(Points));
                        break;
                    case PathType.CubicBezier:
                        converted = ImmutableList.CreateRange(
                            CubicBezierPathPoint.FromPoints(Points));
                        break;
                    default:
                        converted = Points;
                        break;
                }
                Points = converted;
            }
            return base.EndEditAsync();
        }

        // ─────────────────────────────────────────
        //  YMM4 インターフェース実装
        // ─────────────────────────────────────────

        // ── 文字0の元の座標を一時的に保持するキャッシュ（フレーム番号キー） ──
        [Newtonsoft.Json.JsonIgnore]
        public System.Collections.Concurrent.ConcurrentDictionary<long, System.Numerics.Vector3> Char0PosCache { get; } = new();

        [Newtonsoft.Json.JsonIgnore]
        public System.Numerics.Vector3 CurrentNewDraw { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public System.Numerics.Vector3 CurrentNewRotation { get; set; }

        public override IEnumerable<string> CreateExoVideoFilters(
            int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
            => new TextPathEffectProcessor(devices, this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            var list = new List<IAnimatable>
            {
                PathOffsetX, PathOffsetY, PathScale, PathRotation,
                RotationOffset,
                TextSpread, TextAngleOffset, TextOffset,
                MarginStart, MarginEnd
            };
            foreach (var p in Points)
                list.Add(p);
            return list;
        }
    }
}
