using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// 外部からの検証要求を監視して自動実行する開発用ウォッチャー。
    ///
    /// エディタを開いたままバッチ検証を回すための仕組み。バッチモード
    /// (-executeMethod) はプロジェクトが開かれていると Temp/UnityLockfile の
    /// 排他ロックで起動できないため、エディタ側に受け口を用意している。
    ///
    /// 使い方（外部から）:
    ///   1. Library/NataneToon/Audit/verify-request.txt を作る
    ///   2. 数秒待つ
    ///   3. Library/NataneToon/Audit/verify-response.txt を読む（PASS / FAIL / ERROR）
    ///      詳細は同ディレクトリの batch-verify.md
    ///
    /// 要求ファイルが無いときは何もしない。常駐コストはファイル存在チェックのみ。
    /// </summary>
    [InitializeOnLoad]
    internal static class NataneToonAutoVerifyWatcher
    {
        private const double PollIntervalSeconds = 1.0;

        // ドメインリロードを跨いで「Refresh は済んだ」ことを覚えておくためのキー。
        // AssetDatabase.Refresh() はリロードを起こすので、これが無いと Refresh を繰り返す。
        private const string RefreshedKey = "NataneToonAutoVerify.Refreshed";

        private static double _nextPoll;

        private static string ProjectRootPath =>
            Path.GetDirectoryName(Application.dataPath)?.Replace("\\", "/") ?? string.Empty;

        private static string AuditDir =>
            Path.Combine(ProjectRootPath, "Library", "NataneToon", "Audit");

        private static string RequestPath => Path.Combine(AuditDir, "verify-request.txt");
        private static string ResponsePath => Path.Combine(AuditDir, "verify-response.txt");

        static NataneToonAutoVerifyWatcher()
        {
            // バッチモードでは -executeMethod を使うので常駐は不要。
            if (Application.isBatchMode) return;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + PollIntervalSeconds;

            // コンパイル中・インポート中・再生中は触らない。
            // 要求ファイルは消さないので、落ち着いてから次のポーリングで拾われる。
            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string request;
            try
            {
                if (!File.Exists(RequestPath)) return;
                request = File.ReadAllText(RequestPath);
            }
            catch (IOException)
            {
                return; // 書き込み途中の可能性。次のポーリングで再試行する。
            }

            // まずアセットを取り込む。C# を書き換えた直後なら
            // ここでドメインリロードが起き、次のポーリングで続きから走る。
            if (!SessionState.GetBool(RefreshedKey, false))
            {
                SessionState.SetBool(RefreshedKey, true);
                Debug.Log("[NataneAutoVerify] 要求を受理。AssetDatabase を更新します。");
                AssetDatabase.Refresh(ImportAssetOptions.Default);
                return;
            }

            SessionState.SetBool(RefreshedKey, false);
            Run(request);
        }

        private static void Run(string request)
        {
            string status;
            string detail;

            try
            {
                string body = request.Trim();
                Debug.Log("[NataneAutoVerify] 要求: " + body);

                // 要求に "generate" が含まれていれば、検証の前に Properties を生成する。
                // 新規プロパティを追加定義に書いた直後は、生成しないと全バリアントへ広がらない。
                if (body.IndexOf("generate", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bool ok = NataneShaderPropertyCatalogBootstrap.ApplyGenerationHeadless(
                        out int written, out int genProblems, out string genMessage);
                    Debug.Log($"[NataneAutoVerify] 生成: {(ok ? "OK" : "NG")} / {genMessage}");

                    if (!ok)
                    {
                        WriteResponse("FAIL", "生成に失敗: " + genMessage);
                        DeleteRequest();
                        return;
                    }
                }

                bool failed = NataneToonBatchVerify.RunForWatcher(out string reportPath);
                status = failed ? "FAIL" : "PASS";
                detail = reportPath;
                Debug.Log($"[NataneAutoVerify] 完了: {status} / {reportPath}");
            }
            catch (Exception e)
            {
                status = "ERROR";
                detail = e.Message;
                Debug.LogError("[NataneAutoVerify] 例外: " + e);
            }

            WriteResponse(status, detail);
            DeleteRequest();
        }

        private static void WriteResponse(string status, string detail)
        {
            try
            {
                if (!Directory.Exists(AuditDir)) Directory.CreateDirectory(AuditDir);
                File.WriteAllText(
                    ResponsePath,
                    $"{status}\n{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{detail}\n",
                    new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NataneAutoVerify] 応答を書けませんでした: " + e.Message);
            }
        }

        private static void DeleteRequest()
        {
            try
            {
                if (File.Exists(RequestPath)) File.Delete(RequestPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NataneAutoVerify] 要求ファイルを削除できませんでした: " + e.Message);
            }
        }
    }
}
