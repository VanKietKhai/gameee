# Mod #14 — Input conflict audit of the 13-mod staging pack (2026-10-05, pre-Dev Kit)

**Result: no hotkey chosen.** Some candidate keys are recorded below, but the vanilla bindings and Improved Thralls & QoL's default keys still need the Dev Kit (DEVKIT_CHECKLIST.md, Part C).

## Method (read-only; no mod file was changed)

- **Source:** the current client bundle `ConanClientModBundle-20261005-041519`, which is a copy. The live server's Mods folder was not used. All 13 paks were re-hashed: **13/13 equal `SHA256SUMS.txt`**, and the order follows the bundle's `modlist.txt`.
- **Scan 1, input asset names:** names in each pak containing `Input`, `Hotkey`, `KeyBind`, `Shortcut`, `IA_` or `IMC_`.
- **Scan 2, embedded descriptions:** the embedded modinfo description, searched for key-related text (hotkey, keybind, press, key, Ctrl/Alt/Shift, F-keys, toggle, input).

**Limits:**
- Asset *contents* are compressed. Default keys stored inside Blueprints or input assets are not readable without the Dev Kit.
- A mod can read raw keys in a Blueprint graph without any named input asset.
- Descriptions can be incomplete.

## Findings

| # | Mod | Input assets | Description mentions | Assessment |
|---|---|---|---|---|
| 1 | StackMe10K | none | none | No input expected |
| 2 | Savage Paragon | none | none | No input expected |
| 3 | Grit & Grease | none | none | No input expected |
| 4 | Thrall Reputation | none | none | No input expected |
| 5 | **Improved Thralls & QoL** | `E_HotkeyBindFunctions`, `Str_HotkeyBind`, `Str_Keybinds`, `WBP_ENHANCED_KeyBindings`, `WBP_ENHANCED_KeyBindButton`, `W_V_KeybindOverlay`, `W_V_InputModal` | Admin feature toggles | **Has its own rebindable hotkey system; default keys UNKNOWN.** The highest conflict risk. Also the best lead for Enhanced-menu integration. |
| 6 | WO Riding Thralls | none | none | No named input; Dev Kit check |
| 7 | Ancient Realms | none | none | No named input |
| 8 | Cannibal Captivity | none | none | No named input |
| 9 | Night Terrors | none | none | No input expected |
| 10 | PvE Plus Ambush | none | Tells admins to add `ConsoleKeys=Insert`. Refers to *ModControlPanel by hades* on **Shift+End**. | Console (`~` / Insert). ModControlPanel is **not** in the pack, but its Shift+End should be avoided. |
| 11 | Player DBNO | none | Console commands; console opened with `~` or **Insert** | Console only |
| 12 | **Simple Minimap** | `SM_IA_UseMouse`, `SM_IMC_UseMouseContext`, `SM_IC_InputComp` (Enhanced Input) | **Hold F1** for mouse mode. **Shift+click** on the map adds a custom marker. | F1 and Shift+click on the map are taken |
| 13 | Chest Labels | none | Toggle on the radial menu | No key |

Some descriptions (Player DBNO, Simple Minimap, Chest Labels) also advertise the same author's *Twin-Bar* and *Follower Remote* hotkeys. Those mods are **not** in the pack.

## Keys to avoid

| Key | Reason |
|---|---|
| F1 (hold) | Simple Minimap mouse mode |
| Shift + click on the map | Simple Minimap custom marker |
| `~`, Insert | The console (Player DBNO, PvE Plus Ambush admin setup) |
| Shift + End | ModControlPanel, a common admin companion mod (not installed) |
| *Every vanilla binding* | Not yet listed; Dev Kit Part C2 |
| *Improved Thralls & QoL defaults* | Unknown; Dev Kit Part C3 |

## Candidate bindings (NOT chosen)

Requirements:
- a single press toggles the tab (not hold-to-use);
- rebindable by the player;
- ignored while chat or another text field has focus.

| Candidate | Why | Still to check |
|---|---|---|
| **No default key: open from the Enhanced menu** | Zero conflicts if a supported menu hook exists (preferred path) | Dev Kit C1 |
| F7 | Function key away from F1; rarely bound by mods | Vanilla, Improved Thralls & QoL |
| F8 | Same | Vanilla, Improved Thralls & QoL |
| F6 | Same | Vanilla, Improved Thralls & QoL |
| Shift + a letter | Mnemonic option (to pick after C2/C3) | Vanilla, Improved Thralls & QoL, chat focus |

The final choice is made after Dev Kit steps C1–C3, then confirmed in a licensed client with all 13 mods loaded.
