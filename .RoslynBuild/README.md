# Roslyn DLL Repack

このフォルダは `Microsoft.CodeAnalysis.CSharp` を `Narazaka.Unity.LilToonShaderMerger.Roslyn.dll` 単一 DLL に ILRepack + internalize するためのビルド設定。

## 出力

- 出力先: `../Editor/Plugins/Narazaka.Unity.LilToonShaderMerger.Roslyn.dll`
- Microsoft.CodeAnalysis.* の全型は internalize されて取り込まれるため、 他パッケージが同名 DLL を持っていても衝突しない

## ビルド方法

```bash
dotnet build -c Release
```

ビルド成功時に `../Editor/Plugins/` に DLL が自動コピーされる。

事前要件:
- .NET SDK (netstandard2.0 対応版)
- `dotnet tool install -g dotnet-ilrepack` で ilrepack CLI 導入

## 注意

- このフォルダは `.` 接頭辞のため Unity の Asset import 対象外
- `bin/`, `obj/` 等のビルド産物はコミット対象外
- **出力 DLL (`Editor/Plugins/Narazaka.Unity.LilToonShaderMerger.Roslyn.dll`) は gitignore されており、ソース repository にはコミットされない**
- Unity でパッケージを使う前に **必ずこの dotnet build を実行** して DLL を生成する必要がある (リリース zip 配布時はビルド済 DLL を同梱する)
- Microsoft.CodeAnalysis.CSharp の version 更新時は rebuild してから動作確認
