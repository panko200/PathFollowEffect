using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace PathFollowEffect
{
    /// <summary>
    /// ソートされたキーフレームの計算済み値
    /// </summary>
    internal struct SortedKeyframe
    {
        public int Frame;
        public float X;
        public float Y;
        public float Angle;
        public float Length1;
        public float Length2;
        public MotionKeyframe Original;
    }

    /// <summary>
    /// モーションパスエフェクトのプロセッサ
    /// 現在フレームに基づいてパス上の位置と角度を計算し、DrawDescriptionを変更する
    /// </summary>
    internal class MotionPathEffectProcessor : IVideoEffectProcessor
    {
        private readonly IGraphicsDevicesAndContext devices;
        private readonly MotionPathEffect item;
        private ID2D1Image? input;

        public ID2D1Image Output => input ?? throw new NullReferenceException("input is null");

        public MotionPathEffectProcessor(IGraphicsDevicesAndContext devices, MotionPathEffect item)
        {
            this.devices = devices;
            this.item = item;
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            if (input == null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var drawDesc = effectDescription.DrawDescription;

            // キーフレームが2つ未満なら何もしない
            if (item.Keyframes.Count < 2)
                return drawDesc;

            // ── パラメータ取得 ──
            float smoothness = (float)item.Smoothness.GetValue(frame, length, fps);
            float offsetX = (float)item.PathOffsetX.GetValue(frame, length, fps);
            float offsetY = (float)item.PathOffsetY.GetValue(frame, length, fps);
            float scale = (float)item.PathScale.GetValue(frame, length, fps) / 100f;
            float rotationOffset = (float)item.RotationOffset.GetValue(frame, length, fps);

            // ── キーフレームリストの取得と進行率tの計算 ──
            List<SortedKeyframe> sortedKeyframes;
            float t;

            if (item.ProgressMode == MotionProgressMode.Easing)
            {
                // リストの登録順のままパスを生成（Frameによるソートは行わない）
                sortedKeyframes = item.Keyframes
                    .Select(kf => new SortedKeyframe
                    {
                        Frame = (int)kf.Frame.GetValue(frame, length, fps),
                        X = (float)kf.X.GetValue(frame, length, fps),
                        Y = (float)kf.Y.GetValue(frame, length, fps),
                        Angle = (float)kf.Angle.GetValue(frame, length, fps),
                        Length1 = (float)kf.Length1.GetValue(frame, length, fps),
                        Length2 = (float)kf.Length2.GetValue(frame, length, fps),
                        Original = kf
                    })
                    .ToList();

                // アイテム全体のフレーム進行度 (0.0 〜 1.0)
                float totalFrames = Math.Max(1f, length - 1);
                float rate = Math.Clamp((float)frame / totalFrames, 0f, 1f);
                if (item.Reverse)
                    rate = 1f - rate;

                // YMM4公式のイージング関数を適用
                t = (float)Easing.GetValue(item.EasingType, item.EasingMode, rate);
                t = Math.Clamp(t, 0f, 1f);
            }
            else if (item.ProgressMode == MotionProgressMode.Bezier)
            {
                // リストの登録順のままパスを生成（Frameによるソートは行わない）
                sortedKeyframes = item.Keyframes
                    .Select(kf => new SortedKeyframe
                    {
                        Frame = (int)kf.Frame.GetValue(frame, length, fps),
                        X = (float)kf.X.GetValue(frame, length, fps),
                        Y = (float)kf.Y.GetValue(frame, length, fps),
                        Angle = (float)kf.Angle.GetValue(frame, length, fps),
                        Length1 = (float)kf.Length1.GetValue(frame, length, fps),
                        Length2 = (float)kf.Length2.GetValue(frame, length, fps),
                        Original = kf
                    })
                    .ToList();

                // アイテム全体のフレーム進行度 (0.0 〜 1.0)
                float totalFrames = Math.Max(1f, length - 1);
                float rate = Math.Clamp((float)frame / totalFrames, 0f, 1f);

                // YMM4公式のベジェ曲線を適用
                t = (float)item.Bezier.GetAnimation(rate);
                t = Math.Clamp(t, 0f, 1f);
            }
            else
            {
                // 従来方式（キーフレーム時刻）
                sortedKeyframes = item.Keyframes
                    .Select(kf => new SortedKeyframe
                    {
                        Frame = (int)kf.Frame.GetValue(frame, length, fps),
                        X = (float)kf.X.GetValue(frame, length, fps),
                        Y = (float)kf.Y.GetValue(frame, length, fps),
                        Angle = (float)kf.Angle.GetValue(frame, length, fps),
                        Length1 = (float)kf.Length1.GetValue(frame, length, fps),
                        Length2 = (float)kf.Length2.GetValue(frame, length, fps),
                        Original = kf
                    })
                    .OrderBy(k => k.Frame)
                    .ToList();

                int firstFrame = sortedKeyframes[0].Frame;
                int lastFrame = sortedKeyframes[^1].Frame;
                float totalDuration = Math.Max(1, lastFrame - firstFrame);

                float rawT = (float)(frame - firstFrame) / totalDuration;
                rawT = Math.Clamp(rawT, 0f, 1f);

                t = PathMath.SmoothInterpolate(rawT, smoothness);
            }

            // ── キーフレーム間の補間位置を特定 ──
            // キーフレームをパスポイントとして扱い、パス上の位置を計算
            PathPointCache[] caches;
            if (item.PathType == PathType.CubicBezier)
            {
                caches = sortedKeyframes.Select(k =>
                    (PathPointCache)new CubicBezierPathPointCache(
                        new CubicBezierPathPoint(
                            new PathPoint(k.X, k.Y),
                            k.Angle, k.Length1, k.Length2),
                        0, 1, 30)
                ).ToArray();
            }
            else
            {
                caches = sortedKeyframes.Select(k =>
                    new PathPointCache(new PathPoint(k.X, k.Y), 0, 1, 30)
                ).ToArray();
            }

            // パス上の位置を計算
            var result = PathMath.GetPositionOnPath(item.PathType, caches, t);

            if (!result.IsValid)
                return drawDesc;

            // ── パス全体の回転を適用 ──
            float pathRot = (float)item.PathRotation.GetValue(frame, length, fps);
            float pathRotRad = pathRot * MathF.PI / 180f;
            if (pathRot != 0f)
            {
                float cos = MathF.Cos(pathRotRad);
                float sin = MathF.Sin(pathRotRad);
                float rx = result.Position.X * cos - result.Position.Y * sin;
                float ry = result.Position.X * sin + result.Position.Y * cos;
                result.Position = new Vector2(rx, ry);
                result.AngleRadians += pathRotRad;
            }

            // ── スケールとオフセットを適用 ──
            float finalX = result.Position.X * scale + offsetX;
            float finalY = result.Position.Y * scale + offsetY;

            // ── DrawDescriptionを更新 ──
            var newDraw = new Vector3(
                drawDesc.Draw.X + finalX,
                drawDesc.Draw.Y + finalY,
                drawDesc.Draw.Z);

            var newRotation = drawDesc.Rotation;
            if (item.RotateAlongPath)
            {
                float angleDeg = result.AngleRadians * 180f / MathF.PI + rotationOffset;
                newRotation = new Vector3(
                    newRotation.X,
                    newRotation.Y,
                    newRotation.Z + angleDeg);
            }

            // リアルタイムの現在の座標・回転をエフェクト側に保存（ドラッグ補正用）
            item.CurrentNewDraw = newDraw;
            item.CurrentNewRotation = newRotation;

            // ── プレビューUIのコントローラーを作成 ──
            var controllers = CreateControllers(effectDescription, sortedKeyframes, caches, scale, offsetX, offsetY, drawDesc, newDraw, newRotation);

            return drawDesc with
            {
                Draw = newDraw,
                Rotation = newRotation,
                Controllers = drawDesc.Controllers.AddRange(controllers)
            };
        }

        /// <summary>
        /// プレビュー上のドラッグ可能なコントロールポイントを作成する
        /// </summary>
        private List<VideoEffectController> CreateControllers(
            EffectDescription effectDescription,
            List<SortedKeyframe> sortedKeyframes,
            PathPointCache[] caches,
            float scale, float offsetX, float offsetY,
            DrawDescription drawDesc, Vector3 newDraw, Vector3 newRotation)
        {
            if (scale == 0f) scale = 0.0001f;
            var controllers = new List<VideoEffectController>();

            // ── パス全体の回転の角度取得 ──
            int frame = effectDescription.ItemPosition.Frame;
            int length = effectDescription.ItemDuration.Frame;
            double fps = effectDescription.FPS;
            float pathRot = (float)item.PathRotation.GetValue(frame, length, (int)fps);
            float pathRotRad = pathRot * MathF.PI / 180f;

            Vector2 rotatePoint(Vector2 p)
            {
                if (pathRot == 0f) return p;
                float cos = MathF.Cos(pathRotRad);
                float sin = MathF.Sin(pathRotRad);
                return new Vector2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
            }

            Vector2 getLocalPos(Vector2 p)
            {
                var rotated = rotatePoint(p);
                return rotated * scale + new Vector2(offsetX, offsetY);
            }

            // ── 元の座標系（デカップリング）と新しい座標系の変換行列を構築 ──
            var origDraw = drawDesc.Draw;

            var newZoom = drawDesc.Zoom;
            var newRotZ = newRotation.Z;

            // パス描画自体がアイテム自体のスケール・回転に引きずられないよう、mOrig は平行移動（origDraw）のみを適用する
            var mOrig = Matrix4x4.CreateTranslation(origDraw);

            var mNew = Matrix4x4.CreateScale(new Vector3(newZoom.X, newZoom.Y, 1f)) *
                       Matrix4x4.CreateRotationZ((float)(newRotZ / 180.0 * Math.PI)) *
                       Matrix4x4.CreateTranslation(newDraw);

            if (!Matrix4x4.Invert(mNew, out var mNewInv))
            {
                mNewInv = Matrix4x4.Identity;
            }

            Vector3 transformPoint(float lx, float ly)
            {
                var worldPos = Vector3.Transform(new Vector3(lx, ly, 0f), mOrig);
                return Vector3.Transform(worldPos, mNewInv);
            }

            Action<ControlPointDragEventArgs> makeDragHandler(Action<Vector3> applyDelta)
            {
                return arg =>
                {
                    // ドラッグ発生時のリアルタイムの最新の mNew を構築して逆変換
                    var currentNewDraw = item.CurrentNewDraw;
                    var currentNewRotation = item.CurrentNewRotation;

                    var mNewCurrent = Matrix4x4.CreateScale(new Vector3(newZoom.X, newZoom.Y, 1f)) *
                                      Matrix4x4.CreateRotationZ((float)(currentNewRotation.Z / 180.0 * Math.PI)) *
                                      Matrix4x4.CreateTranslation(currentNewDraw);

                    var dWorld = Vector3.TransformNormal(arg.Delta, mNewCurrent);

                    // パス全体の回転（PathRotation）を打ち消すように回転
                    float dragRotRad = -pathRotRad;
                    float cos = MathF.Cos(dragRotRad);
                    float sin = MathF.Sin(dragRotRad);
                    float dx = dWorld.X * cos - dWorld.Y * sin;
                    float dy = dWorld.X * sin + dWorld.Y * cos;

                    applyDelta(new Vector3(dx, dy, 0f));
                };
            }

            if (item.PathType == PathType.CubicBezier && caches.All(c => c is CubicBezierPathPointCache))
            {
                // 三次ベジェ: 各ポイント + 制御点ハンドル
                var bezierCaches = caches.Cast<CubicBezierPathPointCache>().ToArray();
                for (int i = 0; i < sortedKeyframes.Count; i++)
                {
                    var kf = sortedKeyframes[i].Original;
                    if (kf == null) continue;
                    var cache = bezierCaches[i];
                    int idx = i;

                    var points = new List<ControllerPoint>();

                    // 前方制御点（最初のポイント以外）
                    if (i > 0)
                    {
                        var cp1 = getLocalPos(cache.ControlPoint1);
                        points.Add(new ControllerPoint(
                            transformPoint(cp1.X, cp1.Y),
                            makeDragHandler(d =>
                            {
                                var c = new CubicBezierPathPointCache(
                                    new CubicBezierPathPoint(
                                        new PathPoint(kf.X.GetValue(0L, 1L, 30), kf.Y.GetValue(0L, 1L, 30)),
                                        kf.Angle.GetValue(0L, 1L, 30),
                                        kf.Length1.GetValue(0L, 1L, 30),
                                        kf.Length2.GetValue(0L, 1L, 30)),
                                    0, 1, 30);
                                var cp = c.ControlPoint1 - c.Point;
                                var newCp = cp + new Vector2(d.X / scale, d.Y / scale);
                                float deltaAngle = (float)((MathF.Atan2(newCp.Y, newCp.X) - MathF.Atan2(cp.Y, cp.X)) * 180.0 / Math.PI);
                                float deltaLen = newCp.Length() - cp.Length();
                                kf.Angle.AddToEachValues(deltaAngle);
                                kf.Length1.AddToEachValues(deltaLen);
                            }))
                        { Shape = VideoControllerPointShape.SmallCircle });
                    }

                    // メインポイント
                    var pt = getLocalPos(cache.Point);
                    points.Add(new ControllerPoint(
                        transformPoint(pt.X, pt.Y),
                        makeDragHandler(d =>
                        {
                            kf.X.AddToEachValues(d.X / scale);
                            kf.Y.AddToEachValues(d.Y / scale);
                        })));

                    // 後方制御点（最後のポイント以外）
                    if (i < sortedKeyframes.Count - 1)
                    {
                        var cp2 = getLocalPos(cache.ControlPoint2);
                        points.Add(new ControllerPoint(
                            transformPoint(cp2.X, cp2.Y),
                            makeDragHandler(d =>
                            {
                                var c = new CubicBezierPathPointCache(
                                    new CubicBezierPathPoint(
                                        new PathPoint(kf.X.GetValue(0L, 1L, 30), kf.Y.GetValue(0L, 1L, 30)),
                                        kf.Angle.GetValue(0L, 1L, 30),
                                        kf.Length1.GetValue(0L, 1L, 30),
                                        kf.Length2.GetValue(0L, 1L, 30)),
                                    0, 1, 30);
                                var cp = c.ControlPoint2 - c.Point;
                                var newCp = cp + new Vector2(d.X / scale, d.Y / scale);
                                float deltaAngle = (float)((MathF.Atan2(newCp.Y, newCp.X) - MathF.Atan2(cp.Y, cp.X)) * 180.0 / Math.PI);
                                float deltaLen = newCp.Length() - cp.Length();
                                kf.Angle.AddToEachValues(deltaAngle);
                                kf.Length2.AddToEachValues(deltaLen);
                            }))
                        { Shape = VideoControllerPointShape.SmallCircle });
                    }

                    controllers.Add(new VideoEffectController(item, points)
                    {
                        Connection = VideoControllerPointConnection.Line
                    });
                }
            }
            else
            {
                // 直線・二次ベジェ: 各ポイントのみ
                var controllerPoints = new List<ControllerPoint>();
                for (int i = 0; i < sortedKeyframes.Count; i++)
                {
                    var kf = sortedKeyframes[i].Original;
                    if (kf == null) continue;
                    var cache = caches[i];

                    var pt = getLocalPos(cache.Point);
                    controllerPoints.Add(new ControllerPoint(
                        transformPoint(pt.X, pt.Y),
                        makeDragHandler(d =>
                        {
                            kf.X.AddToEachValues(d.X / scale);
                            kf.Y.AddToEachValues(d.Y / scale);
                        })));
                }

                if (controllerPoints.Count > 0)
                {
                    var connection = item.PathType == PathType.QuadraticBezier
                        ? VideoControllerPointConnection.Line
                        : VideoControllerPointConnection.Line;

                    controllers.Add(new VideoEffectController(item, controllerPoints)
                    {
                        Connection = connection
                    });
                }
            }

            // ── パス曲線の可視化用コントローラーを追加 ──
            var curvePoints = new List<ControllerPoint>();
            const int numSegments = 100;
            for (int i = 0; i <= numSegments; i++)
            {
                float tCurve = i / (float)numSegments;
                var res = PathMath.GetPositionOnPath(item.PathType, caches, tCurve);
                if (res.IsValid)
                {
                    var pt = getLocalPos(res.Position);
                    var pCurve = transformPoint(pt.X, pt.Y);
                    curvePoints.Add(new ControllerPoint(pCurve)
                    {
                        Shape = VideoControllerPointShape.None
                    });
                }
            }
            if (curvePoints.Count > 0)
            {
                controllers.Add(new VideoEffectController(item, curvePoints)
                {
                    Connection = VideoControllerPointConnection.Line
                });
            }

            return controllers;
        }

        // ─────────────────────────────────────────
        //  IVideoEffectProcessor 実装
        // ─────────────────────────────────────────

        public void SetInput(ID2D1Image? input) => this.input = input;
        public void ClearInput() => input = null;

        public void Dispose()
        {
        }
    }
}
