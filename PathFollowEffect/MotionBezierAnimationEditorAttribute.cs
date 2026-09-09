using System.Windows;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace PathFollowEffect
{
    /// <summary>
    /// モーションパス用のベジェ曲線エディタ属性
    /// 高さを適切に確保し、全幅で美しく表示する
    /// </summary>
    internal class MotionBezierAnimationEditorAttribute : BezierAnimationEditorAttribute
    {
        public MotionBezierAnimationEditorAttribute()
        {
            PropertyEditorSize = PropertyEditorSize.FullWidth;
        }

        public override FrameworkElement Create()
        {
            var editor = (BezierAnimationEditor)base.Create();
            editor.Height = 200;
            return editor;
        }
    }
}
