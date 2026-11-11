using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Natane Toon Shader統一カラーパレット
    /// Unified color palette for consistent UI design
    /// </summary>
    public static class NataneToonColorPalette
    {
        // Primary Colors (Pages brand tokens: natanetoon.com styles.css)
        // Dark: --accent #43E5FF / --accent-2 #A07BFF
        // Light: --accent #0B93B3 / --accent-2 #6B4FD8
        public static Color BrandPrimary
        {
            get { return NataneToonEditorTheme.Accent; }
        }

        public static Color BrandSecondary
        {
            get { return NataneToonEditorTheme.Accent2; }
        }

        // Status Colors (theme-aware: dark keeps the legacy bright values,
        // light switches to darker tones that stay legible on white)
        public static Color Success { get { return NataneToonEditorTheme.Success; } }
        public static Color Warning { get { return NataneToonEditorTheme.Warning; } }
        public static Color Error { get { return NataneToonEditorTheme.Error; } }

        // Info: brand cyan so inline messages match the Pages accent
        public static Color Info
        {
            get { return NataneToonEditorTheme.Accent; }
        }

        // Performance Rating Colors (theme-aware: light variants keep the hue
        // identity but darken so ratings remain readable as text on white)
        public static Color PerformanceA
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.2f, 0.9f, 0.2f) : new Color(0.10f, 0.55f, 0.13f); }
        }

        public static Color PerformanceB
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.6f, 0.9f, 0.3f) : new Color(0.38f, 0.58f, 0.10f); }
        }

        public static Color PerformanceC
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(1.0f, 0.8f, 0.2f) : new Color(0.70f, 0.52f, 0.03f); }
        }

        public static Color PerformanceD
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(1.0f, 0.4f, 0.2f) : new Color(0.78f, 0.24f, 0.09f); }
        }

        // Text Colors (route through the Pages text tokens)
        public static Color TextPrimary { get { return NataneToonEditorTheme.Text; } }
        public static Color TextSecondary { get { return NataneToonEditorTheme.TextDim; } }
        public static Color TextDisabled { get { return NataneToonEditorTheme.TextMuted; } }

        // Section Category Colors (Inspector UI)
        // Hues keep their category identity but saturation/brightness follow the
        // Pages neon palette (cyan/violet leaning, softer warm tones).
        // Light variants darken each hue so accent bars / header text stay
        // legible against a white inspector background.
        public static Color SectionBasic
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.30f, 0.80f, 1.00f) : new Color(0.04f, 0.50f, 0.70f); }
        }

        public static Color SectionShading
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.63f, 0.48f, 1.00f) : new Color(0.42f, 0.31f, 0.85f); }
        }

        public static Color SectionLighting
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.95f, 0.75f, 0.30f) : new Color(0.68f, 0.47f, 0.06f); }
        }

        public static Color SectionEffects
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.30f, 0.85f, 0.70f) : new Color(0.05f, 0.53f, 0.41f); }
        }

        public static Color SectionEnvironment
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.40f, 0.83f, 0.95f) : new Color(0.08f, 0.50f, 0.62f); }
        }

        public static Color SectionAdvanced
        {
            get { return NataneToonEditorTheme.IsDark ? new Color(0.80f, 0.55f, 0.60f) : new Color(0.62f, 0.32f, 0.38f); }
        }

        // Toggle Badge Colors (brand cyan for ON, matching the Pages accent)
        public static Color ToggleOn { get { return BrandPrimary; } }
        public static Color ToggleOff { get { return NataneToonEditorTheme.TextMuted; } }

        /// <summary>
        /// パネル/ヘッダー帯の塗り色。Pages --bg-hover 相当（FollowUnity時は相対導出）。
        /// </summary>
        public static Color PanelBackground
        {
            get { return NataneToonEditorTheme.SurfaceHover; }
        }

        /// <summary>
        /// カードとして持ち上げるパネルの塗り色。Pages --bg-card 相当（FollowUnity時は相対導出）。
        /// </summary>
        public static Color PanelBackgroundElevated
        {
            get { return NataneToonEditorTheme.SurfaceCard; }
        }

        public static Color Divider
        {
            get
            {
                // Pages --border: cool blue hairline (exact token match, so route through it directly)
                return NataneToonEditorTheme.Border;
            }
        }

        /// <summary>
        /// セクション/パネルヘッダーの、少し持ち上げた背景色。Pages --bg-hover 相当。
        /// </summary>
        public static Color PanelHeaderBackground
        {
            get { return NataneToonEditorTheme.SurfaceHover; }
        }

        public static Color PrimaryText
        {
            get { return NataneToonEditorTheme.Text; }
        }

        public static Color SecondaryText
        {
            get { return NataneToonEditorTheme.TextDim; }
        }

        public static Color DisabledText
        {
            get { return NataneToonEditorTheme.TextMuted; }
        }

        /// <summary>
        /// Get performance rating color by rating string
        /// パフォーマンス評価文字列から色を取得
        /// </summary>
        public static Color GetPerformanceColor(string rating)
        {
            switch (rating?.ToUpper())
            {
                case "A": return PerformanceA;
                case "B": return PerformanceB;
                case "C": return PerformanceC;
                case "D": return PerformanceD;
                default: return TextDisabled;
            }
        }
    }
}
