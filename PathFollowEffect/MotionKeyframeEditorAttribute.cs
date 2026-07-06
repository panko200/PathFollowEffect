using System;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Views.Converters;

namespace PathFollowEffect
{
    /// <summary>
    /// モーションキーフレームリストのカスタムエディタ属性
    /// </summary>
    internal class MotionKeyframeEditorAttribute : PropertyEditorAttribute2
    {
        public MotionKeyframeEditorAttribute() => PropertyEditorSize = PropertyEditorSize.FullWidth;

        public override FrameworkElement Create() => new MotionKeyframeEditor();

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not MotionKeyframeEditor editor)
                throw new ArgumentException("control is not MotionKeyframeEditor");

            control.SetBinding(
                MotionKeyframeEditor.KeyframesProperty,
                ItemPropertiesBinding.Create2(itemProperties));
            editor.Properties = itemProperties;
            editor.listBox.SelectedIndex = 0;
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not MotionKeyframeEditor editor)
                throw new ArgumentException("control is not MotionKeyframeEditor");

            BindingOperations.ClearBinding(control, MotionKeyframeEditor.KeyframesProperty);
            editor.Properties = null;
        }
    }
}
