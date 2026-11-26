# Words of power: presentation layer

Everything here is built from code at runtime. No prefabs, scenes or inspector wiring.

## Files

| File | What it is |
| --- | --- |
| `SpellContract.cs` | `SpokenWord`, `ISpellTarget`, `SpellOutcomeView` (unchanged) |
| `SpellPresenter.cs` | Public entry point: input, sequencing, world flights |
| `RealmSpeechBubble.cs` | The bubble view (screen space overlay canvas), letters, glyph row, UI motes |
| `GoldMagic.cs` | `ISpellTarget` for world objects: ignite flare and idle hint shimmer |
| `GlyphAtlas.cs` | Gardiner code to `Sprite`, plus frame, particle and UI sprites |
| `GlyphTable.cs` | Generated code to Unicode table with ink boxes (do not edit, rerun the tool) |
| `MagicFx.cs` | Pooled world particle emitters (`ParticleSystem.Emit`) and `FxQuad` |
| `MagicMaterials.cs` | Shader lookup, `RegisterShaders`, additive and alpha material setup |
| `MagicShaderRefs.cs` | Optional scene holder for shader and template material references |
| `MagicPalette.cs` | Colours, HDR helper and easing curves |

Art lives in `Art/Glyphs/Resources/RealmGlyphs` (131 signs, each `<code>.png` and `<code>_glow.png`,
plus `cartouche`, `fx_*` and `ui_*` textures and `glyph_table.json`) and
`Art/Glyphs/Resources/RealmShaders` (two small unlit shaders). Regenerate the art with
`python3 Art/Glyphs/_tools/make_glyphs.py` (Pillow, numpy, fonttools). It also rewrites `GlyphTable.cs`.
Sign art comes from Noto Sans Egyptian Hieroglyphs 2.002, SIL Open Font License 1.1.

## Use

```csharp
var presenter = SpellPresenter.Create(() => activeCamera, ankhuHeadTransform);
presenter.Submitted += text =>
{
    SpellOutcomeView outcome = Judge(text);   // your rules
    presenter.Resolve(outcome);               // may be called from inside the handler
};
SpellPresenter.OpenChanged += open => movement.enabled = !open;
SpellPresenter.GlyphLaunched += (code, pos) => sfx.Play("whoosh", pos);
SpellPresenter.GlyphArrived += (code, pos) => sfx.Play("chime", pos);

var magic = door.AddComponent<GoldMagic>();
magic.Configure("door", new Vector3(0f, 1.4f, 0f));   // local glyph anchor
magic.SetHint(true);                                   // gentle idle shimmer
```

Other API:

* `presenter.IsOpen`, `presenter.Busy`, `presenter.InputAllowed` (false: Enter does not open)
* `presenter.Cancel()` closes an open bubble, `presenter.History` exposes the `SpeechHistory`
* `presenter.Bubble.HeadOffset` (default 0.32 m above the head transform)
* Static events: `OpenChanged(bool)`, `GlyphLaunched(code, worldPos)`, `GlyphArrived(code, worldPos)`,
  `CharTyped(char)`, `OutcomeStarted(bool success)`, and `GoldMagic.Ignited(GoldMagic, strength)`
* `GlyphAtlas.Get(code)`, `GetGlow(code)`, `Has(code)`, `Cartouche`, `InkRect(code)`, `Aspect(code)`,
  `UnicodeOf(code)`. Codes are matched without regard to case or leading zeros.
* `GoldMagic` fields: `Intensity`, `Tint`, `UseEmission`, `UseRimShell`, `UseLight`, `UseShaft`

Input reuses `Ankhu.Speech.SpeechBuffer` and `SpeechHistory` and takes the keyboard through
`Ankhu.Core.InputGate`, like `SpeechBubbleController`. Do not run both controllers in one scene: both
open on Enter.

## Shaders

| Shader | Needed for | How it gets into a build |
| --- | --- | --- |
| `Ankhu/Realms/MagicAdditive` | glyph quads, all particles, ring, shaft | Automatic (under Resources, no keywords) |
| `Ankhu/Realms/GoldRim` | GoldMagic fresnel shell and upward light sweep | Automatic (under Resources, no keywords) |
| `Universal Render Pipeline/Particles/Unlit` | fallback only (and `MagicMaterials.AlphaBlended`, currently unused) | Add to Always Included Shaders, or reference it from a `MagicShaderRefs` |
| `Universal Render Pipeline/Lit` with `_EMISSION` | GoldMagic emissive wash on Lit materials | The variant must exist in the build: keep at least one Lit material with Emission enabled in a built scene |
| `Sprites/Default` | last resort fallback | Built in |
| `UI/Default` | the bubble | Built in |

`MagicMaterials.RegisterShaders(params Shader[])` registers shaders by name so nothing depends on
`Shader.Find`. `MagicMaterials.RegisterTemplates(additive, alpha)` takes editor made material assets
(configure them with `MagicMaterials.ConfigureAdditive` / `ConfigureAlphaBlend`), and runtime materials
are cloned from them, which keeps their exact URP variant in the build. A `MagicShaderRefs` component
in the scene does both on Awake.

URP additive setup used on URP materials: `_Surface=1`, `_Blend=2`, `_SrcBlend=SrcAlpha`,
`_DstBlend=One`, `_ZWrite=0`, `_Cull=Off`, keyword `_SURFACE_TYPE_TRANSPARENT` on,
`_ALPHAPREMULTIPLY_ON` and `_ALPHAMODULATE_ON` off, render queue 3000, RenderType Transparent.

Bloom: world colours are HDR (glyph cores about 2 to 3, glows 2.6 to 3.8, particles 3.2, GoldMagic
emission peak 2.4, rim 2.0 plus a sweep band of 3.2). `MagicPalette.Hdr` keeps intensities linear in a
Linear colour space project. The camera needs HDR on for values above 1 to bloom.

## Timings (unscaled time)

Success:

1. 0.00 s used words tint gold, unused words fade upward
2. 0.24 s first word starts dissolving, 0.045 s per letter, motes fly into its sign group
3. Signs assemble with ease out back, 0.085 s apart, 0.5 s each. Cartouche frame opens just before
4. Gold shimmer sweeps the row (0.75 s). Hold at least 0.6 s, and until `VoiceSeconds - 0.35` after Resolve
5. Anticipation dip 0.16 s, then signs launch 0.075 s apart as world quads
6. Flights: cubic Bezier arcs with per sign variance, 0.8 to 1.7 s by distance, trailing motes
7. Each arrival bursts (`GlyphArrived`). After the last: `Target.Ignite(1f)`, then `OnArrive`
8. `Busy` clears at least 0.35 s later and not before `VoiceSeconds` after Resolve

No target on success: the signs rise 1.3 to 2.1 m, spread sideways and fade over about 1.6 s, then `OnArrive`.

Failure: shake 0.5 s while turning sand grey, letters crumble from 0.46 s at 0.028 s per letter, the
reply fades in mid crumble, hold for 1.5 s plus 0.045 s per reply character (and at least `VoiceSeconds`).
Enter or Escape skips the rest of the hold once the voice time has passed. Then `OnArrive`.

GoldMagic ignite: flare and burst, light rises in 0.14 s and decays over about 2.5 s, rim and emission
follow the same envelope, sweep band climbs the object in 1.3 s, ground ring 1.1 s, shaft 1.9 s, four
orbiting signs 2.3 s, rising mote ring up to 2.4 s. Everything is restored about 3.2 s after the call.

## Safety

* `OnArrive` is called exactly once per Resolve and `Busy` always returns to false: on normal end, on a
  new Resolve during a sequence, on disable or destroy, and from a watchdog at `VoiceSeconds + 15 s`.
* A target destroyed mid flight is detected (Unity null check): flights finish at its last anchor,
  `Ignite` is skipped, `OnArrive` still runs. Exceptions from callbacks are logged, never rethrown.
* All world quads are destroyed on arrival or on abort. GoldMagic restores swapped materials (only slots
  still holding its own instances), restores or clears property blocks, destroys its instances, shells
  and effect objects when idle and on disable. Pooled particle systems live under one DontDestroyOnLoad
  root ("Ankhu Magic FX").
* Submitted but never resolved: the words fade after 4 s and the bubble closes.

## Untested (nobody pressed Play)

Compiled against Unity 6 reference assemblies in Editor and Player variants with zero errors and zero
warnings. Nothing has run. Most likely to need a tweak:

* The two `.shader` files were not compiled by Unity. They are plain `CGPROGRAM` unlit passes with no
  LightMode tag, which URP draws as SRPDefaultUnlit. If either shows pink, the fallback chain does not
  help for that shader, so check the console first.
* Visual tuning: sizes, HDR levels and bloom response, bubble proportions at extreme aspect ratios,
  glyph stacking rule (flat signs stack up to three high), cartouche padding.
* Texture import: PNGs are power of two and white RGB, so default import works. If the project
  imports them as Sprite with point filtering the code switches them to bilinear at runtime.
* `Font.GetCharacterInfo` advance measurement for per letter layout with the shipped Cormorant font.
* `RectMask2D.softness` on the border glint and sheen.
* URP point light intensity units (peak 7 times strength) against your moonlit exposure.
* Emission on URP Lit relies on the `_EMISSION` variant existing in the build (see Shaders).
* SkinnedMeshRenderer rim shells share the source bones. Untested on animated characters.
