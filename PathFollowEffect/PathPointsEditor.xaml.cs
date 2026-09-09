using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace PathFollowEffect
{
    /// <summary>
    /// パスポイントリストのカスタムエディタUI
    /// テキストパスエフェクトで使用（フレーム番号なし、ポイント座標のみ）
    /// </summary>
    public partial class PathPointsEditor : System.Windows.Controls.UserControl, IPropertyEditorControl2, IPropertyEditorControl
    {
        public static readonly DependencyProperty PointsProperty =
            DependencyProperty.Register(
                nameof(Points),
                typeof(ImmutableList<PathPoint>),
                typeof(PathPointsEditor),
                new FrameworkPropertyMetadata(
                    ImmutableList<PathPoint>.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    (s, _) =>
                    {
                        if (s is PathPointsEditor editor)
                        {
                            editor.UpdateTargetPoints();
                            editor.UpdateButtonState();
                        }
                    }));

        public static readonly DependencyProperty TargetPointsProperty =
            DependencyProperty.Register(
                nameof(TargetPoints),
                typeof(IEnumerable<PathPoint>),
                typeof(PathPointsEditor),
                new PropertyMetadata(null));

        private ItemProperty[]? properties;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ImmutableList<PathPoint> Points
        {
            get => (ImmutableList<PathPoint>)GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        public IEnumerable<PathPoint>? TargetPoints
        {
            get => (IEnumerable<PathPoint>?)GetValue(TargetPointsProperty);
            set => SetValue(TargetPointsProperty, value);
        }

        public ItemProperty[]? Properties
        {
            get => properties;
            set
            {
                if (properties == value) return;
                properties = value;
                UpdateTargetPoints();
            }
        }

        public PathPointsEditor()
        {
            InitializeComponent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = Math.Max(0, listBox.SelectedIndex);
            BeginEdit?.Invoke(this, EventArgs.Empty);

            if (Points.FirstOrDefault() is CubicBezierPathPoint)
                AddCubicBezierPoint(selectedIndex);
            else
                AddLinearPoint(selectedIndex);

            EndEdit?.Invoke(this, EventArgs.Empty);
            listBox.SelectedIndex = selectedIndex + 1;
            UpdateButtonState();
        }

        private void AddCubicBezierPoint(int selectedIndex)
        {
            Vector2 newPos;
            double angle;
            double len;

            if (selectedIndex + 1 >= Points.Count)
            {
                // 末尾に追加: 最後のポイントの延長方向
                var last = Points[^1];
                var lastPos = new Vector2(
                    (float)last.X.GetValue(0L, 1L, 30),
                    (float)last.Y.GetValue(0L, 1L, 30));

                if (last is CubicBezierPathPoint cbLast)
                {
                    var cache = new CubicBezierPathPointCache(cbLast, 0, 1, 30);
                    var dir = Vector2.Transform(
                        new Vector2(1f, 0f),
                        Matrix3x2.CreateRotation((float)(cache.Angle / 180.0 * Math.PI)));
                    newPos = cache.ControlPoint2 + dir * 50f;
                }
                else
                {
                    var prev = Points[Math.Max(0, Points.Count - 2)];
                    var prevPos = new Vector2(
                        (float)prev.X.GetValue(0L, 1L, 30),
                        (float)prev.Y.GetValue(0L, 1L, 30));
                    var dir = Vector2.Normalize(lastPos - prevPos);
                    newPos = lastPos + dir * 50f;
                }

                angle = 0;
                len = 25;
            }
            else
            {
                // 中間に挿入: 2点の中間
                var p1 = Points[selectedIndex];
                var p2 = Points[selectedIndex + 1];
                var pos1 = new Vector2(
                    (float)p1.X.GetValue(0L, 1L, 30),
                    (float)p1.Y.GetValue(0L, 1L, 30));
                var pos2 = new Vector2(
                    (float)p2.X.GetValue(0L, 1L, 30),
                    (float)p2.Y.GetValue(0L, 1L, 30));
                newPos = (pos1 + pos2) / 2f;
                angle = Math.Atan2(pos2.Y - newPos.Y, pos2.X - newPos.X) * 180.0 / Math.PI;
                len = (newPos - pos1).Length() / 3.0;
            }

            var newPoint = new CubicBezierPathPoint(
                new PathPoint(newPos.X, newPos.Y), angle, len, len);
            Points = Points.Insert(selectedIndex + 1, newPoint);
        }

        private void AddLinearPoint(int selectedIndex)
        {
            Vector2 newPos;

            if (selectedIndex + 1 >= Points.Count)
            {
                // 末尾に追加
                var last = Points[^1];
                var prev = Points[Math.Max(0, Points.Count - 2)];
                var lastPos = new Vector2(
                    (float)last.X.GetValue(0L, 1L, 30),
                    (float)last.Y.GetValue(0L, 1L, 30));
                var prevPos = new Vector2(
                    (float)prev.X.GetValue(0L, 1L, 30),
                    (float)prev.Y.GetValue(0L, 1L, 30));
                var dir = lastPos - prevPos;
                if (dir.Length() > 0.001f)
                    dir = Vector2.Normalize(dir);
                else
                    dir = new Vector2(1f, 0f);
                newPos = lastPos + dir * 50f;
            }
            else
            {
                // 中間に挿入
                var p1 = Points[selectedIndex];
                var p2 = Points[selectedIndex + 1];
                newPos = (new Vector2(
                    (float)p1.X.GetValue(0L, 1L, 30),
                    (float)p1.Y.GetValue(0L, 1L, 30)) +
                    new Vector2(
                    (float)p2.X.GetValue(0L, 1L, 30),
                    (float)p2.Y.GetValue(0L, 1L, 30))) / 2f;
            }

            var newPoint = new PathPoint(newPos.X, newPos.Y);
            Points = Points.Insert(selectedIndex + 1, newPoint);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            if (index == -1 || Points.Count <= 2) return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            Points = Points.RemoveAt(index);
            EndEdit?.Invoke(this, EventArgs.Empty);

            listBox.SelectedIndex = Math.Max(0, index - 1);
            UpdateButtonState();
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            if (index <= 0) return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            var point = Points[index];
            Points = Points.RemoveAt(index).Insert(index - 1, point);
            EndEdit?.Invoke(this, EventArgs.Empty);

            listBox.SelectedIndex = index - 1;
            UpdateButtonState();
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            if (index == -1 || index >= Points.Count - 1) return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            var point = Points[index];
            Points = Points.RemoveAt(index).Insert(index + 1, point);
            EndEdit?.Invoke(this, EventArgs.Empty);

            listBox.SelectedIndex = index + 1;
            UpdateButtonState();
        }

        private void Editor_BeginEdit(object sender, EventArgs e)
            => BeginEdit?.Invoke(this, EventArgs.Empty);

        private void Editor_EndEdit(object sender, EventArgs e)
            => EndEdit?.Invoke(this, EventArgs.Empty);

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTargetPoints();
            UpdateButtonState();
        }

        private void UpdateTargetPoints()
        {
            if (listBox.SelectedIndex == -1)
            {
                listBox.SelectedIndex = 0;
                return;
            }

            int index = Math.Max(0, listBox.SelectedIndex);
            if (TargetPoints == null || Properties == null || index == -1)
            {
                TargetPoints = Array.Empty<PathPoint>();
                return;
            }

            var list = new List<PathPoint>();
            for (int i = 0; i < Properties.Length; i++)
            {
                var immutableList = Properties[i].GetValue<ImmutableList<PathPoint>>();
                if (immutableList != null && index < immutableList.Count)
                    list.Add(immutableList[index]);
            }
            TargetPoints = list.ToArray();
        }

        private void UpdateButtonState()
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            removeButton.IsEnabled = Points.Count > 2;
            upButton.IsEnabled = index > 0;
            downButton.IsEnabled = index < Points.Count - 1;
        }

        public void SetEditorInfo(IEditorInfo? info) => pointEditor.SetEditorInfo(info);
    }
}
