using UnityEngine;
using UnityEditor;

namespace NataneToon.Editor
{
    /// <summary>
    /// セクションカテゴリ - Inspector UIの各セクションに対応
    /// </summary>
    public enum SectionCategory
    {
        Basic,       // 基本設定 (水色)
        Shading,     // シェーディング (紫)
        Lighting,    // ライティング (橙)
        Effects,     // エフェクト (緑)
        Environment, // 環境 (ティール)
        Advanced     // 詳細設定 (赤茶)
    }

    /// <summary>
    /// Natane Toon Shader Inspector UI用のキャッシュ済みGUIStyleとセクションカラー定義
    /// </summary>
    public static class NataneToonShaderGUIStyles
    {
        // ===== Toggle Status Colors =====
        public static Color ToggleEnabledColor { get { return NataneToonColorPalette.BrandPrimary; } }
        public static Color ToggleDisabledColor { get { return NataneToonEditorTheme.TextMuted; } }
        public static Color ToggleBlockedColor { get { return NataneToonEditorTheme.Warning; } }
        public static Color SamplerBlockHintColor { get { return NataneToonEditorTheme.Warning; } }

        /// <summary>
        /// アクセント地の上に載せる文字色（Pages --accent-ink 相当）。
        /// 明るいシアン背景では白文字よりダーク文字の方が読める。
        /// </summary>
        public static Color AccentInk
        {
            get { return NataneToonEditorTheme.AccentInk; }
        }

        /// <summary>
        /// テーマ依存スタイルの再構築要否を判定するための合成キー。
        /// (NataneToonEditorTheme.Version, IsDark) の組が変わったら古いキャッシュは破棄する。
        /// </summary>
        private static int CurrentThemeKey()
        {
            return NataneToonEditorTheme.CacheKey;
        }

        /// <summary>
        /// セクションカテゴリに対応するカラーを返す
        /// </summary>
        public static Color GetSectionColor(SectionCategory category)
        {
            switch (category)
            {
                case SectionCategory.Basic:       return NataneToonColorPalette.SectionBasic;
                case SectionCategory.Shading:     return NataneToonColorPalette.SectionShading;
                case SectionCategory.Lighting:    return NataneToonColorPalette.SectionLighting;
                case SectionCategory.Effects:     return NataneToonColorPalette.SectionEffects;
                case SectionCategory.Environment: return NataneToonColorPalette.SectionEnvironment;
                case SectionCategory.Advanced:    return NataneToonColorPalette.SectionAdvanced;
                default:                          return NataneToonColorPalette.SectionBasic;
            }
        }

        // ===== Cached GUIStyles =====

        private static GUIStyle _boxOuter;
        private static int _boxOuterThemeKey = int.MinValue;
        /// <summary>
        /// セクション外枠ボックス - helpBoxベース、padding(8,8,4,4)、下マージン付き
        /// P-21: ダークテーマ時にコントラストを強化
        /// </summary>
        public static GUIStyle BoxOuter
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_boxOuter == null || _boxOuterThemeKey != themeKey)
                {
                    _boxOuter = new GUIStyle(EditorStyles.helpBox)
                    {
                        padding = new RectOffset(8, 8, 4, 4),
                        margin = new RectOffset(0, 0, 0, 6)
                    };
                    // P-21: Dark theme contrast enhancement
                    if (NataneToonEditorTheme.IsDark)
                    {
                        _boxOuter.normal.background = NataneToonEditorTextures.Solid(new Color(0.25f, 0.25f, 0.25f, 0.3f));
                    }
                    _boxOuterThemeKey = themeKey;
                }
                return _boxOuter;
            }
        }

        private static GUIStyle _boxInner;
        /// <summary>
        /// 内部コンテンツエリア - インデント付き、padding(12,8,4,4)
        /// </summary>
        public static GUIStyle BoxInner
        {
            get
            {
                if (_boxInner == null)
                {
                    _boxInner = new GUIStyle()
                    {
                        padding = new RectOffset(12, 8, 4, 4),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                }
                return _boxInner;
            }
        }

        private static GUIStyle _sectionHeader;
        /// <summary>
        /// セクションヘッダー - 太字ラベル、fontSize 12
        /// </summary>
        public static GUIStyle SectionHeader
        {
            get
            {
                if (_sectionHeader == null)
                {
                    _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 12
                    };
                }
                return _sectionHeader;
            }
        }

        private static GUIStyle _sectionHeaderFoldout;
        /// <summary>
        /// フォールドアウトヘッダースタイル - 太字、fontSize 12
        /// </summary>
        public static GUIStyle SectionHeaderFoldout
        {
            get
            {
                if (_sectionHeaderFoldout == null)
                {
                    _sectionHeaderFoldout = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 12
                    };
                }
                return _sectionHeaderFoldout;
            }
        }

        private static GUIStyle _toggleBadgeOn;
        private static int _toggleBadgeOnThemeKey = int.MinValue;
        /// <summary>
        /// ON状態バッジ - Pages .param-badge パターン（アクセント文字 on アクセント地15%程度の薄色背景）
        /// </summary>
        public static GUIStyle ToggleBadgeOn
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_toggleBadgeOn == null || _toggleBadgeOnThemeKey != themeKey)
                {
                    _toggleBadgeOn = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(4, 4, 1, 1),
                        fontStyle = FontStyle.Bold,
                        fontSize = 9
                    };
                    _toggleBadgeOn.normal.textColor = NataneToonEditorTheme.Accent;
                    _toggleBadgeOn.normal.background = NataneToonEditorTextures.Solid(NataneToonEditorTheme.AccentSoft);
                    _toggleBadgeOnThemeKey = themeKey;
                }
                return _toggleBadgeOn;
            }
        }

        private static GUIStyle _toggleBadgeOff;
        private static int _toggleBadgeOffThemeKey = int.MinValue;
        /// <summary>
        /// OFF状態バッジ - ミュート文字 on ミュート色8%程度の薄色背景（同じtint-badgeパターン）
        /// </summary>
        public static GUIStyle ToggleBadgeOff
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_toggleBadgeOff == null || _toggleBadgeOffThemeKey != themeKey)
                {
                    _toggleBadgeOff = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(4, 4, 1, 1),
                        fontSize = 9
                    };
                    Color muted = NataneToonEditorTheme.TextMuted;
                    _toggleBadgeOff.normal.textColor = muted;
                    _toggleBadgeOff.normal.background = NataneToonEditorTextures.Solid(new Color(muted.r, muted.g, muted.b, 0.08f));
                    _toggleBadgeOffThemeKey = themeKey;
                }
                return _toggleBadgeOff;
            }
        }

        private static GUIStyle _categoryDividerLabel;
        private static int _categoryDividerLabelThemeKey = int.MinValue;
        /// <summary>
        /// カテゴリ区切りラベル - 中央揃え miniLabel、ミュートカラー
        /// </summary>
        public static GUIStyle CategoryDividerLabel
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_categoryDividerLabel == null || _categoryDividerLabelThemeKey != themeKey)
                {
                    _categoryDividerLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    Color muted = NataneToonEditorTheme.TextMuted;
                    _categoryDividerLabel.normal.textColor = new Color(muted.r, muted.g, muted.b, 0.8f);
                    _categoryDividerLabelThemeKey = themeKey;
                }
                return _categoryDividerLabel;
            }
        }

        private static GUIStyle _searchField;
        /// <summary>
        /// 検索フィールド - テキスト入力用スタイル
        /// </summary>
        public static GUIStyle SearchField
        {
            get
            {
                if (_searchField == null)
                {
                    _searchField = new GUIStyle(EditorStyles.toolbarSearchField)
                    {
                        margin = new RectOffset(4, 4, 4, 4)
                    };
                }
                return _searchField;
            }
        }

        // P-17: Dependency hint label for disabled feature sections
        private static GUIStyle _dependencyHintLabel;
        private static int _dependencyHintLabelThemeKey = int.MinValue;
        /// <summary>
        /// 無効な機能セクションに表示するヒントラベル - イタリック、右揃え、半透明
        /// </summary>
        public static GUIStyle DependencyHintLabel
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_dependencyHintLabel == null || _dependencyHintLabelThemeKey != themeKey)
                {
                    _dependencyHintLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontStyle = FontStyle.Italic,
                        alignment = TextAnchor.MiddleRight
                    };
                    Color muted = NataneToonEditorTheme.TextMuted;
                    _dependencyHintLabel.normal.textColor = new Color(muted.r, muted.g, muted.b, 0.6f);
                    _dependencyHintLabelThemeKey = themeKey;
                }
                return _dependencyHintLabel;
            }
        }

        // ===== Hot-path cached styles (OnGUI内での new GUIStyle を排除) =====

        private static GUIStyle _centeredMiniLabel;
        /// <summary>中央揃え miniLabel（機能オーバービューのチップ等、色は GUI.contentColor で指定）</summary>
        public static GUIStyle CenteredMiniLabel
        {
            get
            {
                if (_centeredMiniLabel == null)
                {
                    _centeredMiniLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                }
                return _centeredMiniLabel;
            }
        }

        private static GUIStyle _centeredMiniLabelBold;
        /// <summary>中央揃え miniLabel の太字版</summary>
        public static GUIStyle CenteredMiniLabelBold
        {
            get
            {
                if (_centeredMiniLabelBold == null)
                {
                    _centeredMiniLabelBold = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                }
                return _centeredMiniLabelBold;
            }
        }

        private static GUIStyle _panelTitleLabel;
        /// <summary>パネル見出し - 太字 fontSize 13（オンボーディング等）</summary>
        public static GUIStyle PanelTitleLabel
        {
            get
            {
                if (_panelTitleLabel == null)
                {
                    _panelTitleLabel = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                }
                return _panelTitleLabel;
            }
        }

        private static GUIStyle _stepNumberLabel;
        private static int _stepNumberLabelThemeKey = int.MinValue;
        /// <summary>手順番号ラベル - 太字、中央揃え、アクセント色</summary>
        public static GUIStyle StepNumberLabel
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_stepNumberLabel == null || _stepNumberLabelThemeKey != themeKey)
                {
                    _stepNumberLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12
                    };
                    _stepNumberLabel.normal.textColor = NataneToonEditorTheme.Accent;
                    _stepNumberLabelThemeKey = themeKey;
                }
                return _stepNumberLabel;
            }
        }

        private static GUIStyle _recommendedRangeLabel;
        private static int _recommendedRangeLabelThemeKey = int.MinValue;
        /// <summary>推奨値表示ラベル - miniLabel、アクセント色</summary>
        public static GUIStyle RecommendedRangeLabel
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_recommendedRangeLabel == null || _recommendedRangeLabelThemeKey != themeKey)
                {
                    _recommendedRangeLabel = new GUIStyle(EditorStyles.miniLabel);
                    _recommendedRangeLabel.normal.textColor = NataneToonEditorTheme.Accent;
                    _recommendedRangeLabelThemeKey = themeKey;
                }
                return _recommendedRangeLabel;
            }
        }

        private static GUIStyle _categoryDividerCenteredLabel;
        private static int _categoryDividerCenteredLabelThemeKey = int.MinValue;
        /// <summary>カテゴリ区切りの中央ラベル - miniLabel、fontSize 12、ミュート80%</summary>
        public static GUIStyle CategoryDividerCenteredLabel
        {
            get
            {
                int themeKey = CurrentThemeKey();
                if (_categoryDividerCenteredLabel == null || _categoryDividerCenteredLabelThemeKey != themeKey)
                {
                    _categoryDividerCenteredLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 12
                    };
                    Color muted = NataneToonEditorTheme.TextMuted;
                    _categoryDividerCenteredLabel.normal.textColor = new Color(muted.r, muted.g, muted.b, 0.8f);
                    _categoryDividerCenteredLabelThemeKey = themeKey;
                }
                return _categoryDividerCenteredLabel;
            }
        }

        // ===== Helper Methods =====

        /// <summary>
        /// 指定されたRectの左端に3px幅のアクセントバーを描画する
        /// </summary>
        public static void DrawAccentBar(Rect rect, Color color)
        {
            Rect barRect = new Rect(rect.x, rect.y, 3f, rect.height);
            EditorGUI.DrawRect(barRect, color);
        }

        /// <summary>
        /// 指定されたRectにセクションカテゴリカラーの背景を描画する
        /// Dark/Lightテーマに応じてアルファ値を調整
        /// </summary>
        public static void DrawSectionBackground(Rect rect, SectionCategory category)
        {
            Color color = GetSectionColor(category);
            float alpha = NataneToonEditorTheme.IsDark ? 0.15f : 0.08f;
            Color bgColor = new Color(color.r, color.g, color.b, alpha);
            EditorGUI.DrawRect(rect, bgColor);
        }
    }
}
