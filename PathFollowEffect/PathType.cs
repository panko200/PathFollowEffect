using System.ComponentModel.DataAnnotations;

namespace PathFollowEffect
{
    /// <summary>
    /// パスの種類
    /// </summary>
    public enum PathType
    {
        [Display(Name = "直線", Description = "制御点を直線で結びます")]
        Straight,

        [Display(Name = "二次ベジェ曲線", Description = "二次ベジェ曲線で滑らかなパスを作ります")]
        QuadraticBezier,

        [Display(Name = "三次ベジェ曲線", Description = "三次ベジェ曲線で自由度の高いパスを作ります")]
        CubicBezier,
    }
}
