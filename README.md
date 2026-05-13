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
