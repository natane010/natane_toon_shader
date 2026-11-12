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
        private static GUIStyle _groupIndexStyle;
        private static GUIStyle _groupSummaryStyle;
        private static GUIStyle _groupLabelStyle;
        // 計測用の使い回し GUIContent（OnGUI ごとの確保を避ける）
        private static readonly GUIContent _measureContent = new GUIContent();
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
            // 見出しは 1 回の OnGUI で何度も描くので、派生スタイルは毎回 new せずここで作っておく。
            _groupIndexStyle = new GUIStyle(_groupStyle) { fontStyle = FontStyle.Bold };
            Color groupBrand = NataneToonColorPalette.BrandPrimary;
            _groupIndexStyle.normal.textColor = new Color(groupBrand.r, groupBrand.g, groupBrand.b, dark ? 0.85f : 1f);
            _groupSummaryStyle = new GUIStyle(_groupStyle) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
            _groupLabelStyle = new GUIStyle(_groupStyle) { clipping = TextClipping.Clip };

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
            // 狭い幅では一行に収まらず切れるので、全文をツールチップでも読めるようにする。
            GUI.Label(rect, new GUIContent(description, description), _captionStyle);
        }

        /// <summary>
        /// プロパティ直下に「推奨: ...」の一行ヒントを描く。
        /// </summary>
        public static void DrawRecommendationHint(string hint)
        {
            if (string.IsNullOrEmpty(hint)) return;
            EnsureStyles();
            // LabelField は高さ 1 行固定のため、折り返した 2 行目以降が切れていた。
            DrawWrappedLabel((IsJapanese ? "推奨: " : "Recommended: ") + hint, _hintStyle);
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

        // ------------------------------------------------------------------
        // 幅可変レイアウト用の共通部品
        //
        // EditorGUILayout.LabelField は「ラベル幅 + フィールド幅」ぶんの最小幅を要求し、
        // 高さも 1 行固定なので、横並びの行に置くとインスペクターを狭めたときに
        // はみ出す / 切れる。可変幅の場所では以下を使うこと。
        // ------------------------------------------------------------------

        private static GUIStyle _wrappedMiniStyle;
        private static GUIStyle _wrappedStyle;

        /// <summary>折り返す miniLabel。</summary>
        public static GUIStyle WrappedMiniLabel
        {
            get
            {
                if (_wrappedMiniStyle == null)
                    _wrappedMiniStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                return _wrappedMiniStyle;
            }
        }

        /// <summary>折り返す通常ラベル。</summary>
        public static GUIStyle WrappedLabel
        {
            get
            {
                if (_wrappedStyle == null)
                    _wrappedStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
                return _wrappedStyle;
            }
        }

        /// <summary>
        /// 幅に合わせて折り返し、必要な行数ぶんの高さを確保するラベル。
        /// インデントは EditorGUI.indentLevel に従う。
        /// </summary>
        public static void DrawWrappedLabel(string text, GUIStyle style = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUIStyle s = style ?? WrappedMiniLabel;
            if (!s.wordWrap) s = new GUIStyle(s) { wordWrap = true };

            float indent = EditorGUI.indentLevel * 15f;
            if (indent > 0f)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(indent);
                GUILayout.Label(text, s, GUILayout.MinWidth(0f));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(text, s, GUILayout.MinWidth(0f));
            }
        }

        /// <summary>
        /// 横並びの行の中で、幅が足りなければ縮んで切れる（行を押し広げない）一行ラベル。
        /// </summary>
        public static void DrawShrinkLabel(string text, GUIStyle style = null, params GUILayoutOption[] options)
        {
            GUIStyle s = style ?? EditorStyles.label;
            var content = new GUIContent(text, text);
            if (options != null && options.Length > 0)
                GUILayout.Label(content, s, options);
            else
                GUILayout.Label(content, s, GUILayout.MinWidth(0f));
        }

        /// <summary>
        /// 現在のインスペクター幅に、指定の最小幅のボタンが横に何個入るか（1 以上）。
        /// Layout と Repaint で同じ値になるよう、矩形ではなくビュー幅から求める。
        /// </summary>
        public static int ColumnsFor(float minItemWidth, int maxColumns, float reservedWidth = 40f)
        {
            float available = Mathf.Max(60f, EditorGUIUtility.currentViewWidth - reservedWidth - EditorGUI.indentLevel * 15f);
            int columns = Mathf.FloorToInt(available / Mathf.Max(20f, minItemWidth));
            return Mathf.Clamp(columns, 1, Mathf.Max(1, maxColumns));
        }

        /// <summary>
        /// ボタン列を、幅が足りなければ複数行に折り返して描く。押されたボタンの添字を返す（なければ -1）。
        /// 固定幅ボタンの横並び（狭いと右端が切れる）の置き換え用。
        /// </summary>
        public static int DrawFlowButtons(GUIContent[] buttons, float minButtonWidth, float height = 20f, GUIStyle style = null, int selected = -1, float reservedWidth = 40f)
        {
            if (buttons == null || buttons.Length == 0) return -1;
            GUIStyle s = style ?? EditorStyles.miniButton;
            int columns = ColumnsFor(minButtonWidth, buttons.Length, reservedWidth);
            int clicked = -1;
            const float gap = 3f;

            for (int start = 0; start < buttons.Length; start += columns)
            {
                Rect row = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true)));
                float cell = (row.width - gap * (columns - 1)) / columns;
                for (int c = 0; c < columns && start + c < buttons.Length; c++)
                {
                    int index = start + c;
                    Rect r = new Rect(row.x + (cell + gap) * c, row.y, cell, row.height);
                    bool isSelected = index == selected;
                    Color oldBg = GUI.backgroundColor;
                    if (isSelected) GUI.backgroundColor = NataneToonEditorTheme.Accent;
                    if (GUI.Button(r, buttons[index], s)) clicked = index;
                    GUI.backgroundColor = oldBg;
                }
                GUILayout.Space(2f);
            }
            return clicked;
        }

        public static int DrawFlowButtons(string[] labels, float minButtonWidth, float height = 20f, GUIStyle style = null, int selected = -1, float reservedWidth = 40f)
        {
            if (labels == null) return -1;
            var contents = new GUIContent[labels.Length];
            for (int i = 0; i < labels.Length; i++) contents[i] = new GUIContent(labels[i]);
            return DrawFlowButtons(contents, minButtonWidth, height, style, selected, reservedWidth);
        }

        /// <summary>
        /// 中央寄せの単独ボタン。希望幅より狭いインスペクターでは幅いっぱいまで縮む
        /// （FlexibleSpace + 固定幅だと狭いときに左右が切れる）。
        /// </summary>
        public static bool DrawCenteredButton(GUIContent content, float preferredWidth, float height = 22f, GUIStyle style = null)
        {
            GUIStyle s = style ?? GUI.skin.button;
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool clicked = GUILayout.Button(content, s, GUILayout.MinWidth(40f), GUILayout.MaxWidth(preferredWidth), GUILayout.ExpandWidth(true), GUILayout.Height(height));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return clicked;
        }

        public static bool DrawCenteredButton(string label, float preferredWidth, float height = 22f, GUIStyle style = null)
        {
            return DrawCenteredButton(new GUIContent(label), preferredWidth, height, style);
        }

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

            // タイトル + チップ 3 個 + ボタン 2 個を 1 行に並べるには約 520px 必要。
            // それ未満では「タイトル + ボタン」「サブタイトル」「チップ」の 3 段に積む。
            // （以前は 1 行固定で、狭めると言語切替と ⋮ メニューが右へ押し出されて押せなくなっていた）
            bool singleRow = IsWide;
            string subtitleText = string.IsNullOrEmpty(subtitle) ? null : subtitle.ToUpperInvariant();

            EditorGUILayout.BeginHorizontal();
            if (singleRow)
            {
                EditorGUILayout.BeginVertical();
                GUILayout.Label(new GUIContent(title, title), _titleStyle, GUILayout.MinWidth(0f), GUILayout.Height(19f));
                if (subtitleText != null)
                    GUILayout.Label(new GUIContent(subtitleText, subtitle), _subtitleStyle, GUILayout.MinWidth(0f), GUILayout.Height(16f));
                EditorGUILayout.EndVertical();

                if (!string.IsNullOrEmpty(variant))
                    DrawStatusChip(variant, NataneInspectorStatus.Neutral, 86f);
                if (!string.IsNullOrEmpty(ratingText))
                    DrawStatusChip(ratingText, ratingStatus, 64f, ratingTooltip);
                DrawStatusChip(sampler, samplerStatus, 82f, SamplerChipTooltip);
            }
            else
            {
                GUILayout.Label(new GUIContent(title, title), _titleStyle, GUILayout.MinWidth(0f), GUILayout.Height(20f));
            }

            if (GUILayout.Button(IsJapanese ? "JP" : "EN", EditorStyles.miniButton, GUILayout.Width(30f), GUILayout.Height(20f)))
                onLanguageClick?.Invoke();
            if (onMenuClick != null && GUILayout.Button("⋮", EditorStyles.miniButton, GUILayout.Width(24f), GUILayout.Height(20f)))
                onMenuClick.Invoke();
            EditorGUILayout.EndHorizontal();

            if (!singleRow)
            {
                if (subtitleText != null)
                    GUILayout.Label(new GUIContent(subtitleText, subtitle), _subtitleStyle, GUILayout.MinWidth(0f), GUILayout.Height(16f));

                // チップは行の幅を等分して並べる（最大 110px）。どれだけ狭くても行からはみ出さない。
                int chipCount = 1 + (string.IsNullOrEmpty(variant) ? 0 : 1) + (string.IsNullOrEmpty(ratingText) ? 0 : 1);
                Rect chipRow = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
                const float chipGap = 4f;
                float chipWidth = Mathf.Min(110f, (chipRow.width - chipGap * (chipCount - 1)) / chipCount);
                float chipX = chipRow.x;
                if (!string.IsNullOrEmpty(variant))
                {
                    DrawStatusChip(new Rect(chipX, chipRow.y, chipWidth, 20f), variant, NataneInspectorStatus.Neutral, variant);
                    chipX += chipWidth + chipGap;
                }
                if (!string.IsNullOrEmpty(ratingText))
                {
                    DrawStatusChip(new Rect(chipX, chipRow.y, chipWidth, 20f), ratingText, ratingStatus, ratingTooltip);
                    chipX += chipWidth + chipGap;
                }
                DrawStatusChip(new Rect(chipX, chipRow.y, chipWidth, 20f), sampler, samplerStatus, SamplerChipTooltip);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(NataneUIConstants.INSPECTOR_SPACE_XS);
        }

        private static string SamplerChipTooltip => L(
            "使用中のテクスチャ枠 / 上限。機能を ON にすると増え、上限に達すると新しい機能を ON にできなくなります（使っていない機能を OFF にすると空きます）。",
            "Texture slots in use / limit. Enabling features uses more; at the limit no further feature can be enabled (turn off unused ones to free slots).");

        public static void DrawStatusChip(string text, NataneInspectorStatus status, float width = 72f, string tooltip = null)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 20f, GUILayout.Width(width), GUILayout.Height(20f));
            DrawStatusChip(rect, text, status, tooltip);
        }

        /// <summary>指定矩形にチップを描く（幅可変レイアウト用）。</summary>
        public static void DrawStatusChip(Rect rect, string text, NataneInspectorStatus status, string tooltip = null)
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
                GUIStyle indexStyle = _groupIndexStyle;
                _measureContent.text = indexText;
                Vector2 indexSize = indexStyle.CalcSize(_measureContent);
                GUI.Label(new Rect(rect.x, rect.y, indexSize.x, rect.height), indexText, indexStyle);
                labelX = rect.x + indexSize.x + 4f;
            }

            // 右端のサマリーの実寸を先に確保し、残りをラベルに使う。
            // （以前はラベル 65% 固定 + サマリーを全幅に重ね描きしていて、狭いと文字が重なった）
            float summaryWidth = 0f;
            if (!string.IsNullOrEmpty(summary))
            {
                GUIStyle right = _groupSummaryStyle;
                _measureContent.text = label;
                float labelNeeded = _groupStyle.CalcSize(_measureContent).x + 6f;
                _measureContent.text = summary;
                summaryWidth = Mathf.Min(right.CalcSize(_measureContent).x + 4f,
                    Mathf.Max(0f, rect.xMax - labelX - labelNeeded));
                if (summaryWidth >= 24f)
                    GUI.Label(new Rect(rect.xMax - summaryWidth, rect.y, summaryWidth, rect.height), new GUIContent(summary, summary), right);
                else
                    summaryWidth = 0f;
            }
            GUIStyle labelStyle = _groupLabelStyle;
            GUI.Label(new Rect(labelX, rect.y, Mathf.Max(0f, rect.xMax - summaryWidth - labelX), rect.height), new GUIContent(label, label), labelStyle);
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

            // 高さはレイアウト側に実際の幅から計算させる。以前は「ビュー幅 - 42」で見積もっていたため、
            // 入れ子のボックス内やスクロールバー表示時に実幅が足りず、最後の行が切れていた。
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(message), _inlineMessageStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(0f));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, NataneToonEditorTheme.IsDark ? 0.10f : 0.08f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), accent);
            }
            GUI.Label(rect, message, _inlineMessageStyle);
        }

        public static int DrawTabBar(int selectedTab, string[] labels, string[] compactLabels)
        {
            EnsureStyles();
            string[] visibleLabels = IsNarrow && compactLabels != null ? compactLabels : labels;
            int count = visibleLabels != null ? visibleLabels.Length : 0;
            if (count == 0) return selectedTab;

            // 420〜520px 付近では長いラベルが 1 タブに収まらず両端が切れていた。
            // 実測して収まらないラベルが 1 つでもあれば短縮ラベルに切り替える。
            if (visibleLabels == labels && compactLabels != null && compactLabels.Length == count)
            {
                float estimatedTabWidth = (EditorGUIUtility.currentViewWidth - 28f) / count;
                for (int i = 0; i < count; i++)
                {
                    _measureContent.text = labels[i];
                    if (_tabStyleActive.CalcSize(_measureContent).x + 8f > estimatedTabWidth)
                    {
                        visibleLabels = compactLabels;
                        break;
                    }
                }
            }

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
