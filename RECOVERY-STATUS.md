# Recovery status

Verified from the Jellyfin server export:

- Request Movie **0.1.0.111**, GUID `f8517621-7c60-4e19-9696-67f9b31fa092`, 117248-byte DLL.
- NFO Trailer **0.1.0.10**, GUID `e30f17c1-cd72-4bdc-8225-38d122947719`, 26112-byte DLL.
- Both plugin `meta.json` records say `Active`, with target ABI `12.4.0.0`.

The full original C# sources, configuration data stored outside the plugin directories, and original build pipeline are not present in the server archive.

The recovered HTML and JS frontend files and binaries remain in the local recovery bundle. Use `scripts/import_recovery.py` to extract the DLLs locally. A successful decompilation and rebuild have not yet been verified.
