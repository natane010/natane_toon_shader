#if VRC_SDK_VRCSDK3
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace NataneToon.Editor
{
    /// <summary>
    /// VRChat SDK build callbacks.
    ///
    /// The VRChat SDK builds AssetBundles directly and never invokes Unity's
    /// IPreprocessBuildWithReport / IProcessSceneWithReport, so the keyword
    /// synchronization and build preparation that NataneBuildPreparationBuildHook
    /// performs for Player builds are hooked into the SDK pipeline here.
    /// Compiled only when the VRChat SDK defines VRC_SDK_VRCSDK3.
    /// </summary>
    public sealed class NataneVRChatBuildCallbacks : IVRCSDKBuildRequestedCallback, IVRCSDKPreprocessAvatarCallback
    {
        // Run early so tools that follow (and the shader variant stripper) see synchronized materials.
        public int callbackOrder => -1000;

        /// <summary>
        /// Called once per avatar/world build request, before any bundle is built.
        /// </summary>
        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            try
            {
                NataneShaderKeywordSynchronizer.SynchronizeAllNataneMaterials();
                NataneBuildPreparationService.PrepareForBuild(forceRefresh: true, logSummary: false);
                ShaderVariantStripper.ResetForNewBuild();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneToonShader] VRChat build preparation failed, continuing without it: {e.Message}");
            }

            // Never block the upload because of a preparation failure.
            return true;
        }

        /// <summary>
        /// Called with the avatar clone that is about to be uploaded. Materials on the
        /// avatar may live in Packages/ or outside the indexed folders, so synchronize
        /// exactly the materials it references.
        /// </summary>
        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (avatarGameObject == null)
            {
                return true;
            }

            try
            {
                int fixedCount = 0;
                var seen = new HashSet<Material>();
                foreach (Renderer renderer in avatarGameObject.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null || !seen.Add(material))
                        {
                            continue;
                        }

                        if (!NataneShaderCatalog.IsNataneShader(material.shader.name))
                        {
                            continue;
                        }

                        if (NataneShaderKeywordSynchronizer.SynchronizeMaterialKeywords(material))
                        {
                            EditorUtility.SetDirty(material);
                            fixedCount++;
                        }
                    }
                }

                if (fixedCount > 0)
                {
                    Debug.Log($"[NataneToonShader] VRChat upload: synchronized keywords on {fixedCount} material(s) of '{avatarGameObject.name}'.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NataneToonShader] VRChat avatar preprocess failed, continuing without it: {e.Message}");
            }

            return true;
        }
    }
}
#endif
