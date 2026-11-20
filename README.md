# Jellyfin resources — plugin recovery

Repository for recovering two Jellyfin 12.4 plugins from the owner's server after losing access to an older GitHub account.

| Plugin | Installed version | Plugin GUID |
| --- | --- | --- |
| Request Movie | 0.1.0.111 | `f8517621-7c60-4e19-9696-67f9b31fa092` |
| NFO Trailer | 0.1.0.10 | `e30f17c1-cd72-4bdc-8225-38d122947719` |

## What is preserved

The recovery archive contains both original DLLs and plugin metadata. Embedded JS/HTML assets were also extracted. **Original C# source code was not included in the installed plugin directories**, so a buildable project still requires decompilation and reconstruction.

To avoid accidental exposure, the private recovery archive, binary DLLs, and Jellyfin configuration are **not uploaded automatically to this public repository**.

## Import the server archive

```bash
python3 scripts/import_recovery.py jellyfin-recovery.tar.gz --output recovered
```

This creates an offline copy in `recovered/` (ignored by Git). Review it before selecting files for publication.

## Rebuild plan

1. Preserve the original binaries and verify SHA-256 hashes.
2. Decompile both DLLs with ILSpy to recreate C# code.
3. Restore two separate .NET 10 plugin projects targeting Jellyfin 12.4.
4. Test API endpoints, frontend injection, NFO parsing, and plugin startup.
5. Publish independently versioned binary ZIPs and maintain the plugin catalog only after successful builds.

**Public repository:** do not commit Jellyfin access tokens, TMDb API keys, requests database, or other sensitive server data.
