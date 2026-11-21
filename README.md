# Jellyfin plugin catalog

Public release repository for **Request Movie** and **NFO Trailer** plugins for Jellyfin 12.4.

This repository contains only the public plugin catalog (`manifest.json`) and compiled DLL packages in [GitHub Releases](https://github.com/tests-tcj/jellyin-resources/releases). The editable source code, reference decompilation, and build workflows live in the private `tests-tcj/jellyfin-plugins` repository.

## Install catalog

In Jellyfin Dashboard → Plugins → Repositories, add:

```text
https://raw.githubusercontent.com/tests-tcj/jellyin-resources/main/manifest.json
```

Install plugins independently from the catalog. Restart Jellyfin after installing or upgrading.

## Important

The plugins are reconstructed from the owner's prior installations. Test on a non-production server before replacing working installations.

**Git history note:** the public repository previously contained source files. Removing files from the latest commit does not remove them from older commits or existing forks/caches. Historical visibility remains until history is separately rewritten and GitHub caches are handled.
