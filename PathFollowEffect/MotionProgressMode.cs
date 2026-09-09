using System.ComponentModel.DataAnnotations;

namespace PathFollowEffect
{
    /// <summary>
    /// モーションパスの進行指定方法
    /// </summary>
    public enum MotionProgressMode
    {
        [Display(Name = "キーフレーム時刻", Description = "各頂点に設定されたフレーム番号に従って移動します（従来方式）")]
        Keyframe = 1,

        [Display(Name = "イージング関数", Description = "アイテム全体を通してイージング関数に従って移動します")]
        Easing = 2,

        [Display(Name = "ベジェ曲線編集", Description = "ベジェ曲線エディタで進行カーブを自由に編集して移動します")]
        Bezier = 4,
    }
}
