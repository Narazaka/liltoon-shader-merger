# lilToon Shader Merger

複数の lilToon カスタムシェーダー (例: もっちりシェーダー、 うずもれシェーダー 等) を 1 つのカスタムシェーダーに自動合成する Unity Editor ツール。

カスタムシェーダーの `custom.hlsl` / `custom_insert.hlsl` / `.lilblock` / `.lilcontainer` / `CustomInspector.cs` をパース・合成し、 出力フォルダに merge 済みシェーダー一式を生成する。

## Install

### VCC用インストーラーunitypackageによる方法（おすすめ）

https://github.com/Narazaka/liltoon-shader-merger/releases/latest から `net.narazaka.unity.liltoon-shader-merger-installer.zip` をダウンロードして解凍し、対象のプロジェクトにインポートする。

### VCCによる方法

1. https://vpm.narazaka.net/ から「Add to VCC」ボタンを押してリポジトリをVCCにインストールします。
2. VCCでSettings→Packages→Installed Repositoriesの一覧中で「Narazaka VPM Listing」にチェックが付いていることを確認します。
3. アバタープロジェクトの「Manage Project」から「lilToon Shader Merger」をインストールします。

## Usage

1. `Assets/Create > lilToon Shader Merger > Merger Settings` で設定アセットを作成
2. Inspector の **Scan Project** ボタンで合成対象のカスタムシェーダーフォルダを選択 (または ObjectField に直接ドラッグ)
3. `shaderName` (例: `Merged/MotchiriUzumore`) と `outputFolder` を設定
4. **Dry Run** で衝突や警告がないか確認
5. **Build** で merged シェーダーを出力

衝突戦略 (`propertyConflict` / `functionConflict` / `replaceConflict` / `textureConflict`) は ErrorOut / PreferFirst / PreferLast から選択可能。

## Changelog

- 0.3.0-alpha.0:
  - (fix): 元 Inspector が自クラスを参照するメンバー（例: Uzumore 1.0.20 の `Copy && Convert` メニュー）を含む場合、合成後に未定義型エラーになる問題を修正
  - (feature): ソースの `.hlsl` / `.lilcontainer` / `.lilblock` が `#include` するファイル（`lil_tessellation_cancel.hlsl` 等）を依存関係を辿って常にコピーするようにした
  - (breaking): `copyExtraFiles` 設定を廃止。代わりに `copyAllExtraFiles`（デフォルト off）を追加。on にすると `#include` されていないファイルもソースフォルダから全てコピーする
- 0.2.0-alpha.0:
  - (feature): 合成出力の `.meta` GUID を決定論化。Merger Settings の `shaderName` と、`outputFolder` から見た各出力ファイルの相対パス（例: `Editor/MergedInspector.cs`）から UUIDv5 で導出する。`shaderName` と出力ファイル構成が同じなら、`outputFolder` の場所やビルドした人によらず同じ GUID になる。
  - (breaking): 既存ビルドのランダム GUID `.meta` は本バージョン以降の Build で決定論値に上書きされる。初回マイグレーション時のみ、その合成シェーダーを参照していたマテリアルの参照が一度切れる。
- 0.1.0-alpha.0: とりあえずリリース

## Build (開発者向け)

このパッケージは Roslyn (`Microsoft.CodeAnalysis.CSharp`) を internalize した DLL に依存する。 リポジトリには DLL 自体は含まれていないため、 ソースから利用する場合は事前にビルドが必要:

```bash
cd Packages/net.narazaka.unity.liltoon-shader-merger/.RoslynBuild
dotnet tool install -g dotnet-ilrepack  # 初回のみ
dotnet build -c Release
```

ビルド成功時に `Editor/Plugins/Narazaka.Unity.LilToonShaderMerger.Roslyn.dll` が生成される。
リリースアーティファクト (VCC 経由インストール) には DLL がビルド済みで同梱される。

## License

[Zlib License](LICENSE.txt)
