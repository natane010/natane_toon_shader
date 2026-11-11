using UnityEngine;

namespace NataneToon
{
    /// <summary>
    /// 非 VRChat プロジェクト向けの固定ランタイムプリウォームコンポーネント。
    ///
    /// 警告: VRChat アバター / ワールドでは動作しません（VRChat はユーザー製ランタイムスクリプトを許可しないため）。
    /// VRChat 向けには Editor 限定プリウォーム（Tools > Natane > Shader > Shader Prewarming）を使用してください。
    ///
    /// 使い方: シーン内の GameObject に本コンポーネントを追加し、Inspector で ShaderVariantCollection を割り当てる。
    /// Start() で割り当てられた Collection を WarmUp する（旧スクリプト生成方式の代替）。
    /// </summary>
    [AddComponentMenu("Natane Toon/Runtime Shader Prewarmer (Non-VRChat)")]
    public sealed class NataneRuntimeShaderPrewarmer : MonoBehaviour
    {
        [Tooltip("プリウォームする ShaderVariantCollection。VRChat では動作しません（Editor 限定プリウォームを使用）。")]
        [SerializeField] private ShaderVariantCollection shaderVariants;

        [Tooltip("デバッグログを出力する")]
        [SerializeField] private bool showDebugLogs = false;

        private void Start()
        {
            WarmUp();
        }

        /// <summary>
        /// 割り当てられた ShaderVariantCollection をプリウォームする。任意タイミングで呼び出し可能。
        /// </summary>
        public void WarmUp()
        {
            if (shaderVariants == null)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[NataneRuntimeShaderPrewarmer] ShaderVariantCollection が割り当てられていません。");
                return;
            }

            float startTime = Time.realtimeSinceStartup;
            shaderVariants.WarmUp();

            if (showDebugLogs)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                Debug.Log($"[NataneRuntimeShaderPrewarmer] プリウォーム完了: {shaderVariants.variantCount} バリアント / {elapsed:F3} 秒");
            }
        }
    }
}
