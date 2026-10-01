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

- 0.4.0-alpha.4:
  - (feature): 合成するソースの一部が用意していないシェーダーの種類（例: HawaseGimmickShader に無い Lite / Multi / FakeShadow）を、Build と DryRun で `[NOT PROVIDED BY <ソース>]` として、`Lite/Cutout` や `[Optional] FakeShadow` のような種類名で一覧表示するようにした。出力は従来どおり全ソースの種類の和集合で、それらの種類にもそのソースのコードが入る（動くこともあるが作者の想定外）
  - (feature): Verify Compile で、ソースが用意していない種類で出たメッセージを `[NOT PROVIDED BY <ソース>]` に分類し、合成処理が原因の `[CAUSED BY MERGE]` と区別するようにした
- 0.4.0-alpha.3:
  - (fix): `custom.hlsl` の `#define` 以外の内容（`#if` などの条件分岐、引数付きマクロ、`#undef`、関数定義や `#include`）が合成時に黙って捨てられていた問題を修正。条件分岐はそのまま保持して出力する。たとえば msdfmask の `#if !defined(LIL_LITE)` 内の定義が、これまでは Lite 版にも無条件で入っていた
  - (feature): `BEFORE_*` などの連結型マクロを条件付きで定義しているソースがあっても、ソースごとの補助マクロに分けて元の条件のまま連結するようにした
  - (feature): Merger Settings に「Verify Compile」ボタンを追加。Build 済みの出力シェーダーを実際にコンパイルし、元のシェーダーにも同じメッセージがあるかどうかで `[CAUSED BY MERGE]`（合成で生じたもの）と `[PRE-EXISTING in <ソース>]`（元シェーダー由来）に分けてコンソールに出す。元シェーダー由来のエラーは Warning として扱う
  - (change): コンソールに出す診断の書式を `[Error][分類] 本文` に変更
- 0.4.0-alpha.2:
  - (feature): `.lilcontainer` の構造（`HLSLINCLUDE` 以外の `lilSubShaderBRP` 等の指定や自前の `SubShader`）を lilToon のカスタムシェーダーテンプレートと照合し、テンプレートから書き換えたソースの構造を `sourceFolders` の順番に関係なく土台にするようにした。これまでは常に先頭ソースの構造が使われ、後ろのソースの書き換え（もっちりシェーダーの tess 系の自前 SubShader など）が黙って捨てられていた
  - (feature): 同じ `.lilcontainer` を複数のソースが別々に書き換えている場合と、あるソースの `HLSLINCLUDE` を合成後の構造に置けない場合は、Error で停止するようにした。DryRun でも検出される
  - (fix): `HLSLINCLUDE` ブロックを出現順ではなく、どの Shader / SubShader / Pass の中にあるかで対応付けるようにした。SubShader を自前で書くソースとそうでないソースの組み合わせで、ブロック数の違いによる誤った警告が出ていた
- 0.4.0-alpha.1:
  - (fix): `HLSLINCLUDE` ブロックを複数持つ `.lilcontainer`（Shader 直下と SubShader 内に持つ tess 系など）で、すべてのブロックが 1 つ目の内容で上書きされていた問題を修正。SubShader 側の `#define LIL_TESSELLATION` などが消え、`undeclared identifier '_TessEdge'` などのエラーになっていた
  - (fix): もっちりシェーダーの `custom_fur.hlsl` のように、`.lilcontainer` が `custom.hlsl` の代わりに include する派生ファイルを、他ソースの `custom.hlsl` と合成して出力するようにした。これまでは派生ファイルと合成済みの `custom.hlsl` が両方 include され、Fur 系で `invalid subscript 'uv23'` などのエラーになっていた。派生ファイルが `custom.hlsl` を include し、`#undef` で一部を差し替える形にも対応
  - (fix): `HLSLINCLUDE` の合成時に `#if` / `#endif` などの条件ディレクティブまで重複除去され、2 つ目以降のソースで条件分岐の対応が崩れる問題を修正
  - (feature): ソース間で `HLSLINCLUDE` ブロックの数が異なる場合と、1 つのブロックで複数の派生ファイルが include される場合に Warning を出すようにした
- 0.4.0-alpha.0:
  - (breaking): 合成出力の `.meta` GUID の導出元を Merger Settings の `shaderName` から「構成シェーダー名の列（各ソースの ShaderName、`sourceFolders` 順）」に変更。出力名や出力フォルダに関わらず、同じシェーダーの組み合わせなら同じ GUID になり、合成シェーダーを参照するマテリアルを別プロジェクトへ持ち込んでも参照が繋がる。0.2.0 / 0.3.0 で生成した GUID からは再び変わるため、既存マテリアルの参照が一度切れる
  - (feature): 出力ファイルを先に全て計画し、GUID 衝突（同じ構成を同一プロジェクト内の別フォルダへビルド）や同名出力の二重書き込みがあれば何も書かずに Error で停止する
  - (fix): extra ファイルのコピーで出力フォルダ自身の `.meta` を上書きしていた問題を修正。`#include` の探索は実際に出力へ流れ込むファイルからのみ行い、ソースフォルダ外を指す include は警告して飛ばす
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
