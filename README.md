# Jellyfin plugins — recovered from installed server

**Public recovery repository:** source and recovered binaries for Jellyfin 12.4.

| Plugin | Original installed version | GUID | Reconstructed source |
|---|---|---|---|
| Request Movie | 0.1.0.111 | `f8517621-7c60-4e19-9696-67f9b31fa092` | `src/Jellyfin.Plugin.RequestMovie/` |
| NFO Trailer | 0.1.0.10 | `e30f17c1-cd72-4bdc-8225-38d122947719` | `src/Jellyfin.Plugin.Trailer/` |

## Recovery sources

- `binaries/`: the exact DLLs recovered from the Jellyfin server (original behavior/reference).
- `src/`: separate, compilable .NET 10 projects reconstructed from project history and recovered resources. **Do not assume the C# implementation is byte-for-byte identical to the original binaries.**
- `custom-tab.html`, `Client/*.js`, `Configuration/*.html`: original UI resources recovered from DLLs.
- `reference-decompiled/`: automatically generated ILSpy source reference, when decompilation workflow succeeds.
- `.github/workflows/build.yml`: builds both plugins independently and saves ZIP artifacts.
- `.github/workflows/decompile.yml`: extracts C# code from the original DLLs for deeper comparison.

## Build

Run the GitHub Actions workflow **Build recovered Jellyfin plugins**, or locally:

```bash
dotnet build src/Jellyfin.Plugin.RequestMovie/Jellyfin.Plugin.RequestMovie.csproj -c Release
dotnet build src/Jellyfin.Plugin.Trailer/Jellyfin.Plugin.Trailer.csproj -c Release
```

Outputs are `Jellyfin.Plugin.RequestMovie.dll` and `Jellyfin.Plugin.Trailer.dll`.

**Important:** reconstructed code has not yet been tested against the owner's active Jellyfin server. Do not replace the existing installed DLLs or delete the original data until functionality and persisted-request compatibility have been verified. The old configuration and stored requests are NOT included in the recovery archive.

## Original functionality targeted

Request Movie: title/IMDb/TMDb searches, duplicate checks, request lifecycle/status, admin actions, auto-reconciliation, optional TV placeholders, Jellyfin web navigation and embedded request form.

NFO Trailer: read YouTube IDs from movie NFO, show the modal trailer on a movie details page, support SPA re-navigation and TV remote Back where the client loads the injected UI. Native Samsung Tizen may require a separately modified WGT client.

## Security

This is a PUBLIC repository. Never commit TMDb API keys, Jellyfin authentication tokens, server configuration, or user-request databases.

The preserved DLLs are source recovery artifacts, **not** replacements for a correctly tested rebuilt plugin release.
