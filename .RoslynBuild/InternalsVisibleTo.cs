// Microsoft.CodeAnalysis.* は ILRepack /internalize により merged DLL 内で internal 化される。
// このパッケージの Editor asmdef からのみ internal 型へアクセスを許可する。
// 他パッケージは Microsoft.CodeAnalysis を見ることができず、 DLL 名衝突も発生しない。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("net.narazaka.unity.liltoon-shader-merger.Editor")]
