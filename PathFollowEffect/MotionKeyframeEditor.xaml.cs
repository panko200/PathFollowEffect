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
    /// モーションキーフレームリストのカスタムエディタUI
    /// フレーム番号付きのキーフレームの追加・削除・編集を行う
    /// </summary>
    public partial class MotionKeyframeEditor : System.Windows.Controls.UserControl, IPropertyEditorControl2, IPropertyEditorControl
    {
        public static readonly DependencyProperty KeyframesProperty =
            DependencyProperty.Register(
                nameof(Keyframes),
                typeof(ImmutableList<MotionKeyframe>),
                typeof(MotionKeyframeEditor),
                new FrameworkPropertyMetadata(
                    ImmutableList<MotionKeyframe>.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    (s, _) =>
                    {
                        if (s is MotionKeyframeEditor editor)
                        {
                            editor.UpdateTargetKeyframes();
                            editor.UpdateButtonState();
                        }
                    }));

        public static readonly DependencyProperty TargetKeyframesProperty =
            DependencyProperty.Register(
                nameof(TargetKeyframes),
                typeof(IEnumerable<MotionKeyframe>),
                typeof(MotionKeyframeEditor),
                new PropertyMetadata(null));

        private ItemProperty[]? properties;

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ImmutableList<MotionKeyframe> Keyframes
        {
            get => (ImmutableList<MotionKeyframe>)GetValue(KeyframesProperty);
            set => SetValue(KeyframesProperty, value);
        }

        public IEnumerable<MotionKeyframe>? TargetKeyframes
        {
            get => (IEnumerable<MotionKeyframe>?)GetValue(TargetKeyframesProperty);
            set => SetValue(TargetKeyframesProperty, value);
        }

        public ItemProperty[]? Properties
        {
            get => properties;
            set
            {
                if (properties == value) return;
                properties = value;
                UpdateTargetKeyframes();
            }
        }

        public MotionKeyframeEditor()
        {
            InitializeComponent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = Math.Max(0, listBox.SelectedIndex);
            BeginEdit?.Invoke(this, EventArgs.Empty);

            // 新しいキーフレームを作成
            // 選択中のキーフレームと次のキーフレームの中間位置に追加
            int newFrame;
            double newX, newY;

            if (selectedIndex + 1 >= Keyframes.Count)
            {
                // 末尾に追加
                var last = Keyframes[^1];
                int lastFrame = (int)last.Frame.GetValue(0L, 1L, 30);
                double lastX = last.X.GetValue(0L, 1L, 30);
                double lastY = last.Y.GetValue(0L, 1L, 30);

                if (Keyframes.Count >= 2)
                {
                    var prev = Keyframes[^2];
                    int prevFrame = (int)prev.Frame.GetValue(0L, 1L, 30);
                    newFrame = lastFrame + Math.Max(1, lastFrame - prevFrame);
                    double prevX = prev.X.GetValue(0L, 1L, 30);
                    double prevY = prev.Y.GetValue(0L, 1L, 30);
                    newX = lastX + (lastX - prevX);
                    newY = lastY + (lastY - prevY);
                }
                else
                {
                    newFrame = lastFrame + 30;
                    newX = lastX + 50;
                    newY = lastY - 50;
                }
            }
            else
            {
                // 中間に挿入
                var p1 = Keyframes[selectedIndex];
                var p2 = Keyframes[selectedIndex + 1];
                int f1 = (int)p1.Frame.GetValue(0L, 1L, 30);
                int f2 = (int)p2.Frame.GetValue(0L, 1L, 30);
                newFrame = (f1 + f2) / 2;
                newX = (p1.X.GetValue(0L, 1L, 30) + p2.X.GetValue(0L, 1L, 30)) / 2;
                newY = (p1.Y.GetValue(0L, 1L, 30) + p2.Y.GetValue(0L, 1L, 30)) / 2;

                // 同一フレームチェック
                if (newFrame == f1) newFrame = f1 + 1;
            }

            // 同一フレーム重複チェック
            while (Keyframes.Any(k => (int)k.Frame.GetValue(0L, 1L, 30) == newFrame))
                newFrame++;

            var newKeyframe = new MotionKeyframe(newFrame, newX, newY);
            Keyframes = Keyframes.Insert(selectedIndex + 1, newKeyframe);

            EndEdit?.Invoke(this, EventArgs.Empty);
            listBox.SelectedIndex = selectedIndex + 1;
            UpdateButtonState();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            if (index == -1 || Keyframes.Count <= 2) return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            Keyframes = Keyframes.RemoveAt(index);
            EndEdit?.Invoke(this, EventArgs.Empty);

            listBox.SelectedIndex = Math.Max(0, index - 1);
            UpdateButtonState();
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            if (index <= 0) return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            var kf = Keyframes[index];
            Keyframes = Keyframes.RemoveAt(index).Insert(index - 1, kf);
            EndEdit?.Invoke(this, EventArgs.Empty);

            listBox.SelectedIndex = index - 1;
            UpdateButtonState();
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            if (index == -1 || index >= Keyframes.Count - 1) return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            var kf = Keyframes[index];
            Keyframes = Keyframes.RemoveAt(index).Insert(index + 1, kf);
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
            UpdateTargetKeyframes();
            UpdateButtonState();
        }

        private void UpdateTargetKeyframes()
        {
            if (listBox.SelectedIndex == -1)
            {
                listBox.SelectedIndex = 0;
                return;
            }

            int index = Math.Max(0, listBox.SelectedIndex);
            if (TargetKeyframes == null || Properties == null || index == -1)
            {
                TargetKeyframes = Array.Empty<MotionKeyframe>();
                return;
            }

            var list = new List<MotionKeyframe>();
            for (int i = 0; i < Properties.Length; i++)
            {
                var immutableList = Properties[i].GetValue<ImmutableList<MotionKeyframe>>();
                if (index < immutableList.Count)
                    list.Add(immutableList[index]);
            }
            TargetKeyframes = list.ToArray();
        }

        private void UpdateButtonState()
        {
            int index = Math.Max(0, listBox.SelectedIndex);
            removeButton.IsEnabled = Keyframes.Count > 2;
            upButton.IsEnabled = index > 0;
            downButton.IsEnabled = index < Keyframes.Count - 1;
        }

        public void SetEditorInfo(IEditorInfo info) => keyframeEditor.SetEditorInfo(info);
    }
}
