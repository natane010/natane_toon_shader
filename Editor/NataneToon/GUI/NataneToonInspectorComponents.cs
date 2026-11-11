using UnityEditor;
using UnityEngine;
using static NataneToon.Editor.NataneToonLocalization;

namespace NataneToon.Editor
{
    public enum NataneInspectorStatus
    {
        Neutral,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Natane系インスペクターで共有する表示部品。
    /// Unity標準の描画を基礎にして、テーマやDPIが変わっても視認性を保つ。
    /// </summary>
    public static class NataneToonInspectorComponents
    {
        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _chipStyle;
        private static GUIStyle _groupStyle;
        private static GUIStyle _inlineMessageStyle;
        private static GUIStyle _captionStyle;
        private static GUIStyle _hintStyle;
        private static GUIStyle _tabStyleInactive;
        private static GUIStyle _tabStyleActive;
        private static int _cachedThemeKey = int.MinValue;

        private static void EnsureStyles()
        {
            bool dark = NataneToonEditorTheme.IsDark;
            int themeKey = (NataneToonEditorTheme.Version << 1) | (dark ? 1 : 0);
            if (_titleStyle != null && _cachedThemeKey == themeKey) return;

            _cachedThemeKey = themeKey;
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            _titleStyle.normal.textColor = NataneToonColorPalette.PrimaryText;

            // HUD風サブタイトル（Pages の .eyebrow 近似）。IMGUIにletter-spacingは
            // 無いため、大文字化＋mini-bold＋ミュートシアンで代替する。
            _subtitleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            Color brand = NataneToonColorPalette.BrandPrimary;
            _subtitleStyle.normal.textColor = new Color(brand.r, brand.g, brand.b, dark ? 0.62f : 0.78f);

            _chipStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(7, 7, 1, 1),
                clipping = TextClipping.Clip
            };

            _groupStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 0, 0, 0)
            };
            // Pages のセクション見出しはメインテキスト色（--text）で強調する
            _groupStyle.normal.textColor = NataneToonEditorTheme.Text;

            _inlineMessageStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                padding = new RectOffset(8, 8, 5, 5)
            };
            _inlineMessageStyle.normal.textColor = NataneToonColorPalette.SecondaryText;

            // 折りたたみ中に出す一行説明。控えめな色でスキャンの邪魔をしない。
            _captionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(9, 6, 0, 2)
            };
            _captionStyle.normal.textColor = new Color(
                NataneToonColorPalette.SecondaryText.r,
                NataneToonColorPalette.SecondaryText.g,
                NataneToonColorPalette.SecondaryText.b,
                dark ? 0.72f : 0.78f);

            // プロパティ下の「推奨: ...」ヒント。青系で目に留まりやすく、でも主張しすぎない。
            _hintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _hintStyle.normal.textColor = dark
                ? new Color(0.55f, 0.78f, 1f, 0.9f)
                : new Color(0.18f, 0.42f, 0.72f, 1f);

            // タブバー（Pages のナビタブ相当）。ホバー時のみ薄い背景を出すため
            // GUI.Button で使う前提の hover ステートを持たせる。
            _tabStyleInactive = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                fontSize = EditorStyles.label.fontSize,
                clipping = TextClipping.Clip
            };
            _tabStyleInactive.normal.textColor = NataneToonEditorTheme.TextDim;
            _tabStyleInactive.hover.textColor = NataneToonEditorTheme.TextDim;
            _tabStyleInactive.hover.background = NataneToonEditorTextures.Solid(NataneToonEditorTheme.SurfaceHover);
            _tabStyleInactive.active.textColor = NataneToonEditorTheme.TextDim;
            _tabStyleInactive.active.background = NataneToonEditorTextures.Solid(NataneToonEditorTheme.SurfaceHover);

            _tabStyleActive = new GUIStyle(_tabStyleInactive)
            {
                fontStyle = FontStyle.Bold
            };
            _tabStyleActive.normal.textColor = NataneToonEditorTheme.Accent;
            _tabStyleActive.hover.textColor = NataneToonEditorTheme.Accent;
            _tabStyleActive.active.textColor = NataneToonEditorTheme.Accent;
        }

        /// <summary>
        /// 難易度に対応する色（バッジのドット用）。
        /// 緑=Basic（安心して触れる）/ 橙=Intermediate / 赤=Advanced。
        /// </summary>
        public static Color GetDifficultyColor(NataneInspectorDifficulty difficulty)
        {
            switch (difficulty)
            {
                case NataneInspectorDifficulty.Basic:
                    return NataneToonColorPalette.Success;
                case NataneInspectorDifficulty.Intermediate:
                    return NataneToonColorPalette.Warning;
                case NataneInspectorDifficulty.Advanced:
                    return NataneToonColorPalette.Error;
                default:
                    return NataneToonColorPalette.SecondaryText;
            }
        }

        public static string GetDifficultyLabel(NataneInspectorDifficulty difficulty)
        {
            bool jp = IsJapanese;
            switch (difficulty)
            {
                case NataneInspectorDifficulty.Basic:
                    return jp ? "初級" : "Basic";
                case NataneInspectorDifficulty.Intermediate:
                    return jp ? "中級" : "Intermediate";
                case NataneInspectorDifficulty.Advanced:
                    return jp ? "上級" : "Advanced";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// セクションヘッダー上に難易度ドットを描く。tooltip付きで初心者が意味を確認できる。
        /// </summary>
        public static void DrawDifficultyDot(Rect headerRect, NataneInspectorDifficulty difficulty, bool hasToggle)
        {
            EnsureStyles();
            float size = 8f;
            // トグルバッジ(ON/OFF)がある場合はその左に、無い場合は右端寄せに配置。
            float x = hasToggle ? headerRect.xMax - 60f : headerRect.xMax - 16f;
            Rect dotRect = new Rect(x, headerRect.y + headerRect.height * 0.5f - size * 0.5f, size, size);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(dotRect, GetDifficultyColor(difficulty));

            Rect hoverRect = new Rect(dotRect.x - 3f, dotRect.y - 4f, size + 6f, size + 8f);
            GUI.Label(hoverRect, new GUIContent(string.Empty,
                (IsJapanese ? "難易度: " : "Difficulty: ") + GetDifficultyLabel(difficulty)));
        }

        /// <summary>
        /// 折りたたみ中のセクションに、見た目の結果を表す一行説明を控えめに描く。
        /// </summary>
        public static void DrawCollapsedDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return;
            EnsureStyles();
            Rect rect = EditorGUILayout.GetControlRect(false, 14f);
            GUI.Label(rect, description, _captionStyle);
        }

        /// <summary>
        /// プロパティ直下に「推奨: ...」の一行ヒントを描く。
        /// </summary>
        public static void DrawRecommendationHint(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return;
            EnsureStyles();
            EditorGUILayout.LabelField((IsJapanese ? "推奨: " : "Recommended: ") + hint, _hintStyle);
        }

        private static GUIStyle _docLinkStyle;
        private static GUIStyle DocLinkStyle
        {
            get
            {
                if (_docLinkStyle == null)
                {
                    _docLinkStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleRight,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(4, 4, 1, 1)
                    };
                }
                // テーマ切り替えに追従できるよう毎回文字色だけ同期する
                Color accent = NataneToonEditorTheme.Accent;
                _docLinkStyle.normal.textColor = accent;
                _docLinkStyle.hover.textColor = accent;
                _docLinkStyle.active.textColor = accent;
                return _docLinkStyle;
            }
        }

        /// <summary>
        /// セクション本文右上に公式ドキュメント(natanetoon.com)へのミニリンクを描く。
        /// DrawBoxedSection内から一元的に呼び出される（各セクションへ個別実装はしない）。
        /// docSlugは "category/slug"（拡張子なし）形式。 例: "lighting/rim-direction"
        /// </summary>
        public static void DrawDocLink(string docSlug)
        {
            if (string.IsNullOrEmpty(docSlug)) return;
            EnsureStyles();

            string label = IsJapanese ? "ドキュメント ↗" : "Docs ↗";
            Rect rect = EditorGUILayout.GetControlRect(false, 15f);
            float width = Mathf.Min(120f, rect.width);
            Rect linkRect = new Rect(rect.xMax - width, rect.y, width, rect.height);

            EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);
            if (GUI.Button(linkRect, label, DocLinkStyle))
            {
                string baseUrl = IsJapanese
                    ? "https://natanetoon.com/params/"
                    : "https://natanetoon.com/en/params/";
                Application.OpenURL(baseUrl + docSlug + ".html");
            }
        }

        public static bool IsCompact => EditorGUIUtility.currentViewWidth < NataneUIConstants.INSPECTOR_WIDTH_COMPACT;
        public static bool IsNarrow => EditorGUIUtility.currentViewWidth < NataneUIConstants.INSPECTOR_WIDTH_NARROW;
        public static bool IsWide => EditorGUIUtility.currentViewWidth >= NataneUIConstants.INSPECTOR_WIDTH_WIDE;

        /// <summary>
        /// ツール系 EditorWindow 共通のテーマ背景。サイト固定モード(SiteDark/SiteLight)のときだけ
        /// ウィンドウ全面を --bg-deep で塗る（マテリアルインスペクターの Task A3 と同じ挙動）。
        /// 各ウィンドウの OnGUI 先頭で `DrawWindowBackground(position)` を呼ぶこと。
        /// </summary>
        public static void DrawWindowBackground(Rect windowPosition)
        {
            if (Event.current.type != EventType.Repaint || !NataneToonEditorTheme.IsSiteMode)
                return;
            EditorGUI.DrawRect(new Rect(0f, 0f, windowPosition.width, windowPosition.height), NataneToonEditorTheme.BgDeep);
        }

        public static void DrawInspectorHeader(
            string title,
            string subtitle,
            string variant,
            string sampler,
            NataneInspectorStatus samplerStatus,
            System.Action onLanguageClick,
            System.Action onMenuClick,
            string ratingText = null,
            NataneInspectorStatus ratingStatus = NataneInspectorStatus.Neutral,
            string ratingTooltip = null)
        {
            EnsureStyles();

            Rect panelRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(panelRect, NataneToonEditorTheme.SurfaceCard);

                // helpBoxの既定背景を塗りつぶしたので、枠線(--border相当)を明示的に描き直す
                Color border = NataneToonEditorTheme.SurfaceBorder;
                EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.y, panelRect.width, 1f), border);
                EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.yMax - 1f, panelRect.width, 1f), border);
                EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.y, 1f, panelRect.height), border);
                EditorGUI.DrawRect(new Rect(panelRect.xMax - 1f, panelRect.y, 1f, panelRect.height), border);

                EditorGUI.DrawRect(new Rect(panelRect.x, panelRect.y, 3f, panelRect.height), NataneToonColorPalette.BrandPrimary);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, _titleStyle, GUILayout.Height(19f));
            if (!string.IsNullOrEmpty(subtitle))
                EditorGUILayout.LabelField(subtitle.ToUpperInvariant(), _subtitleStyle, GUILayout.Height(16f));
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            if (!IsCompact && !string.IsNullOrEmpty(variant))
                DrawStatusChip(variant, NataneInspectorStatus.Neutral, 86f);
            if (!string.IsNullOrEmpty(ratingText))
                DrawStatusChip(ratingText, ratingStatus, IsCompact ? 56f : 64f, ratingTooltip);
            DrawStatusChip(sampler, samplerStatus, IsCompact ? 64f : 82f);

            if (GUILayout.Button(IsJapanese ? "JP" : "EN", EditorStyles.miniButton, GUILayout.Width(30f), GUILayout.Height(20f)))
                onLanguageClick?.Invoke();
            if (onMenuClick != null && GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(24f), GUILayout.Height(20f)))
                onMenuClick.Invoke();
            EditorGUILayout.EndHorizontal();

            if (IsCompact && !string.IsNullOrEmpty(variant))
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(variant, _subtitleStyle);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(NataneUIConstants.INSPECTOR_SPACE_XS);
        }

        public static void DrawStatusChip(string text, NataneInspectorStatus status, float width = 72f, string tooltip = null)
        {
            EnsureStyles();
            Color background;
            Color foreground;
            switch (status)
            {
                case NataneInspectorStatus.Success:
                    background = new Color(NataneToonColorPalette.Success.r, NataneToonColorPalette.Success.g, NataneToonColorPalette.Success.b, 0.22f);
                    foreground = NataneToonEditorTheme.IsDark ? new Color(0.63f, 0.92f, 0.68f) : new Color(0.12f, 0.46f, 0.20f);
                    break;
                case NataneInspectorStatus.Warning:
                    background = new Color(NataneToonColorPalette.Warning.r, NataneToonColorPalette.Warning.g, NataneToonColorPalette.Warning.b, 0.24f);
                    foreground = NataneToonEditorTheme.IsDark ? new Color(1f, 0.82f, 0.45f) : new Color(0.55f, 0.33f, 0.02f);
                    break;
                case NataneInspectorStatus.Error:
                    background = new Color(NataneToonColorPalette.Error.r, NataneToonColorPalette.Error.g, NataneToonColorPalette.Error.b, 0.24f);
                    foreground = NataneToonEditorTheme.IsDark ? new Color(1f, 0.62f, 0.62f) : new Color(0.60f, 0.10f, 0.10f);
                    break;
                default:
                    // Pages --accent-soft: 薄いブランドシアン地 + アクセント文字（.param-badge のtint-badgeパターン）
                    background = NataneToonEditorTheme.AccentSoft;
                    foreground = NataneToonEditorTheme.Accent;
                    break;
            }

            Rect rect = GUILayoutUtility.GetRect(width, 20f, GUILayout.Width(width), GUILayout.Height(20f));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, background);

            Color old = _chipStyle.normal.textColor;
            _chipStyle.normal.textColor = foreground;
            GUI.Label(rect, text, _chipStyle);
            _chipStyle.normal.textColor = old;

            if (!string.IsNullOrEmpty(tooltip))
                GUI.Label(rect, new GUIContent(string.Empty, tooltip));
        }

        public static void DrawGroupHeader(string label, string summary = null, int index = -1)
        {
            EnsureStyles();
            EditorGUILayout.Space(NataneUIConstants.INSPECTOR_SPACE_SM);
            Rect rect = EditorGUILayout.GetControlRect(false, 18f);
            if (Event.current.type == EventType.Repaint)
            {
                // Pages のセクション見出し（h2）風: 左端44pxだけシアン→バイオレットの
                // グラデーションバー、残りは通常のヘアラインでボーダーを引く。
                float gradientWidth = Mathf.Min(44f, rect.width);
                Rect gradientRect = new Rect(rect.x, rect.yMax - 2f, gradientWidth, 2f);
                GUI.DrawTexture(gradientRect, NataneToonEditorTextures.HorizontalGradient(NataneToonEditorTheme.Accent, NataneToonEditorTheme.Accent2));

                Rect hairlineRect = new Rect(rect.x + gradientWidth, rect.yMax - 1f, rect.width - gradientWidth, 1f);
                EditorGUI.DrawRect(hairlineRect, NataneToonColorPalette.Divider);
            }

            float labelX = rect.x;
            // Pages の "01 / LATEST UPDATE" 風インデックス。番号だけブランド色にする。
            if (index >= 0)
            {
                string indexText = (index + 1).ToString("00") + " /";
                GUIStyle indexStyle = new GUIStyle(_groupStyle) { fontStyle = FontStyle.Bold };
                Color brand = NataneToonColorPalette.BrandPrimary;
                indexStyle.normal.textColor = new Color(brand.r, brand.g, brand.b,
                    NataneToonEditorTheme.IsDark ? 0.85f : 1f);
                Vector2 indexSize = indexStyle.CalcSize(new GUIContent(indexText));
                GUI.Label(new Rect(rect.x, rect.y, indexSize.x, rect.height), indexText, indexStyle);
                labelX = rect.x + indexSize.x + 4f;
            }

            GUI.Label(new Rect(labelX, rect.y, rect.x + rect.width * 0.65f - labelX, rect.height), label, _groupStyle);
            if (!string.IsNullOrEmpty(summary))
            {
                GUIStyle right = new GUIStyle(_groupStyle) { alignment = TextAnchor.MiddleRight };
                GUI.Label(rect, summary, right);
            }
            EditorGUILayout.Space(2f);
        }

        public static void DrawInlineMessage(string message, NataneInspectorStatus status)
        {
            EnsureStyles();
            Color accent = status == NataneInspectorStatus.Error
                ? NataneToonColorPalette.Error
                : status == NataneInspectorStatus.Warning
                    ? NataneToonColorPalette.Warning
                    : status == NataneInspectorStatus.Success
                        ? NataneToonColorPalette.Success
                        : NataneToonColorPalette.Info;

            float height = _inlineMessageStyle.CalcHeight(new GUIContent(message), Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 42f));
            Rect rect = GUILayoutUtility.GetRect(0f, height + 6f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, NataneToonEditorTheme.IsDark ? 0.10f : 0.08f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accent);
            }
            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 6f, rect.height - 4f), message, _inlineMessageStyle);
        }

        public static int DrawTabBar(int selectedTab, string[] labels, string[] compactLabels)
        {
            EnsureStyles();
            string[] visibleLabels = IsNarrow && compactLabels != null ? compactLabels : labels;
            int count = visibleLabels != null ? visibleLabels.Length : 0;
            if (count == 0) return selectedTab;

            Rect barRect = GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true));
            float tabWidth = barRect.width / count;

            int newSelected = selectedTab;
            for (int i = 0; i < count; i++)
            {
                Rect tabRect = new Rect(barRect.x + tabWidth * i, barRect.y, tabWidth, barRect.height);
                bool isActive = i == selectedTab;
                GUIStyle style = isActive ? _tabStyleActive : _tabStyleInactive;
                // 数字キー 1〜5 でのタブ切替ショートカットをツールチップで案内する
                GUIContent tabContent = new GUIContent(
                    visibleLabels[i],
                    i < 9 ? L($"ショートカット: Ctrl+{i + 1}", $"Shortcut: Ctrl+{i + 1}") : null);
                if (GUI.Button(tabRect, tabContent, style))
                    newSelected = i;
            }

            if (Event.current.type == EventType.Repaint)
            {
                // タブバー全体の下に1pxヘアライン
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.yMax - 1f, barRect.width, 1f), NataneToonEditorTheme.SurfaceBorder);

                // アクティブタブの下だけ、シアン→バイオレットの2px下線（Pages h2::after 相当）
                Rect underlineRect = new Rect(barRect.x + tabWidth * selectedTab, barRect.yMax - 2f, tabWidth, 2f);
                GUI.DrawTexture(underlineRect, NataneToonEditorTextures.HorizontalGradient(NataneToonEditorTheme.Accent, NataneToonEditorTheme.Accent2));
            }

            return newSelected;
        }
    }
}
