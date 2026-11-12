# Tests/CI

パッケージ外の Unity プロジェクトを立てずに実行できる検証一式。
`.github/workflows/pr-validate.yml` の `static` / `shaders` ジョブが同じコマンドを使う。

## 1. パリティ検査（Unity 不要）

`Properties` の `[Toggle(KEYWORD)]` が立てるキーワードを、**どのパスもコンパイルしていない**
状態を検出する。この状態になるとインスペクタのトグルは何も変えず、
誰も読まないキーワードだけがマテリアルへ書き込まれる。

```bash
python Tests/CI/check_shader_parity.py --repo-root .
```

| 終了コード | 意味 |
|---|---|
| 0 | 未宣言の検出なし |
| 1 | 未宣言の検出あり、または宣言テーブルが腐っている |
| 2 | 入力が見つからない等、検査自体が成立しなかった |

判定と宣言テーブルは C# 版
（`Editor/NataneToon/Integration/NataneShaderParityChecker.cs`）を単一の真実として読み取る。
片方だけ更新して食い違うことがない。同じ内容を EditMode テスト
`Tests/Editor/NataneShaderVariantParityTests.cs` からも検査している。

**意図的な除外は宣言テーブルへ理由付きで書くこと。** 暗黙の欠落は許さない設計にしてある。

## 2. シェーダーコンパイル検証（Unity 必要）

パッケージを `file:` 参照するミニプロジェクトを作り、
`ShaderUtil.GetShaderMessages` で全シェーダーを検証する。
**error / warning を両方ゼロに保つ**のが合格条件。

```bash
Tests/CI/setup-validation-project.sh --unity "/path/to/Unity" 
```

| オプション | 内容 |
|---|---|
| `--unity PATH` | Unity 実行ファイル（必須。`--no-run` 時は不要） |
| `--out DIR` | プロジェクトの作成先（既定: `../NataneToonValidation`） |
| `--repo DIR` | パッケージルート（既定: このスクリプトの2つ上） |
| `--keep` | 既存のプロジェクトディレクトリを消さない |
| `--no-run` | プロジェクト生成のみ。Unity は起動しない |

| 終了コード | 意味 |
|---|---|
| 0 | error / warning ともにゼロ |
| 1 | error または warning あり |
| 2 | 検証メソッドまで到達しなかった（多くは C# コンパイルエラー） |
| 3 | 引数不正 / セットアップ失敗 |

### 終了コード 2 について

`error CS` で C# のコンパイルが落ちると Unity は `-executeMethod` まで到達せず、
シェーダー検証が**走らないまま終了**する。何もしないと「合格」と見分けがつかないので、
スクリプト側でログを grep して明示的に区別している。
この終了コードが出たときは、まずログの `error CS` を確認すること。

### 検証する Unity バージョン

| 用途 | バージョン |
|---|---|
| VRChat 相当 | `2022.3.28f1` |
| Unity 6 | `6000.0.55f1` |

`--out` を分けて両方走らせる。

```bash
Tests/CI/setup-validation-project.sh --unity "$UNITY_2022" --out /tmp/val-2022
Tests/CI/setup-validation-project.sh --unity "$UNITY_6000" --out /tmp/val-6000
```

### URP の SubShader（Unity 必要）

上の検証は Built-in RP のプロジェクトで走るので、URP の SubShader（`PackageRequirements` で
URP 導入時だけ有効）は**一切コンパイルされない**。URP 側は別のスクリプトで検証する。

```bash
Tests/CI/setup-urp-validation-project.sh --unity "/path/to/Unity"
```

URP を入れたミニプロジェクトを作り、`RenderPipeline=UniversalPipeline` の SubShader の全パスを、
マテリアル × パイプラインのキーワード組み合わせでコンパイルする（`ValidateURP.cs.txt`）。
URP はエディタ同梱の版を使うので、確認したいエディタごとに `--out` を分けて実行する。
オプションと終了コードは上と同じ（`--keep` は無い。既定の出力先は `../NataneToonValidationURP`）。

| 用途 | 検証したバージョン |
|---|---|
| Unity 6 用 SubShader | `6000.3.11f1`（URP 17.3）、`6000.5.7f1`（URP 17.5） |
| 互換 SubShader（URP 14 〜 17.0） | `2022.3.33f1`（URP 14.0.11） |

キーワードで切り替わる URP のコードを足したら、`ValidateURP.cs.txt` の `MaterialSets` /
`PipelineSets` に組み合わせを足すこと。

### URP SubShader の生成（Unity 不要）

URP の SubShader は 6 つの `.shader` に同じ構成で入っている。手で揃えるとずれるので生成する。

```bash
python Tests/CI/generate_urp_subshaders.py --repo-root .          # 生成
python Tests/CI/generate_urp_subshaders.py --repo-root . --check  # 差分があれば exit 1
```

## 3. コミット前チェック（Unity 不要）

`.meta` の欠落・孤児 `.meta`、`Documentation~/` の未追跡ファイル、
バージョン表記5箇所の一致を検査する。

```bash
python Tests/CI/precommit_check.py --repo-root .
```

`git config core.hooksPath` で任意にフックとして導入できるが、**強制はしない**。
リポジトリ共有フックは環境差で事故りやすいため、CI の `static` ジョブを本線にしている。

### `Documentation~/` の注意

グローバル gitignore の `*~` が `Documentation~/` にマッチするため、
ここへの新規ファイルは **`git add -f` が必須**。`git add -A` では静かにスキップされる。
