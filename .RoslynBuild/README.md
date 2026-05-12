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

## 注意

- このフォルダは `.` 接頭辞のため Unity の Asset import 対象外
- `bin/`, `obj/` 等のビルド産物はコミット対象外
- Microsoft.CodeAnalysis.CSharp の version 更新時は rebuild + commit
