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
    /// テキストパスエフェクトのプロセッサ
    /// InputIndex（文字インデックス）を使って各文字の位置をパス上に計算する
    /// </summary>
    internal class TextPathEffectProcessor : IVideoEffectProcessor
    {
        private readonly IGraphicsDevicesAndContext devices;
        private readonly TextPathEffect item;
        private ID2D1Image? input;

        public ID2D1Image Output => input ?? throw new NullReferenceException("input is null");

        public TextPathEffectProcessor(IGraphicsDevicesAndContext devices, TextPathEffect item)
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

            // ポイントが2つ未満なら何もしない
            if (item.Points.Count < 2)
                return drawDesc;

            // ── パラメータ取得 ──
            float offsetX = (float)item.PathOffsetX.GetValue(frame, length, fps);
            float offsetY = (float)item.PathOffsetY.GetValue(frame, length, fps);
            float scale = (float)item.PathScale.GetValue(frame, length, fps) / 100f;
            float rotationOffset = (float)item.RotationOffset.GetValue(frame, length, fps);
            float relLength = (float)item.TextSpread.GetValue(frame, length, fps); // ピクセル単位のズレ距離
            float relAngleDeg = (float)item.TextAngleOffset.GetValue(frame, length, fps); // 接線からの角度オフセット（度）
            float textOffset = (float)item.TextOffset.GetValue(frame, length, fps) / 100f; // スライド進捗オフセット
            float marginStart = (float)item.MarginStart.GetValue(frame, length, fps) / 100f;
            float marginEnd = (float)item.MarginEnd.GetValue(frame, length, fps) / 100f;

            int inputIndex = effectDescription.InputIndex;
            int inputCount = Math.Max(1, effectDescription.InputCount);

            // ── 文字0の未編集位置を一時キャッシュ ──
            if (inputIndex == 0)
            {
                if (item.Char0PosCache.Count > 100)
                {
                    item.Char0PosCache.Clear();
                }
                item.Char0PosCache[frame] = drawDesc.Draw;
            }

            Vector3 char0Pos;
            if (!item.Char0PosCache.TryGetValue(frame, out char0Pos))
            {
                char0Pos = drawDesc.Draw;
            }

            // ── パスポイントのキャッシュを作成 ──
            var caches = PathMath.CreateCaches(item.Points, frame, length, fps);

            // ── 文字のパス上の位置を計算 ──
            // 有効範囲: marginStart 〜 (1 - marginEnd)
            float rangeStart = Math.Clamp(marginStart, 0f, 1f);
            float rangeEnd = Math.Clamp(1f - marginEnd, 0f, 1f);
            float range = Math.Max(0f, rangeEnd - rangeStart);

            float t;
            if (inputCount <= 1)
            {
                // 1文字の場合は範囲の中央に配置
                t = rangeStart + range * 0.5f;
            }
            else
            {
                float step = range / (inputCount - 1);
                t = rangeStart + step * inputIndex;
            }

            // 配置位置（配置オフセット）を加算
            t = t + textOffset;

            t = Math.Clamp(t, 0f, 1f);

            // パス上の位置と角度を取得
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

            // ── 相対位置（長さと角度）によるオフセットを適用 ──
            // 接線方向からの相対角度と長さでズレを算出
            float totalAngleRad = result.AngleRadians + relAngleDeg * MathF.PI / 180f;
            float ox = MathF.Cos(totalAngleRad) * relLength;
            float oy = MathF.Sin(totalAngleRad) * relLength;

            // ── スケールとオフセットを適用 ──
            float finalX = result.Position.X * scale + offsetX + ox;
            float finalY = result.Position.Y * scale + offsetY + oy;

            // ── DrawDescriptionを更新 ──
            // 文字0の元の座標 (char0Pos) を基準として配置し、右方向への意図しない累積ずれを防ぐ
            var newDraw = new Vector3(
                char0Pos.X + finalX,
                char0Pos.Y + finalY,
                char0Pos.Z);

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
            if (inputIndex == 0)
            {
                item.CurrentNewDraw = newDraw;
                item.CurrentNewRotation = newRotation;
            }

            // ── プレビューUIのコントローラーを作成 ──
            // 最初の文字（inputIndex==0）のときだけコントローラーを出す（重複防止）
            var controllers = ImmutableList<VideoEffectController>.Empty;
            if (inputIndex == 0)
            {
                controllers = ImmutableList.CreateRange(
                    CreateControllers(caches, scale, offsetX, offsetY, drawDesc, newDraw, newRotation, effectDescription));
            }

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
            PathPointCache[] caches, float scale, float offsetX, float offsetY,
            DrawDescription drawDesc, Vector3 newDraw, Vector3 newRotation,
            EffectDescription effectDescription)
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

            // ── 元の座標系と新しい座標系の変換行列を構築 ──
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
                for (int i = 0; i < item.Points.Count; i++)
                {
                    if (item.Points[i] is not CubicBezierPathPoint cbp) continue;
                    var cache = bezierCaches[i];
                    var kf = cbp;

                    var points = new List<ControllerPoint>();

                    // 前方制御点
                    if (i > 0)
                    {
                        var cp1 = getLocalPos(cache.ControlPoint1);
                        points.Add(new ControllerPoint(
                            transformPoint(cp1.X, cp1.Y),
                            makeDragHandler(d =>
                            {
                                var c = new CubicBezierPathPointCache(kf, 0, 1, 30);
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

                    // 後方制御点
                    if (i < item.Points.Count - 1)
                    {
                        var cp2 = getLocalPos(cache.ControlPoint2);
                        points.Add(new ControllerPoint(
                            transformPoint(cp2.X, cp2.Y),
                            makeDragHandler(d =>
                            {
                                var c = new CubicBezierPathPointCache(kf, 0, 1, 30);
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
                for (int i = 0; i < item.Points.Count; i++)
                {
                    var p = item.Points[i];
                    var cache = caches[i];

                    var pt = getLocalPos(cache.Point);
                    controllerPoints.Add(new ControllerPoint(
                        transformPoint(pt.X, pt.Y),
                        makeDragHandler(d =>
                        {
                            p.X.AddToEachValues(d.X / scale);
                            p.Y.AddToEachValues(d.Y / scale);
                        })));
                }

                if (controllerPoints.Count > 0)
                {
                    controllers.Add(new VideoEffectController(item, controllerPoints)
                    {
                        Connection = VideoControllerPointConnection.Line
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
