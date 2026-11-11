using UnityEngine;
using UnityEditor;

namespace NataneToon.Editor
{
    /// <summary>
    /// エディターテーマの選択モード。
    /// FollowUnity: Unity Editor のスキン(Pro/Personal)に追従。
    /// SiteDark/SiteLight: natanetoon.com Pages のトークンを固定で使用。
    /// </summary>
    public enum NataneEditorThemeMode
    {
        FollowUnity = 0,
        SiteDark = 1,
        SiteLight = 2
    }

    /// <summary>
    /// エディターUIカラーの単一情報源 (Single Source of Truth)。
    /// natanetoon.com の styles.css で定義されたデザイントークンをミラーし、
    /// Dark/Light 双方の値をテーマモードに応じて返す。
    /// NataneToonColorPalette / NataneToonShaderGUIStyles など、他のカラー定義は
    /// ここのトークンを参照する（新しいUIカラーは直接 new Color せずここに追加する）。
    /// </summary>
    public static class NataneToonEditorTheme
    {
        private const string ModePrefsKey = "NataneToon_EditorThemeMode";

        // ===== Mode / Versioning =====

        private static int _version;

        /// <summary>
        /// テーマが変化するたびに増加するカウンター。
        /// GUIStyle/テクスチャキャッシュはこの値(と IsDark)の組で再構築要否を判定する。
        /// </summary>
        public static int Version { get { return _version; } }

        /// <summary>
        /// テーマ関連のキャッシュを強制的に無効化したい場合に呼ぶ。
        /// </summary>
        public static void BumpVersion()
        {
            _version++;
        }

        /// <summary>
        /// テーマ依存キャッシュ(GUIStyle/テクスチャ等)の再構築要否を判定する合成キー。
        /// (Version, IsDark) の組が変わると値が変わる。
        /// </summary>
        public static int CacheKey
        {
            get { return (_version << 1) | (IsDark ? 1 : 0); }
        }

        private static NataneEditorThemeMode? _modeCache;

        /// <summary>
        /// ユーザーが選択したテーマモード。EditorPrefs に永続化される。
        /// </summary>
        public static NataneEditorThemeMode Mode
        {
            get
            {
                if (_modeCache == null)
                {
                    _modeCache = (NataneEditorThemeMode)EditorPrefs.GetInt(
                        ModePrefsKey, (int)NataneEditorThemeMode.FollowUnity);
                }
                return _modeCache.Value;
            }
            set
            {
                if (_modeCache != null && _modeCache.Value == value)
                    return;

                _modeCache = value;
                EditorPrefs.SetInt(ModePrefsKey, (int)value);
                BumpVersion();
                // テーマが変わったので生成済みの単色/グラデーションテクスチャは作り直す。
                NataneToonEditorTextures.Clear();
            }
        }

        /// <summary>
        /// 現在ダークトーンを使うべきかどうか。
        /// FollowUnity の場合は EditorGUIUtility.isProSkin (Pro/Personalスキン切替) にも追従する。
        /// </summary>
        public static bool IsDark
        {
            get
            {
                switch (Mode)
                {
                    case NataneEditorThemeMode.SiteDark:
                        return true;
                    case NataneEditorThemeMode.SiteLight:
                        return false;
                    default:
                        return EditorGUIUtility.isProSkin;
                }
            }
        }

        /// <summary>
        /// 現在のテーマモードが「サイト固定」(SiteDark/SiteLight) かどうか。
        /// FollowUnityの場合はUnity純正の背景をそのまま活かすため、
        /// パネル全面塗り (Task A3) などの要否判定に使う。
        /// </summary>
        public static bool IsSiteMode
        {
            get { return Mode != NataneEditorThemeMode.FollowUnity; }
        }

        // ===== Color helpers =====

        /// <summary>0xRRGGBB を Color に変換する。</summary>
        private static Color Hex(int rgb, float a = 1f)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }

        private static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        // ===== Design tokens (natanetoon.com styles.css の CSS変数を1:1で反映) =====

        // --bg-deep
        public static Color BgDeep { get { return IsDark ? Hex(0x06080B) : Hex(0xF4F6FA); } }
        // --bg-dark
        public static Color BgDark { get { return IsDark ? Hex(0x0A0D12) : Hex(0xEEF1F6); } }
        // --bg-mid
        public static Color BgMid { get { return IsDark ? Hex(0x0E121A) : Hex(0xFFFFFF); } }
        // --bg-card
        public static Color BgCard { get { return IsDark ? Hex(0x0F141D) : Hex(0xFFFFFF); } }
        // --bg-hover
        public static Color BgHover { get { return IsDark ? Hex(0x151B27) : Hex(0xEEF3F9); } }

        // ===== Relative surface tokens (Phase 4: 背景ハーモニー) =====
        // site トークン(BgCard等)をUnity純正の灰色インスペクター背景へ直接乗せると
        // 浮いて見えるため、FollowUnity時はインスペクター背景から相対的に導出する。

        /// <summary>
        /// インスペクターそのものの背景色。FollowUnityではUnityのデフォルト背景の近似値、
        /// サイト固定モードでは --bg-deep をインスペクター背景として扱う。
        /// </summary>
        public static Color InspectorBg
        {
            get
            {
                if (IsSiteMode) return BgDeep;
                return IsDark ? new Color32(56, 56, 56, 255) : new Color32(200, 200, 200, 255);
            }
        }

        /// <summary>
        /// カード/パネルの塗り色。サイト固定モードでは --bg-card、FollowUnityでは
        /// InspectorBgから相対的に暗く/明るくして「浮いて見える」問題を避ける。
        /// </summary>
        public static Color SurfaceCard
        {
            get
            {
                if (IsSiteMode) return BgCard;
                Color bg = InspectorBg;
                return IsDark ? Color.Lerp(bg, Color.black, 0.22f) : Color.Lerp(bg, Color.white, 0.5f);
            }
        }

        /// <summary>
        /// ホバー/ヘッダー帯などの塗り色。SurfaceCardよりわずかに明暗方向へずらす。
        /// </summary>
        public static Color SurfaceHover
        {
            get
            {
                if (IsSiteMode) return BgHover;
                Color bg = InspectorBg;
                return IsDark ? Color.Lerp(bg, Color.white, 0.06f) : Color.Lerp(bg, Color.black, 0.05f);
            }
        }

        /// <summary>
        /// パネル/カードの枠線色。
        /// </summary>
        public static Color SurfaceBorder
        {
            get
            {
                if (IsSiteMode) return Border;
                return IsDark ? new Color(0f, 0f, 0f, 0.30f) : new Color(0f, 0f, 0f, 0.15f);
            }
        }

        // --accent
        public static Color Accent { get { return IsDark ? Hex(0x43E5FF) : Hex(0x0B93B3); } }
        // --accent-2
        public static Color Accent2 { get { return IsDark ? Hex(0xA07BFF) : Hex(0x6B4FD8); } }
        // --accent-deep
        public static Color AccentDeep { get { return IsDark ? Hex(0x12B7D6) : Hex(0x0B93B3); } }
        // --accent-ink (アクセント地の上に載せる文字色)
        public static Color AccentInk { get { return IsDark ? Hex(0x041318) : Hex(0xFFFFFF); } }

        // --accent-soft
        public static Color AccentSoft { get { return IsDark ? Rgba(67, 229, 255, 0.10f) : Rgba(11, 147, 179, 0.10f); } }
        // --accent-line
        public static Color AccentLine { get { return IsDark ? Rgba(67, 229, 255, 0.55f) : Rgba(11, 147, 179, 0.5f); } }
        // --glow
        public static Color Glow { get { return IsDark ? Rgba(67, 229, 255, 0.38f) : Rgba(11, 147, 179, 0.22f); } }
        // --glow-2
        public static Color Glow2 { get { return IsDark ? Rgba(160, 123, 255, 0.30f) : Rgba(107, 79, 216, 0.18f); } }

        // --text
        public static Color Text { get { return IsDark ? Hex(0xEEF3F7) : Hex(0x10161F); } }
        // --text-dim
        public static Color TextDim { get { return IsDark ? Hex(0xA6B2C0) : Hex(0x43505F); } }
        // --text-muted
        public static Color TextMuted { get { return IsDark ? Hex(0x6D7889) : Hex(0x6A7686); } }

        // --border
        public static Color Border { get { return IsDark ? Rgba(120, 190, 230, 0.14f) : Rgba(20, 40, 70, 0.14f); } }
        // --border-strong
        public static Color BorderStrong { get { return IsDark ? Rgba(120, 200, 240, 0.32f) : Rgba(20, 40, 70, 0.30f); } }

        // --code-bg
        public static Color CodeBg { get { return IsDark ? Hex(0x070A0F) : Hex(0xEEF1F6); } }

        // ===== Status tokens (エディター拡張; styles.css には存在しない) =====
        // Dark は従来の NataneToonColorPalette の値を踏襲し、
        // Light は白背景でもテキスト/アイコンとして読めるよう暗めに振る。

        public static Color Success { get { return IsDark ? new Color(0.30f, 0.80f, 0.30f) : Hex(0x1E7A34); } }
        public static Color Warning { get { return IsDark ? new Color(1.00f, 0.70f, 0.20f) : Hex(0x9A6206); } }
        public static Color Error { get { return IsDark ? new Color(0.90f, 0.30f, 0.30f) : Hex(0xB02A2A); } }

        // ===== Tools メニューからの切替 (発見性向上; インスペクター内の◐ボタンと同じ動作) =====

        private const string MenuFollowUnity = "Tools/Natane/Editor Theme/Follow Unity";
        private const string MenuSiteDark = "Tools/Natane/Editor Theme/Site Dark";
        private const string MenuSiteLight = "Tools/Natane/Editor Theme/Site Light";

        [MenuItem(MenuFollowUnity, false, 900)]
        private static void SetModeFollowUnity() { SetModeAndRepaint(NataneEditorThemeMode.FollowUnity); }

        [MenuItem(MenuSiteDark, false, 901)]
        private static void SetModeSiteDark() { SetModeAndRepaint(NataneEditorThemeMode.SiteDark); }

        [MenuItem(MenuSiteLight, false, 902)]
        private static void SetModeSiteLight() { SetModeAndRepaint(NataneEditorThemeMode.SiteLight); }

        [MenuItem(MenuFollowUnity, true)]
        private static bool ValidateMenuChecks()
        {
            Menu.SetChecked(MenuFollowUnity, Mode == NataneEditorThemeMode.FollowUnity);
            Menu.SetChecked(MenuSiteDark, Mode == NataneEditorThemeMode.SiteDark);
            Menu.SetChecked(MenuSiteLight, Mode == NataneEditorThemeMode.SiteLight);
            return true;
        }

        private static void SetModeAndRepaint(NataneEditorThemeMode mode)
        {
            Mode = mode;
            // 開いているインスペクター/ツールウィンドウへ即時反映
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        // ===== Param-type badge colors (Pages .param-badge-* ; Dark/Lightで共通) =====
        public static Color BadgeRange { get { return Hex(0x64B4FF); } }
        public static Color BadgeToggle { get { return Hex(0x64DC96); } }
        public static Color BadgeColor { get { return Hex(0xFFB464); } }
        public static Color BadgeTexture { get { return Hex(0xC896FF); } }
        public static Color BadgeEnum { get { return Hex(0xFFDC64); } }
        public static Color BadgeFloat { get { return Hex(0xB4C8DC); } }
        public static Color BadgeVector { get { return Hex(0x96DCC8); } }
    }
}
