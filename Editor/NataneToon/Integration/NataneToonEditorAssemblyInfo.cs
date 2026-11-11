using System.Runtime.CompilerServices;

// テスト専用の内部シーム（Snapshot ビルダーの例外注入フック・抽出ヘルパ等）を
// テストアセンブリへ公開する。製品 API は public のまま維持する。
[assembly: InternalsVisibleTo("NataneToon.Editor.Tests")]
