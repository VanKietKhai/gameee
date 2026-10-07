# Mod text translation (client-only, pilot)

The Vietnamese patch is a client pak `ConanSandbox\Content\Paks\~mod\pakchunk0-Windows_P_999.pak`
that overrides the 18 `en` .locres files. Mod widgets use FText keys in namespace "" and are looked
up in those files, so a mod text is translated by adding (namespace, key, CRC of the English source,
Vietnamese text) to `Exiles_UI.locres` and repacking the patch pak. Mods, server and world are not touched.

Scripts (pilot; paths point at the session scratch folder and must be adjusted):
- `modscan.py` - extract each client mod (UnrealPak), decompress its IoStore container with the
  Dev Kit's oo2core.dll, count FText entries.
- `uehash.py` - UE text hashes: source hash = CRC32 of UTF-32LE text; key hash = CityHash64 of
  UTF-16LE folded to 32 bits (empty string = 0). Verified on all 15,531 keys of the patch.
- `locres_rw.py` - locres v3 reader/writer (byte-identical round trip on Exiles_UI.locres).
- `extract_tr.py <ModName>` - writes `tr_<ModName>.json` (ns, key, English source, already-translated flag).
- `apply_tr.py tr_A.json tr_B.json ...` - starts from the ORIGINAL patch Exiles_UI.locres and adds every
  entry that has a `vi` field, so the output is reproducible.
- `translations/` - the translation tables (installed: PlayerDBNO 3, Simple_Minimap 86, ModControlPanel 20, Cannibal_Captivity 2,
  ThrallReputation 10, Ancient_Realms 9, WO_RidingThralls 11, StackMe10K 32, NightTerrors 35, PvEPlusAmbush 34,
  GritandGrease 118, IdeaPoet_EditAppearance 128, ImprovedThrallsAndQoL 1604).
- `itq_merge.py` - maps the ITQoL unique-text translations (itq_vi_*.json, scratch) back onto every entry. Apply all tables in one apply_tr.py call.

Build: response file with `"<file>" "../../../ConanSandbox/Content/<path>" -compress`, then
`UnrealPak.exe <out>.pak -create=<resp> -compressionformats=Zlib`. Original patch backed up in
`E:\CSC-M3-Live\client-backups\vihoa-original-20261007`.

Limits: texts that a mod builds from plain strings or its own language table (for example Extended
Thrall Stats labels) cannot be translated this way. A Vietnamese patch update replaces the pak, so
the merge must be redone.
