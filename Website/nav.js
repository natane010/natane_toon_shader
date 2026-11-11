/* ============================================================
   Natane Toon Shader - Documentation Site Navigation
   natanetoon.com  v3 - Bilingual (JA/EN) navigation
   ============================================================ */

(function () {
  'use strict';

  /* ---------- Theme (dark default, light opt-in, persisted) ----------
     Applied synchronously here in <head> to avoid a flash of the wrong theme. */
  var THEME_KEY = 'natane-theme';
  try {
    var savedTheme = localStorage.getItem(THEME_KEY);
    if (savedTheme === 'light' || savedTheme === 'dark') {
      document.documentElement.setAttribute('data-theme', savedTheme);
    }
  } catch (e) { /* storage unavailable */ }

  /* ---------- Language Detection ---------- */
  var IS_EN = location.pathname.indexOf('/en/') !== -1;

  /* ---------- Site links (footer) ---------- */
  var SITE_LINKS = {
    github: 'https://github.com/natane010/natane_toon_shader',
    changelog: 'https://github.com/natane010/natane_toon_shader/blob/develop/CHANGELOG.md',
    /* TODO: replace with the official Booth product URL when available */
    booth: 'https://booth.pm/ja/search/Natane%20Toon%20Shader'
  };

  /* ---------- Site Map Data (JA) ---------- */
  var CATEGORIES_JA = {
    basic:       { label: '基本',           icon: '🎨' },
    lighting:    { label: 'ライティング',   icon: '💡' },
    effects:     { label: 'エフェクト',     icon: '✨' },
    environment: { label: '環境',           icon: '🌐' },
    advanced:    { label: '詳細',           icon: '⚙️' }
  };

  var CATEGORIES_EN = {
    basic:       { label: 'Basic',         icon: '🎨' },
    lighting:    { label: 'Lighting',      icon: '💡' },
    effects:     { label: 'Effects',       icon: '✨' },
    environment: { label: 'Environment',   icon: '🌐' },
    advanced:    { label: 'Advanced',      icon: '⚙️' }
  };

  var TOOL_CATEGORIES_JA = {
    general:      { label: '全般' },
    material:     { label: 'マテリアル' },
    preset:       { label: 'プリセット' },
    effect:       { label: 'エフェクト' },
    optimization: { label: '最適化' },
    migration:    { label: '移行' },
    shader:       { label: 'シェーダー' }
  };

  var TOOL_CATEGORIES_EN = {
    general:      { label: 'General' },
    material:     { label: 'Material' },
    preset:       { label: 'Preset' },
    effect:       { label: 'Effect' },
    optimization: { label: 'Optimization' },
    migration:    { label: 'Migration' },
    shader:       { label: 'Shader' }
  };

  /* Page order within each category */
  var PAGE_ORDER_JA = {
    basic: [
      { file: 'main-texture.html',      label: 'メインテクスチャ' },
      { file: 'color-enhancement.html',  label: 'カラー保持・強化' },
      { file: 'final-blend.html',        label: '最終カラーブレンド' },
      { file: 'surface-finish.html',     label: '表面仕上げ' },
      { file: 'makeup.html',             label: 'メイクアップ' },
      { file: 'shading.html',            label: 'シェーディング' },
      { file: 'main-tex-animation.html', label: 'メインテクスアニメーション' }
    ],
    lighting: [
      { file: 'lighting-general.html',   label: 'ライティング全般' },
      { file: 'soft-lighting.html',      label: 'ソフトライティング' },
      { file: 'backlight.html',          label: 'バックライト' },
      { file: 'light-volumes.html',      label: 'VRC Light Volumes' },
      { file: 'ltcgi.html',              label: 'LTCGI' },
      { file: 'ao.html',                 label: 'アンビエントオクルージョン' },
      { file: 'dithering.html',          label: 'ディザリング' },
      { file: 'shadow-color.html',       label: 'シャドウカラーテクスチャ' },
      { file: 'sdf-shadow.html',         label: 'SDFシャドウマップ' },
      { file: 'shading-grade-map.html',  label: 'シェーディンググレードマップ' },
      { file: 'procedural-ao.html',      label: 'プロシージャルAO' },
      { file: 'cast-shadow-color.html',  label: 'キャストシャドウカラー' },
      { file: 'light-snap.html',         label: 'ライトスナップ' },
      { file: 'dither-stabilize.html',   label: 'ディザ安定化' },
      { file: 'specular-intensity.html', label: 'スペキュラー強度' }
    ],
    effects: [
      { file: 'specular.html',           label: 'スペキュラー' },
      { file: 'rimlight1.html',          label: 'リムライト1' },
      { file: 'rimlight2.html',          label: 'リムライト2' },
      { file: 'rim-direction.html',      label: 'リムライト方向制御' },
      { file: 'sss.html',               label: 'SSS' },
      { file: 'matcap1.html',            label: 'MatCap 1' },
      { file: 'matcap23.html',           label: 'MatCap 2/3' },
      { file: 'glitter.html',            label: 'グリッター' },
      { file: 'waterdrip.html',          label: '雫エフェクト' },
      { file: 'hologram.html',           label: 'ホログラム' },
      { file: 'glitch.html',             label: 'グリッチ' },
      { file: 'decal.html',              label: 'デカル' },
      { file: 'outline.html',            label: 'アウトライン' },
      { file: 'emission.html',           label: 'エミッション' },
      { file: 'dissolve.html',           label: '溶解' },
      { file: 'alpha-mask.html',         label: 'アルファマスク' },
      { file: 'hue-shift.html',          label: '色相シフト' },
      { file: 'audiolink.html',          label: 'AudioLink' },
      { file: 'screen-tone.html',        label: 'スクリーントーン' },
      { file: 'smear.html',              label: 'スメアエフェクト' },
      { file: 'hair-specular.html',      label: 'ヘアスペキュラー' },
      { file: 'fur.html',                label: 'ファー' },
      { file: 'offset-rim-light.html',   label: 'オフセットリムライト' },
      { file: 'kuwahara-filter.html',    label: 'Kuwaharaフィルター' },
      { file: 'color-bleeding.html',     label: 'カラーブリーディング' },
      { file: 'watercolor.html',         label: '水彩エフェクト' },
      { file: 'color-quantize.html',     label: 'カラー量子化' },
      { file: 'lut-3d.html',             label: '3D LUT' },
      { file: 'fake-reflection.html',    label: 'フェイクリフレクション' },
      { file: 'glitch-stretch.html',     label: 'グリッチストレッチ' },
      { file: 'procedural-matcap.html',  label: 'プロシージャルMatCap' },
      { file: 'sss-lut.html',            label: 'SSS LUT' },
      { file: 'ghost.html',              label: 'ゴースト' },
      { file: 'line-boil.html',          label: 'ラインボイル' },
      { file: 'shaped-highlight.html',   label: '形状付きハイライト' },
      { file: 'topographic.html',        label: '等高線 / 断層スライス' },
      { file: 'fx-modulator.html',       label: 'FXモジュレーター' },
      { file: 'lenticular.html',         label: 'レンチキュラー' },
      { file: 'caustics.html',           label: 'コースティクス' },
      { file: 'pixel-art.html',          label: 'ピクセルアート' },
      { file: 'xray.html',               label: 'X-Ray' }
    ],
    environment: [
      { file: 'reflection.html',         label: 'リフレクション' },
      { file: 'iridescence.html',        label: 'イリデッセンス' },
      { file: 'env-rim.html',            label: '環境リム' },
      { file: 'refraction.html',         label: '屈折' },
      { file: 'height-fog.html',         label: 'ハイトフォグ' }
    ],
    advanced: [
      { file: 'normal-map.html',         label: 'ノーマルマップ' },
      { file: 'parallax.html',           label: 'パララックス' },
      { file: 'vat.html',               label: 'VAT' },
      { file: 'backface.html',           label: '裏面テクスチャ' },
      { file: 'video-texture.html',      label: 'ビデオテクスチャ' },
      { file: 'distance-fade.html',      label: '距離フェード' },
      { file: 'vertex-animation.html',   label: '頂点アニメーション' },
      { file: 'rendering.html',          label: 'レンダリング設定' },
      { file: 'tessellation.html',       label: 'テッセレーション' },
      { file: 'gradient-base-color.html', label: 'グラデーションベースカラー' },
      { file: 'height-fade.html',        label: 'ハイトフェード' },
      { file: 'intersection-fade.html',  label: 'インターセクションフェード' },
      { file: 'detail-map.html',         label: 'ディテールマップ' },
      { file: 'triplanar.html',          label: 'トライプレーナー' },
      { file: 'surface-cover.html',      label: 'サーフェスカバー' },
      { file: 'mirror-control.html',     label: 'ミラーコントロール' },
      { file: 'quest-lite.html',         label: 'Quest軽量モード' },
      { file: 'eye-parallax.html',       label: 'アイパララックス' },
      { file: 'screen-edge.html',        label: 'スクリーンエッジ' },
      { file: 'perspective-flat.html',   label: 'パースペクティブフラット' },
      { file: 'face-ortho.html',         label: '顔直交投影' },
      { file: 'mirror-alt-texture.html', label: '鏡・カメラ写り分けテクスチャ' },
      { file: 'normal-warp.html',        label: 'ノーマルワープ' },
      { file: 'smooth-normal.html',      label: 'スムースノーマル' }
    ]
  };

  var PAGE_ORDER_EN = {
    basic: [
      { file: 'main-texture.html',      label: 'Main Texture' },
      { file: 'color-enhancement.html',  label: 'Color Enhancement' },
      { file: 'final-blend.html',        label: 'Final Color Blend' },
      { file: 'surface-finish.html',     label: 'Surface Finish' },
      { file: 'makeup.html',             label: 'Makeup' },
      { file: 'shading.html',            label: 'Shading' },
      { file: 'main-tex-animation.html', label: 'Main Tex Animation' }
    ],
    lighting: [
      { file: 'lighting-general.html',   label: 'Lighting General' },
      { file: 'soft-lighting.html',      label: 'Soft Lighting' },
      { file: 'backlight.html',          label: 'Backlight' },
      { file: 'light-volumes.html',      label: 'VRC Light Volumes' },
      { file: 'ltcgi.html',              label: 'LTCGI' },
      { file: 'ao.html',                 label: 'Ambient Occlusion' },
      { file: 'dithering.html',          label: 'Dithering' },
      { file: 'shadow-color.html',       label: 'Shadow Color Texture' },
      { file: 'sdf-shadow.html',         label: 'SDF Shadow Map' },
      { file: 'shading-grade-map.html',  label: 'Shading Grade Map' },
      { file: 'procedural-ao.html',      label: 'Procedural AO' },
      { file: 'cast-shadow-color.html',  label: 'Cast Shadow Color' },
      { file: 'light-snap.html',         label: 'Light Snap' },
      { file: 'dither-stabilize.html',   label: 'Dither Stabilize' },
      { file: 'specular-intensity.html', label: 'Specular Intensity' }
    ],
    effects: [
      { file: 'specular.html',           label: 'Specular' },
      { file: 'rimlight1.html',          label: 'Rim Light 1' },
      { file: 'rimlight2.html',          label: 'Rim Light 2' },
      { file: 'rim-direction.html',      label: 'Rim Light Direction' },
      { file: 'sss.html',               label: 'SSS' },
      { file: 'matcap1.html',            label: 'MatCap 1' },
      { file: 'matcap23.html',           label: 'MatCap 2/3' },
      { file: 'glitter.html',            label: 'Glitter' },
      { file: 'waterdrip.html',          label: 'Water Drop Effect' },
      { file: 'hologram.html',           label: 'Hologram' },
      { file: 'glitch.html',             label: 'Glitch' },
      { file: 'decal.html',              label: 'Decal' },
      { file: 'outline.html',            label: 'Outline' },
      { file: 'emission.html',           label: 'Emission' },
      { file: 'dissolve.html',           label: 'Dissolve' },
      { file: 'alpha-mask.html',         label: 'Alpha Mask' },
      { file: 'hue-shift.html',          label: 'Hue Shift' },
      { file: 'audiolink.html',          label: 'AudioLink' },
      { file: 'screen-tone.html',        label: 'Screen Tone' },
      { file: 'smear.html',              label: 'Smear Effect' },
      { file: 'hair-specular.html',      label: 'Hair Specular' },
      { file: 'fur.html',                label: 'Fur Rendering' },
      { file: 'offset-rim-light.html',   label: 'Offset Rim Light' },
      { file: 'kuwahara-filter.html',    label: 'Kuwahara Filter' },
      { file: 'color-bleeding.html',     label: 'Color Bleeding' },
      { file: 'watercolor.html',         label: 'Watercolor Effect' },
      { file: 'color-quantize.html',     label: 'Color Quantize' },
      { file: 'lut-3d.html',             label: '3D LUT' },
      { file: 'fake-reflection.html',    label: 'Fake Reflection' },
      { file: 'glitch-stretch.html',     label: 'Glitch Stretch' },
      { file: 'procedural-matcap.html',  label: 'Procedural MatCap' },
      { file: 'sss-lut.html',            label: 'SSS LUT' },
      { file: 'ghost.html',              label: 'Ghost' },
      { file: 'line-boil.html',          label: 'Line Boil' },
      { file: 'shaped-highlight.html',   label: 'Shaped Highlight' },
      { file: 'topographic.html',        label: 'Topographic / Slice Lines' },
      { file: 'fx-modulator.html',       label: 'FX Modulator' },
      { file: 'lenticular.html',         label: 'Lenticular' },
      { file: 'caustics.html',           label: 'Surface Caustics' },
      { file: 'pixel-art.html',          label: 'Pixel Art' },
      { file: 'xray.html',               label: 'X-Ray' }
    ],
    environment: [
      { file: 'reflection.html',         label: 'Reflection' },
      { file: 'iridescence.html',        label: 'Iridescence' },
      { file: 'env-rim.html',            label: 'Environmental Rim' },
      { file: 'refraction.html',         label: 'Refraction' },
      { file: 'height-fog.html',         label: 'Height Fog' }
    ],
    advanced: [
      { file: 'normal-map.html',         label: 'Normal Map' },
      { file: 'parallax.html',           label: 'Parallax' },
      { file: 'vat.html',               label: 'VAT' },
      { file: 'backface.html',           label: 'Backface Texture' },
      { file: 'video-texture.html',      label: 'Video Texture' },
      { file: 'distance-fade.html',      label: 'Distance Fade' },
      { file: 'vertex-animation.html',   label: 'Vertex Animation' },
      { file: 'rendering.html',          label: 'Rendering Settings' },
      { file: 'tessellation.html',       label: 'Tessellation' },
      { file: 'gradient-base-color.html', label: 'Gradient Base Color' },
      { file: 'height-fade.html',        label: 'Height Fade' },
      { file: 'intersection-fade.html',  label: 'Intersection Fade' },
      { file: 'detail-map.html',         label: 'Detail Map' },
      { file: 'triplanar.html',          label: 'Triplanar Mapping' },
      { file: 'surface-cover.html',      label: 'Surface Cover' },
      { file: 'mirror-control.html',     label: 'Mirror Control' },
      { file: 'quest-lite.html',         label: 'Quest Lite Mode' },
      { file: 'eye-parallax.html',       label: 'Eye Parallax' },
      { file: 'screen-edge.html',        label: 'Screen Edge' },
      { file: 'perspective-flat.html',   label: 'Perspective Flat' },
      { file: 'face-ortho.html',         label: 'Face Ortho Projection' },
      { file: 'mirror-alt-texture.html', label: 'Mirror/Camera Alt Texture' },
      { file: 'normal-warp.html',        label: 'Normal Warp' },
      { file: 'smooth-normal.html',      label: 'Smooth Normal' }
    ]
  };

  var TOOL_ORDER_JA = [
    { file: 'dashboard.html',             label: 'ダッシュボード',               cat: 'general' },
    { file: 'help.html',                  label: 'ヘルプ',                       cat: 'general' },
    { file: 'material-validator.html',    label: 'マテリアル検証',               cat: 'material' },
    { file: 'material-editor.html',       label: 'マテリアルエディタ',           cat: 'material' },
    { file: 'material-preview.html',      label: 'マテリアルプレビュー',         cat: 'material' },
    { file: 'material-comparison.html',   label: 'マテリアル比較',               cat: 'material' },
    { file: 'makeup-layer-manager.html',  label: 'メイクアップレイヤー管理',     cat: 'material' },
    { file: 'preset-browser.html',        label: 'プリセットブラウザ',           cat: 'preset' },
    { file: 'color-palette.html',         label: 'カラーパレット',               cat: 'preset' },
    { file: 'preset-generator.html',      label: 'デフォルトプリセット生成',     cat: 'preset' },
    { file: 'preset-regenerator.html',    label: '全プリセット再生成',           cat: 'preset' },
    { file: 'shadow-wizard.html',         label: 'シャドウ調整ウィザード',       cat: 'effect' },
    { file: 'matcap-composer.html',       label: 'MatCapコンポーザー',           cat: 'effect' },
    { file: 'dissolve-generator.html',    label: 'ディゾルブパターン生成',       cat: 'effect' },
    { file: 'rimlight-visualizer.html',   label: 'リムライト方向ビジュアライザー', cat: 'effect' },
    { file: 'screen-fx.html',             label: 'スクリーンエフェクト設定',     cat: 'effect' },
    { file: 'performance-budget.html',    label: 'パフォーマンスバジェット',     cat: 'optimization' },
    { file: 'texture-optimizer.html',     label: 'テクスチャ最適化',             cat: 'optimization' },
    { file: 'outline-optimizer.html',     label: 'アウトライン最適化',           cat: 'optimization' },
    { file: 'refraction-balancer.html',   label: '屈折品質バランサー',           cat: 'optimization' },
    { file: 'liltoon-migration.html',     label: 'lilToon移行',                  cat: 'migration' },
    { file: 'batch-converter.html',       label: '一括マテリアル変換',           cat: 'migration' },
    { file: 'prefab-converter.html',      label: 'プレハブバリアント変換',       cat: 'migration' },
    { file: 'shader-variant-collector.html', label: 'シェーダーバリアント収集',  cat: 'shader' },
    { file: 'light-volumes-helper.html',  label: 'ライトボリュームヘルパー',     cat: 'shader' },
    { file: 'uv-texture-generator.html',  label: 'UVテクスチャ生成',             cat: 'shader' },
    { file: 'map-generator.html',         label: 'マップジェネレーター',         cat: 'shader' },
    { file: 'hierarchy-batch-editor.html', label: 'ヒエラルキー一括編集',        cat: 'material' },
    { file: 'asset-reference-checker.html', label: 'アセット参照チェッカー',     cat: 'optimization' },
    { file: 'smooth-normal-baker.html',   label: 'スムース法線ベイク',           cat: 'shader' },
    { file: 'particle-effect-editor.html', label: 'パーティクルエフェクトエディタ', cat: 'effect' },
    { file: 'diagnostics.html',           label: 'ツール診断',                   cat: 'general' },
    { file: 'ltcgi-auto-detect.html',     label: 'LTCGI自動検出',               cat: 'shader' },
    { file: 'light-volumes-auto-detect.html', label: 'Light Volumes自動検出',    cat: 'shader' },
    { file: 'variant-stripper.html',      label: 'バリアントストリッピング',     cat: 'shader' },
    { file: 'shader-prewarming.html',     label: 'シェーダープリウォーミング',   cat: 'shader' },
    { file: 'eye-setup.html',             label: '目のセットアップ',             cat: 'material' },
    { file: 'gpu-particle-mesh.html',     label: 'GPUパーティクルメッシュ生成',  cat: 'effect' },
    { file: 'stencil-presets.html',       label: 'ステンシルプリセット',         cat: 'shader' },
    { file: 'build-optimization.html',    label: 'ビルド時最適化',               cat: 'optimization' }
  ];

  var TOOL_ORDER_EN = [
    { file: 'dashboard.html',             label: 'Dashboard',                    cat: 'general' },
    { file: 'help.html',                  label: 'Help',                         cat: 'general' },
    { file: 'material-validator.html',    label: 'Material Validator',           cat: 'material' },
    { file: 'material-editor.html',       label: 'Material Editor',              cat: 'material' },
    { file: 'material-preview.html',      label: 'Material Preview',             cat: 'material' },
    { file: 'material-comparison.html',   label: 'Material Comparison',          cat: 'material' },
    { file: 'makeup-layer-manager.html',  label: 'Makeup Layer Manager',         cat: 'material' },
    { file: 'preset-browser.html',        label: 'Preset Browser',               cat: 'preset' },
    { file: 'color-palette.html',         label: 'Color Palette',                cat: 'preset' },
    { file: 'preset-generator.html',      label: 'Default Preset Generator',     cat: 'preset' },
    { file: 'preset-regenerator.html',    label: 'Regenerate All Presets',        cat: 'preset' },
    { file: 'shadow-wizard.html',         label: 'Shadow Adjustment Wizard',     cat: 'effect' },
    { file: 'matcap-composer.html',       label: 'MatCap Composer',              cat: 'effect' },
    { file: 'dissolve-generator.html',    label: 'Dissolve Pattern Generator',   cat: 'effect' },
    { file: 'rimlight-visualizer.html',   label: 'Rim Light Direction Visualizer', cat: 'effect' },
    { file: 'screen-fx.html',             label: 'Screen Effect Settings',       cat: 'effect' },
    { file: 'performance-budget.html',    label: 'Performance Budget',           cat: 'optimization' },
    { file: 'texture-optimizer.html',     label: 'Texture Optimizer',            cat: 'optimization' },
    { file: 'outline-optimizer.html',     label: 'Outline Optimizer',            cat: 'optimization' },
    { file: 'refraction-balancer.html',   label: 'Refraction Quality Balancer',  cat: 'optimization' },
    { file: 'liltoon-migration.html',     label: 'lilToon Migration',            cat: 'migration' },
    { file: 'batch-converter.html',       label: 'Batch Material Converter',     cat: 'migration' },
    { file: 'prefab-converter.html',      label: 'Prefab Variant Converter',     cat: 'migration' },
    { file: 'shader-variant-collector.html', label: 'Shader Variant Collector',  cat: 'shader' },
    { file: 'light-volumes-helper.html',  label: 'Light Volumes Helper',         cat: 'shader' },
    { file: 'uv-texture-generator.html',  label: 'UV Texture Generator',         cat: 'shader' },
    { file: 'map-generator.html',         label: 'Map Generator',               cat: 'shader' },
    { file: 'hierarchy-batch-editor.html', label: 'Hierarchy Batch Editor',      cat: 'material' },
    { file: 'asset-reference-checker.html', label: 'Asset Reference Checker',    cat: 'optimization' },
    { file: 'smooth-normal-baker.html',   label: 'Smooth Normal Baker',          cat: 'shader' },
    { file: 'particle-effect-editor.html', label: 'Particle Effect Editor',      cat: 'effect' },
    { file: 'diagnostics.html',           label: 'Diagnostics',                  cat: 'general' },
    { file: 'ltcgi-auto-detect.html',     label: 'LTCGI Auto-Detect',            cat: 'shader' },
    { file: 'light-volumes-auto-detect.html', label: 'Light Volumes Auto-Detect', cat: 'shader' },
    { file: 'variant-stripper.html',      label: 'Variant Stripping Settings',   cat: 'shader' },
    { file: 'shader-prewarming.html',     label: 'Shader Prewarming',            cat: 'shader' },
    { file: 'eye-setup.html',             label: 'Eye Setup Tool',               cat: 'material' },
    { file: 'gpu-particle-mesh.html',     label: 'GPU Particle Mesh Generator',  cat: 'effect' },
    { file: 'stencil-presets.html',       label: 'Stencil Presets',              cat: 'shader' },
    { file: 'build-optimization.html',    label: 'Build Optimization',           cat: 'optimization' }
  ];

  /* ---------- Header Nav Data ---------- */
  var NAV_JA = [
    { label: 'ホーム', href: '{root}index.html' },
    {
      label: 'パラメータ',
      children: [
        { label: '基本',           href: '{root}params/basic/main-texture.html' },
        { label: 'ライティング',   href: '{root}params/lighting/lighting-general.html' },
        { label: 'エフェクト',     href: '{root}params/effects/specular.html' },
        { label: '環境',           href: '{root}params/environment/reflection.html' },
        { label: '詳細',           href: '{root}params/advanced/normal-map.html' },
        { label: '── 一覧',       href: '{root}params/index.html' }
      ]
    },
    {
      label: 'ツール',
      children: [
        { label: 'マテリアル',     href: '{root}tools/material-validator.html' },
        { label: 'プリセット',     href: '{root}tools/preset-browser.html' },
        { label: 'エフェクト',     href: '{root}tools/shadow-wizard.html' },
        { label: '最適化',         href: '{root}tools/performance-budget.html' },
        { label: '移行',           href: '{root}tools/liltoon-migration.html' },
        { label: 'シェーダー',     href: '{root}tools/shader-variant-collector.html' },
        { label: '── 一覧',       href: '{root}tools/index.html' }
      ]
    }
  ];

  var NAV_EN = [
    { label: 'Home', href: '{root}index.html' },
    {
      label: 'Parameters',
      children: [
        { label: 'Basic',         href: '{root}params/basic/main-texture.html' },
        { label: 'Lighting',      href: '{root}params/lighting/lighting-general.html' },
        { label: 'Effects',       href: '{root}params/effects/specular.html' },
        { label: 'Environment',   href: '{root}params/environment/reflection.html' },
        { label: 'Advanced',      href: '{root}params/advanced/normal-map.html' },
        { label: '── All',        href: '{root}params/index.html' }
      ]
    },
    {
      label: 'Tools',
      children: [
        { label: 'Material',      href: '{root}tools/material-validator.html' },
        { label: 'Preset',        href: '{root}tools/preset-browser.html' },
        { label: 'Effect',        href: '{root}tools/shadow-wizard.html' },
        { label: 'Optimization',  href: '{root}tools/performance-budget.html' },
        { label: 'Migration',     href: '{root}tools/liltoon-migration.html' },
        { label: 'Shader',        href: '{root}tools/shader-variant-collector.html' },
        { label: '── All',        href: '{root}tools/index.html' }
      ]
    }
  ];

  /* ---------- Select language data ---------- */
  var CATEGORIES      = IS_EN ? CATEGORIES_EN      : CATEGORIES_JA;
  var TOOL_CATEGORIES = IS_EN ? TOOL_CATEGORIES_EN  : TOOL_CATEGORIES_JA;
  var PAGE_ORDER      = IS_EN ? PAGE_ORDER_EN       : PAGE_ORDER_JA;
  var TOOL_ORDER      = IS_EN ? TOOL_ORDER_EN       : TOOL_ORDER_JA;
  var NAV             = IS_EN ? NAV_EN              : NAV_JA;

  /* ---------- Localized strings ---------- */
  var L = IS_EN ? {
    home: 'Home', params: 'Parameters', tools: 'Tools',
    toc: 'Contents', menu: 'Menu', openToc: 'Open table of contents',
    pages: ' pages',
    canDo: 'What this feature does', canDoTool: 'What this tool does',
    theme: 'Toggle light / dark theme'
  } : {
    home: 'ホーム', params: 'パラメータ', tools: 'ツール',
    toc: '目次', menu: 'メニュー', openToc: '目次を開く',
    pages: ' ページ',
    canDo: 'この機能でできること', canDoTool: 'このツールでできること',
    theme: 'ライト / ダークテーマ切り替え'
  };

  /* ---------- Utilities ---------- */
  function getRoot() {
    var path = location.pathname;
    var depth = 0;

    /* add 1 for /en/ prefix */
    if (IS_EN) depth++;

    if (path.indexOf('/params/') !== -1 || path.indexOf('/tools/') !== -1) {
      var parts = path.split('/');
      var idx = -1;
      for (var i = 0; i < parts.length; i++) {
        if (parts[i] === 'params' || parts[i] === 'tools') { idx = i; break; }
      }
      if (idx !== -1) {
        depth += parts.length - 1 - idx;
      }
    }

    if (depth === 0) return '';
    var result = '';
    for (var i = 0; i < depth; i++) result += '../';
    return result;
  }

  var ROOT = getRoot();

  /* Build a path that includes /en/ prefix when on EN pages */
  function makePath(relPath) {
    return ROOT + (IS_EN ? 'en/' : '') + relPath;
  }

  function resolveHref(href) {
    if (IS_EN) {
      return href.replace('{root}', ROOT + 'en/');
    }
    return href.replace('{root}', ROOT);
  }

  /* current file path relative to Website root (without /en/) */
  function getCurrentRelPath() {
    var path = location.pathname;
    var parts = path.split('/');
    for (var i = 0; i < parts.length; i++) {
      if (parts[i] === 'params') return parts.slice(i).join('/');
      if (parts[i] === 'tools')  return parts.slice(i).join('/');
    }
    return parts[parts.length - 1] || 'index.html';
  }

  var CURRENT_REL = getCurrentRelPath();

  function isActive(href) {
    var resolved = resolveHref(href);
    var base = resolved.replace(/^(\.\.\/)+/, '').replace(/^en\//, '');
    return CURRENT_REL.indexOf(base) !== -1 || CURRENT_REL === base;
  }

  /* ---------- Detect page context ---------- */
  function getPageContext() {
    var parts = CURRENT_REL.split('/');
    if (parts[0] === 'params' && parts.length === 3) {
      return { section: 'params', category: parts[1], file: parts[2] };
    }
    if (parts[0] === 'tools' && parts.length === 2 && parts[1] !== 'index.html') {
      return { section: 'tools', category: null, file: parts[1] };
    }
    return null;
  }

  /* ---------- Language toggle URL ---------- */
  function getLangToggleHref() {
    if (IS_EN) {
      /* EN → JP: strip /en/ prefix, go to website root + relPath */
      return ROOT + CURRENT_REL;
    } else {
      /* JP → EN: go to website root + en/ + relPath */
      return ROOT + 'en/' + CURRENT_REL;
    }
  }

  /* ---------- Build header ---------- */
  function buildHeader() {
    var header = document.querySelector('.site-header');
    if (!header) return;

    var inner = document.createElement('div');
    inner.className = 'header-inner';

    var logo = document.createElement('div');
    logo.className = 'site-logo';
    logo.innerHTML = '<a href="' + makePath('index.html') + '">' +
      '<img src="' + ROOT + 'assets/natane-studio-icon.jpg" alt="">' +
      '<span>Natane Toon Shader</span></a>';

    var nav = document.createElement('nav');
    nav.className = 'nav-links';
    nav.id = 'site-navigation';

    function closeDropdowns(except) {
      nav.querySelectorAll('.nav-dropdown.open').forEach(function (dropdown) {
        if (dropdown === except) return;
        dropdown.classList.remove('open');
        var button = dropdown.querySelector('.nav-dropdown-trigger');
        if (button) button.setAttribute('aria-expanded', 'false');
      });
    }

    NAV.forEach(function (item, navIndex) {
      if (item.children) {
        var dd = document.createElement('div');
        dd.className = 'nav-dropdown';
        var trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'nav-dropdown-trigger';
        trigger.textContent = item.label + ' ▾';
        trigger.setAttribute('aria-expanded', 'false');
        trigger.setAttribute('aria-controls', 'nav-dropdown-' + navIndex);
        dd.appendChild(trigger);

        var menu = document.createElement('div');
        menu.className = 'nav-dropdown-menu';
        menu.id = 'nav-dropdown-' + navIndex;
        item.children.forEach(function (child) {
          var a = document.createElement('a');
          a.href = resolveHref(child.href);
          a.textContent = child.label;
          if (isActive(child.href)) a.classList.add('active');
          menu.appendChild(a);
        });
        dd.appendChild(menu);

        trigger.addEventListener('click', function () {
          var isOpen = !dd.classList.contains('open');
          closeDropdowns(dd);
          dd.classList.toggle('open', isOpen);
          trigger.setAttribute('aria-expanded', String(isOpen));
        });

        if (item.children.some(function (c) { return isActive(c.href); })) {
          trigger.classList.add('active');
        }
        nav.appendChild(dd);
      } else {
        var a = document.createElement('a');
        a.href = resolveHref(item.href);
        a.textContent = item.label;
        if (isActive(item.href)) a.classList.add('active');
        nav.appendChild(a);
      }
    });

    /* Language toggle */
    var langLink = document.createElement('a');
    langLink.className = 'lang-toggle';
    langLink.textContent = IS_EN ? '日本語' : 'English';
    langLink.href = getLangToggleHref();
    nav.appendChild(langLink);

    /* Theme toggle (dark <-> light, persisted) */
    var themeBtn = document.createElement('button');
    themeBtn.type = 'button';
    themeBtn.className = 'theme-toggle';
    themeBtn.setAttribute('aria-label', L.theme);
    themeBtn.setAttribute('title', L.theme);
    function currentTheme() {
      return document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
    }
    function paintThemeBtn() {
      themeBtn.textContent = currentTheme() === 'light' ? '☾' : '☀';
    }
    paintThemeBtn();
    themeBtn.addEventListener('click', function () {
      var next = currentTheme() === 'light' ? 'dark' : 'light';
      document.documentElement.setAttribute('data-theme', next);
      try { localStorage.setItem(THEME_KEY, next); } catch (e) {}
      paintThemeBtn();
    });
    nav.appendChild(themeBtn);

    /* Hamburger */
    var hamburger = document.createElement('button');
    hamburger.className = 'hamburger';
    hamburger.setAttribute('aria-label', L.menu);
    hamburger.setAttribute('aria-controls', nav.id);
    hamburger.setAttribute('aria-expanded', 'false');
    hamburger.innerHTML = '<span></span><span></span><span></span>';

    function closeMobileNav() {
      hamburger.classList.remove('open');
      nav.classList.remove('open');
      hamburger.setAttribute('aria-expanded', 'false');
      document.body.classList.remove('nav-open');
    }

    hamburger.addEventListener('click', function () {
      var isOpen = !nav.classList.contains('open');
      hamburger.classList.toggle('open', isOpen);
      nav.classList.toggle('open', isOpen);
      hamburger.setAttribute('aria-expanded', String(isOpen));
      document.body.classList.toggle('nav-open', isOpen);
    });

    nav.addEventListener('click', function (event) {
      if (event.target.closest('a')) closeMobileNav();
    });

    document.addEventListener('keydown', function (event) {
      if (event.key !== 'Escape') return;
      closeDropdowns();
      if (!nav.classList.contains('open')) return;
      closeMobileNav();
      hamburger.focus();
    });

    document.addEventListener('click', function (event) {
      if (!nav.contains(event.target)) closeDropdowns();
    });

    inner.appendChild(logo);
    inner.appendChild(nav);
    inner.appendChild(hamburger);
    header.appendChild(inner);
  }

  /* ---------- Build breadcrumb ---------- */
  function buildBreadcrumb() {
    var bc = document.querySelector('.breadcrumb');
    if (!bc) return;

    var ctx = getPageContext();
    if (!ctx) return;

    var crumbs = [{ label: L.home, href: makePath('index.html') }];

    if (ctx.section === 'params') {
      crumbs.push({ label: L.params, href: makePath('params/index.html') });
      if (ctx.category && CATEGORIES[ctx.category]) {
        crumbs.push({
          label: CATEGORIES[ctx.category].label,
          href: makePath('params/' + ctx.category + '/' + PAGE_ORDER[ctx.category][0].file)
        });
      }
      var pages = PAGE_ORDER[ctx.category] || [];
      for (var i = 0; i < pages.length; i++) {
        if (pages[i].file === ctx.file) {
          crumbs.push({ label: pages[i].label, href: null });
          break;
        }
      }
    } else if (ctx.section === 'tools') {
      crumbs.push({ label: L.tools, href: makePath('tools/index.html') });
      for (var j = 0; j < TOOL_ORDER.length; j++) {
        if (TOOL_ORDER[j].file === ctx.file) {
          crumbs.push({ label: TOOL_ORDER[j].label, href: null });
          break;
        }
      }
    }

    crumbs.forEach(function (c, idx) {
      if (idx > 0) {
        var sep = document.createElement('span');
        sep.className = 'breadcrumb-sep';
        sep.textContent = ' > ';
        bc.appendChild(sep);
      }
      if (c.href) {
        var a = document.createElement('a');
        a.href = c.href;
        a.textContent = c.label;
        bc.appendChild(a);
      } else {
        var span = document.createElement('span');
        span.className = 'breadcrumb-current';
        span.textContent = c.label;
        bc.appendChild(span);
      }
    });
  }

  /* ---------- Build category sidebar ---------- */
  function buildCategorySidebar() {
    var sidebar = document.querySelector('.sidebar');
    if (!sidebar) return;

    var ctx = getPageContext();
    if (!ctx) {
      buildTOC();
      return;
    }

    var pages = null;
    var catLabel = '';

    if (ctx.section === 'params' && ctx.category) {
      pages = PAGE_ORDER[ctx.category];
      catLabel = CATEGORIES[ctx.category] ? CATEGORIES[ctx.category].label : '';
    } else if (ctx.section === 'tools') {
      pages = TOOL_ORDER;
      catLabel = L.tools;
    }

    if (!pages) { buildTOC(); return; }

    var title = document.createElement('div');
    title.className = 'sidebar-title';
    title.textContent = catLabel;
    sidebar.appendChild(title);

    var ul = document.createElement('ul');
    ul.className = 'category-nav';

    pages.forEach(function (p) {
      var li = document.createElement('li');
      var a = document.createElement('a');
      if (ctx.section === 'params') {
        a.href = makePath('params/' + ctx.category + '/' + p.file);
      } else {
        a.href = makePath('tools/' + p.file);
      }
      a.textContent = p.label;
      if (p.file === ctx.file) a.classList.add('active');
      li.appendChild(a);
      ul.appendChild(li);
    });

    sidebar.appendChild(ul);
  }

  /* ---------- Build prev/next navigation ---------- */
  function buildPageNav() {
    var nav = document.querySelector('.page-nav');
    if (!nav) return;

    var ctx = getPageContext();
    if (!ctx) return;

    var pages, basePath;
    if (ctx.section === 'params' && ctx.category) {
      pages = PAGE_ORDER[ctx.category];
      basePath = makePath('params/' + ctx.category + '/');
    } else if (ctx.section === 'tools') {
      pages = TOOL_ORDER;
      basePath = makePath('tools/');
    } else {
      return;
    }

    var idx = -1;
    for (var i = 0; i < pages.length; i++) {
      if (pages[i].file === ctx.file) { idx = i; break; }
    }
    if (idx === -1) return;

    if (idx > 0) {
      var prev = document.createElement('a');
      prev.className = 'page-nav-prev';
      prev.href = basePath + pages[idx - 1].file;
      prev.innerHTML = '&larr; ' + pages[idx - 1].label;
      nav.appendChild(prev);
    }

    if (idx < pages.length - 1) {
      var next = document.createElement('a');
      next.className = 'page-nav-next';
      next.href = basePath + pages[idx + 1].file;
      next.innerHTML = pages[idx + 1].label + ' &rarr;';
      nav.appendChild(next);
    }
  }

  /* ---------- Build sidebar TOC (for portal/index pages) ---------- */
  function buildTOC() {
    var sidebar = document.querySelector('.sidebar');
    if (!sidebar) return;

    var content = document.querySelector('.main-content');
    if (!content) return;

    var headings = content.querySelectorAll('h2, h3');
    if (headings.length === 0) {
      sidebar.style.display = 'none';
      var wrapper = document.querySelector('.page-wrapper');
      if (wrapper) wrapper.classList.add('no-sidebar');
      var footer = document.querySelector('.site-footer');
      if (footer) footer.style.marginLeft = '0';
      return;
    }

    var title = document.createElement('div');
    title.className = 'sidebar-title';
    title.textContent = L.toc;
    sidebar.appendChild(title);

    var ul = document.createElement('ul');
    ul.className = 'toc-list';

    headings.forEach(function (h, i) {
      if (!h.id) h.id = 'section-' + i;
      var li = document.createElement('li');
      if (h.tagName === 'H3') li.classList.add('toc-h3');
      var a = document.createElement('a');
      a.href = '#' + h.id;
      a.textContent = h.textContent;
      li.appendChild(a);
      ul.appendChild(li);
    });
    sidebar.appendChild(ul);

    if ('IntersectionObserver' in window) {
      var links = ul.querySelectorAll('a');
      var observer = new IntersectionObserver(
        function (entries) {
          entries.forEach(function (entry) {
            if (entry.isIntersecting) {
              links.forEach(function (l) { l.classList.remove('active'); });
              var active = ul.querySelector('a[href="#' + entry.target.id + '"]');
              if (active) active.classList.add('active');
            }
          });
        },
        { rootMargin: '-80px 0px -60% 0px', threshold: 0 }
      );
      headings.forEach(function (h) { observer.observe(h); });
    }
  }

  /* ---------- Sidebar mobile toggle ---------- */
  function buildSidebarToggle() {
    var sidebar = document.querySelector('.sidebar');
    if (!sidebar) return;

    var btn = document.createElement('button');
    btn.className = 'sidebar-toggle';
    btn.setAttribute('aria-label', L.openToc);
    btn.textContent = '☰';
    btn.addEventListener('click', function () {
      sidebar.classList.toggle('open');
      btn.textContent = sidebar.classList.contains('open') ? '✕' : '☰';
    });
    document.body.appendChild(btn);
  }

  /* ---------- Build unified footer (GitHub / Booth / CHANGELOG) ---------- */
  function buildFooter() {
    var footer = document.querySelector('.site-footer');
    if (!footer) return;

    var inner = document.createElement('div');
    inner.className = 'footer-inner';

    var brand = document.createElement('div');
    brand.className = 'footer-brand';
    brand.textContent = '© 2026 Natane Studio · Natane Toon Shader';

    var links = document.createElement('nav');
    links.className = 'footer-links';
    links.setAttribute('aria-label', 'Site links');

    [
      { label: 'GitHub', href: SITE_LINKS.github },
      { label: 'Booth', href: SITE_LINKS.booth },
      { label: 'CHANGELOG', href: SITE_LINKS.changelog }
    ].forEach(function (l) {
      var a = document.createElement('a');
      a.href = l.href;
      a.textContent = l.label;
      a.target = '_blank';
      a.rel = 'noopener';
      links.appendChild(a);
    });

    footer.textContent = '';
    inner.appendChild(brand);
    inner.appendChild(links);
    footer.appendChild(inner);
  }

  /* ---------- Enhance doc page header with a "できること" summary box ---------- */
  function enhancePageHeader() {
    var ctx = getPageContext();
    if (!ctx) return;
    var main = document.querySelector('.main-content');
    if (!main) return;
    var subtitle = main.querySelector('.page-subtitle');
    if (!subtitle || subtitle.dataset.enhanced) return;

    var box = document.createElement('div');
    box.className = 'doc-summary';
    var label = document.createElement('span');
    label.className = 'doc-summary-label';
    label.textContent = ctx.section === 'tools' ? L.canDoTool : L.canDo;
    var text = document.createElement('p');
    text.className = 'doc-summary-text';
    text.textContent = subtitle.textContent;
    box.appendChild(label);
    box.appendChild(text);
    box.dataset.enhanced = '1';
    subtitle.replaceWith(box);
  }

  /* ---------- Expose data for portal pages ---------- */
  window.NataneNav = {
    PAGE_ORDER: PAGE_ORDER,
    CATEGORIES: CATEGORIES,
    TOOL_ORDER: TOOL_ORDER,
    TOOL_CATEGORIES: TOOL_CATEGORIES,
    ROOT: ROOT,
    IS_EN: IS_EN,
    makePath: makePath,
    L: L
  };

  /* ---------- Init ---------- */
  document.addEventListener('DOMContentLoaded', function () {
    buildHeader();
    buildBreadcrumb();
    enhancePageHeader();
    buildCategorySidebar();
    buildPageNav();
    buildSidebarToggle();
    buildFooter();
  });
})();
