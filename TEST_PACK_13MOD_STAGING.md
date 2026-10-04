# 13-MOD STAGING GAMEPLAY TEST PACK

**STAGING / TEST ONLY — not production.** Frozen 2026-10-05 from the validated final 13-mod state (`final13-1` + `final13-restart`, server-side PASS). Server world backup at freeze: `2026-10-05_014347`. Workshop ID is the `mainClient` id embedded in each `.pak`, recomputed from the file itself.

Client bundle: built by the production `ClientModBundleService` (`export-bundle`), every pak re-hashed equal to the installed server pak, bundle `modlist.txt` equal to the server `modlist.txt`: **CLIENT MOD PACK == SERVER MOD PACK**.

| Load order | Display name | Pak file | Size (bytes) | SHA-256 | Workshop ID | Source |
|---|---|---|---|---|---|---|
| 1 | StackMe10K | `StackMe10K.pak` | 4,641,754 | `30F5DF542826145FC4B1619296DD135A1370A13CFFBF66DF83E317F52885C8A0` | 3735091187 | Nexus (Conan Exiles Enhanced mod 3); embedded Workshop id |
| 2 | Savage Paragon | `SavageParagon.pak` | 4,760,799 | `5F2673D99DE7D76BA6D5C512C8E53F95AB83863ABF7C926769CC1BDD7C6312B5` | 3766043945 | Workshop |
| 3 | Grit & Grease (Weapon Infusions) | `GritandGrease.pak` | 68,049,336 | `B5FA39CCB338848C5B680EB7E64BE68204A55B99B0048DF1FEB4348D2E74ACCA` | 3801774752 | Workshop |
| 4 | Thrall Reputation | `ThrallReputation.pak` | 833,760 | `5CE7A95D31400518DF31F72349FBB4D181D2D6D0771D970159B4DBF150DE10B9` | 3787066846 | Workshop |
| 5 | Improved Thralls & QoL | `ImprovedThrallsAndQoL.pak` | 154,632,086 | `F35D9D927B4E76869D57DC7073B61FCF2215039B628343B28D6B0989A0B48272` | 3758661389 | Workshop |
| 6 | [Enhanced] WO - Riding Thralls | `WO_RidingThralls.pak` | 78,896,983 | `ACF569D38D7CB73E7523A096200AB1948E72C2783CB70953F43ADC2EFE7B34A8` | 3803149679 | Workshop |
| 7 | Ancient Realms Enhanced (Work In Progress) | `Ancient_Realms.pak` | 496,900,005 | `12F7E7192043270FC5F2290C5989F8B8285494BA47A054646790B4A042D1FD1A` | 3755775098 | Workshop |
| 8 | Cannibal Captivity v0.0.16 (Enhanced) | `Cannibal_Captivity.pak` | 112,432,880 | `DB6E3C299912E48E4DEC8293A58C8DEF1D881FDBDC44F1E67CE99A9BF348F04F` | 3765743138 | Workshop |
| 9 | Night Terrors | `NightTerrors.pak` | 10,219,405 | `2FE3E7AD7160ABA04DD9EAB99225BBD45D937E4B69525271F119348A7BCFBE61` | 3723538551 | Workshop |
| 10 | PvE Plus Ambush (Enhanced) - v1.0.5 | `PvEPlusAmbush.pak` | 5,145,777 | `C9C816FAE72C07626D4F0AD1994347CBDE110FBF6CB295AADD804540E4EC5B74` | 3721274811 | Workshop |
| 11 | Player DBNO System v1.1.1 (by Xevyr) | `PlayerDBNO.pak` | 2,518,846 | `3E7FEEEDA8093E20211F776BC79472DC78AE338344723D55A91667BDE4F9FD65` | 3718882569 | Workshop |
| 12 | Simple Minimap | `Simple_Minimap.pak` | 4,835,375 | `04F31A75559665C1A949D7A9A632F9A777CE7D79E926032CFC9FD101B1626AC9` | 3719513784 | Workshop |
| 13 | Chest Labels | `ChestLabels.pak` | 1,192,363 | `C79C7E00E8B44F7A6F1250D58BF8655BA9FFBDA16FDB7A1884BBD782186D0CD8` | 3735258746 | Workshop |

Exact `modlist.txt` (server and bundle):

```
StackMe10K.pak
SavageParagon.pak
GritandGrease.pak
ThrallReputation.pak
ImprovedThrallsAndQoL.pak
WO_RidingThralls.pak
Ancient_Realms.pak
Cannibal_Captivity.pak
NightTerrors.pak
PvEPlusAmbush.pak
PlayerDBNO.pak
Simple_Minimap.pak
ChestLabels.pak
```

Rules for this test pack: no mods added or removed, no balance changes, no production world, Custom Main Questline paused. Bundle contents: the 13 `.pak` files, `modlist.txt`, `manifest.json`, `SHA256SUMS.txt`, this manifest, and READMEs only (no executables, DLLs, authentication files, server binaries, backups or secrets).

## Build compatibility

- 2.2.2 / CL-377096: validated 2026-10-05 (final13-1, final13-restart).
- **2.2.3 / CL-378132 (release-beta): validated 2026-10-05 with the same 13 pak bytes** (v223-smoke-1, v223-final13-1, v223-final13-restart). The current client bundle is the 2.2.3 export; the 2.2.2 export is superseded.

