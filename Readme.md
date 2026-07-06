# プラグイン情報

[![Release](https://img.shields.io/github/v/release/panko200/PathFollowEffect)](https://github.com/panko200/PathFollowEffect)
[![Downloads](https://img.shields.io/github/downloads/panko200/PathFollowEffect/total)](https://github.com/panko200/PathFollowEffect/releases/latest)
[![License](https://img.shields.io/github/license/panko200/PathFollowEffect)](https://github.com/panko200/PathFollowEffect/blob/master/LICENSE)
[![Last Commit](https://img.shields.io/github/last-commit/panko200/PathFollowEffect)](https://github.com/panko200/PathFollowEffect/commits/master)

パス追従  
製作者：Panko200  
配布場所：https://github.com/panko200/PathFollowEffect

## 概要

YukkuriMovieMaker4 にて動作するプラグインです。  
映像エフェクトとして、パスを指定してそれに合わせて動かしたり、テキストを配置したりすることができます。

## 使用方法

1. プラグインをインストールして YMM4 を起動する。
2. アイテムを追加し、映像エフェクトから、「モーションパス」、もしくは「テキストのパスのオプション」を適用する。
3. パスを自由にいじる
4. 任意の設定にすれば、動きます。

## アンインストール方法

1. YMM4を起動して`ヘルプ(H)`>`その他`>`プラグインフォルダを開く`をクリックする。
2. YMM4を終了する。
3. `PathFollowEffect`という名前のフォルダを削除する。

## 注意点

OS : Windows11 (64bit)  
ゆっくりMovieMaker4 : v4.53.0.9  
CPU : Ryzen 7 9700X  
GPU : NVIDIA Geforce RTX 4070 Ti  
RAM : DDR5 32GB x2 (64GB)  
にて動作確認をしています。

### 運用上の注意

テキストのパスのオプションでは、「文字ごとに分割」をつけていないと、正しく配置されません。

モーションパスには、パスの頂点にt(時間)も紐づけられてるので、そこをちゃんと設定してくださいね。

既出のプラグインだったら申し訳ないです。

_免責事項: 作者は、本プラグインの使用または使用不能に起因するいかなる損害についても、一切の責任を負いません。_

## アップデート内容

v1.0.0  
公開

## ライセンス

また、このプラグイン自体のライセンスは、  
MITLicense  
となります。

[MITLicense](./LICENSE)
