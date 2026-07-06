using System;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace PathFollowEffect
{
    /// <summary>
    /// パスポイントリストのカスタムエディタ属性
    /// テキストパスエフェクトで使用
    /// </summary>
    internal class PathPointEditorAttribute : PropertyEditorAttribute2
    {
        public PathPointEditorAttribute() => PropertyEditorSize = PropertyEditorSize.FullWidth;

        public override FrameworkElement Create() => new PathPointsEditor();

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not PathPointsEditor editor)
                throw new ArgumentException("control is not PathPointsEditor");

            control.SetBinding(
                PathPointsEditor.PointsProperty,
                ItemPropertiesBinding.Create2(itemProperties));
            editor.Properties = itemProperties;
            editor.listBox.SelectedIndex = 0;
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not PathPointsEditor editor)
                throw new ArgumentException("control is not PathPointsEditor");

            BindingOperations.ClearBinding(control, PathPointsEditor.PointsProperty);
            editor.Properties = null;
        }
    }
}
