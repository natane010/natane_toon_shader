using System.Collections.Generic;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// IMGUI用に生成する単色/グラデーションテクスチャの共有キャッシュ。
    /// あちこちの GUIStyle が個別に `new Texture2D(1,1)` していたのを一本化する。
    /// NataneToonEditorTheme.Mode の setter からテーマ切替時に Clear() が呼ばれる。
    /// </summary>
    public static class NataneToonEditorTextures
    {
        private static readonly Dictionary<Color, Texture2D> _solidCache = new Dictionary<Color, Texture2D>();
        private static readonly Dictionary<string, Texture2D> _gradientCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// 指定色の1x1テクスチャを返す（キャッシュ済みなら使い回す）。
        /// </summary>
        public static Texture2D Solid(Color c)
        {
            Texture2D tex;
            if (_solidCache.TryGetValue(c, out tex) && tex != null)
                return tex;

            tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.SetPixel(0, 0, c);
            tex.Apply();
            _solidCache[c] = tex;
            return tex;
        }

        /// <summary>
        /// 左から右への線形グラデーションテクスチャを返す（キャッシュ済みなら使い回す）。
        /// </summary>
        public static Texture2D HorizontalGradient(Color a, Color b, int width = 32)
        {
            int w = Mathf.Max(1, width);
            string key = ColorKey(a) + ">" + ColorKey(b) + "@" + w;

            Texture2D tex;
            if (_gradientCache.TryGetValue(key, out tex) && tex != null)
                return tex;

            tex = new Texture2D(w, 1, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < w; x++)
            {
                float t = w <= 1 ? 0f : x / (float)(w - 1);
                tex.SetPixel(x, 0, Color.Lerp(a, b, t));
            }
            tex.Apply();

            _gradientCache[key] = tex;
            return tex;
        }

        private static string ColorKey(Color c)
        {
            return c.r.ToString("F4") + "_" + c.g.ToString("F4") + "_" + c.b.ToString("F4") + "_" + c.a.ToString("F4");
        }

        /// <summary>
        /// キャッシュへの参照を全て手放す。テーマ切替時など、色が総入れ替えになるタイミングで呼ぶ。
        /// テクスチャ自体は破棄しない: テーマキー検証をしていない外部のGUIStyleが
        /// 旧テクスチャを参照し続けても背景が消えないようにするため
        /// （HideAndDontSave の1x1テクスチャなのでリークしても軽微、ドメインリロードで回収される）。
        /// </summary>
        public static void Clear()
        {
            _solidCache.Clear();
            _gradientCache.Clear();
        }
    }
}
