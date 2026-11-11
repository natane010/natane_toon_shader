using System;
using System.Collections.Generic;

namespace NataneToon.Editor
{
    public static class NataneShaderCatalog
    {
        public struct ShaderPassConfig
        {
            public string name;
            public bool hasForwardAdd;
            public bool hasShadowCaster;
            public bool hasMeta;
            public bool hasNormalPass;

            public ShaderPassConfig(string name, bool hasForwardAdd, bool hasShadowCaster, bool hasMeta = false, bool hasNormalPass = false)
            {
                this.name = name;
                this.hasForwardAdd = hasForwardAdd;
                this.hasShadowCaster = hasShadowCaster;
                this.hasMeta = hasMeta;
                this.hasNormalPass = hasNormalPass;
            }
        }

        public static readonly ShaderPassConfig[] ShaderConfigs =
        {
            new ShaderPassConfig("Natane/Toon Shader", true, true),
            new ShaderPassConfig("Natane/Toon Shader (ScreenEdge Split)", true, true, false, true),
            new ShaderPassConfig("Natane/Toon Shader (Cutout)", true, true),
            new ShaderPassConfig("Natane/Toon Shader (Transparent)", true, false),
            new ShaderPassConfig("Natane/Toon Shader (Lite)", true, true),
            new ShaderPassConfig("Natane/Toon Shader (Cutout Lite)", true, true),
            new ShaderPassConfig("Natane/Toon Shader (Transparent Lite)", true, false),
            new ShaderPassConfig("Natane/Toon Shader (Ghost)", true, false),
            new ShaderPassConfig("Natane/Toon Shader (X-Ray)", true, true),
            new ShaderPassConfig("Natane/Toon Shader (Fur)", true, true),
            new ShaderPassConfig("Natane/Toon Shader (Fur Lite)", true, true),
            new ShaderPassConfig("Natane/Toon Shader (Background)", true, true, true),
            new ShaderPassConfig("Natane/Toon Shader (Particle)", false, false),
            new ShaderPassConfig("Natane/Toon Shader Wirelight", false, false),
            new ShaderPassConfig("Natane/Eye", false, false),
            // FakeShadow は単一 Pass。ForwardAdd も ShadowCaster も持たない
            // （落ち影そのものが影を落とすと二重に暗くなる）。
            new ShaderPassConfig("Natane/Toon Shader FakeShadow", false, false),
            // new ShaderPassConfig("Natane/Screen FX Overlay", false, false),
        };

        public static readonly HashSet<string> ShaderNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Natane/Toon Shader",
            "Natane/Toon Shader (ScreenEdge Split)",
            "Natane/Toon Shader (Cutout)",
            "Natane/Toon Shader (Transparent)",
            "Natane/Toon Shader (Lite)",
            "Natane/Toon Shader (Cutout Lite)",
            "Natane/Toon Shader (Transparent Lite)",
            "Natane/Toon Shader (Ghost)",
            "Natane/Toon Shader (X-Ray)",
            "Natane/Toon Shader (Fur)",
            "Natane/Toon Shader (Fur Lite)",
            "Natane/Toon Shader (Background)",
            "Natane/Toon Shader (Particle)",
            "Natane/Toon Shader Wirelight",
            "Natane/Eye",
            "Natane/Toon Shader FakeShadow",
            // "Natane/Screen FX Overlay"
        };

        public static bool IsNataneShader(string shaderName)
        {
            return !string.IsNullOrEmpty(shaderName) && ShaderNames.Contains(shaderName);
        }

        public static bool IsLilToonShader(string shaderName)
        {
            return !string.IsNullOrEmpty(shaderName) &&
                   (shaderName.Contains("lilToon", StringComparison.OrdinalIgnoreCase) ||
                    shaderName.StartsWith("_lil/", StringComparison.OrdinalIgnoreCase));
        }
    }
}
