<div align="center">

# vini's M79

The M79 "Thumper" for SPT 4.1.6: a single-shot, break-action 40x46mm grenade launcher, plus two new 40x46mm rounds, the R.I.P. and the Hornet's Nest.

![Version](https://img.shields.io/badge/version-1.0.0-orange?style=flat)
![SPT](https://img.shields.io/badge/SPT-4.1.6-blue?style=flat)
![WTT-CommonLib](https://img.shields.io/badge/WTT--CommonLib-3.0.6-purple?style=flat)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green?style=flat)

[Features](#features) · [Install](#install) · [The Gun](#the-gun) · [Ammunition](#ammunition) · [Build](#build-from-source)

</div>

---

## Features

- **M79 40mm grenade launcher**: single shot, break action, with its own "thump" firing sound. It fires every 40x46mm grenade in the game.
- **Two new 40x46mm rounds**: R.I.P. and Hornet's Nest. They also fit the FN40GL, the M32A1 and the M203. See [Ammunition](#ammunition).
- Names and descriptions in English, Portuguese and Russian.

---

## Install

Install [WTT-CommonLib](https://github.com/WelcomeToThursday/WTT-CommonLib) **3.0.6** or newer (3.0.x) first. Then download `M79.zip` from [Releases](https://github.com/viniHNS/m79/releases) and extract it into your SPT game folder:

```
<game folder>/
├── BepInEx/plugins/
│   └── M79.dll             client plugin (Hornet's Nest sound)
└── SPT_Runtime/user/mods/M79/
    ├── M79.dll
    ├── bundles.json
    ├── bundles/            models, textures, animations and sounds
    └── db/                 items, preset, trader offers and locales
```

The client plugin only changes the Hornet's Nest shot sound. Without it, everything else works.

> The new items stay in your profile. Removing the mod after buying them breaks the profile.

---

## The Gun

| | |
|---|---|
| Caliber | 40x46mm |
| Action | Single shot, break action |
| Weight | 2.72 kg |
| Ergonomics | 52 |
| Recoil (vertical / back) | 260 / 520 |
| Sight | Folded leaf sight, zeroed at 25 m |
| Barrel | 356 mm, removable (no barrel, no shot) |
| Sold by | Peacekeeper LL2: $700 (limit 1) |

---

## Ammunition

Both rounds fit the M79, the FN40GL, the M32A1 and the M203. Both are sold by Mechanic.

| Round | Projectiles | Damage | Penetration | Armor damage | Speed | Recoil | Mechanic |
|---|---|---|---|---|---|---|---|
| 40x46mm R.I.P. | 1 | 450 | 3 | 15 | 76 m/s | +40% | LL2, ₽21,436 (10 per restock) |
| 40x46mm "Hornet's Nest" | 7 × 9x18mm PM PBM | 40 each | 28 | 30 | 415 m/s | +75% | LL2, barter for 10 9x18mm PM PBM + 3 Wilston cigarettes (5 per restock) |

- **R.I.P.**: one solid copper projectile with eight petals that split on impact. Heavy bleeding, almost useless against armor.
- **Hornet's Nest**: seven 9x18mm PBM rounds in one shell, fired at once. A short-range volley.

---

## Build from Source

**Requirements:** .NET 10 SDK.

```sh
dotnet build M79.sln -c Release
```

The build creates `M79.zip` in the project folder, with both DLLs.

> The build copies its output into `D:\Jogos\SPT4.1` for testing. Change `SptModsDir` in `Server/M79.csproj` and the `PostBuild` target in `Client/M79.csproj` to your own SPT folder. Close the game and the SPT server before building, or the copy fails because the DLLs are in use.

### Project Structure

```
m79/
├── M79.sln
├── Server/
│   ├── M79.csproj
│   ├── Mod.cs                          mod metadata and loading (WTT-CommonLib)
│   ├── bundles.json                    bundle manifest
│   ├── bundles/                        models, textures, animations and sounds
│   └── db/
│       ├── CustomItems/                M79, barrel, R.I.P. and Hornet's Nest (EN, PO, RU)
│       ├── CustomLocales/              preset name
│       └── weaponPresets/
│           ├── GlobalPresets/          default preset
│           └── Assorts/                Peacekeeper and Mechanic offers
└── Client/
    ├── M79.csproj
    ├── Plugin.cs                       BepInEx plugin
    └── Patches/NestSoundPatch.cs       Hornet's Nest sound on every 40x46mm launcher
```

---

## Credits

- Firing sound: from Rising Storm 2: Vietnam (Tripwire Interactive, Antimatter Games), extracted by RichardNixon.
- R.I.P. and Hornet's Nest: original models. The 9x18mm rounds inside the Hornet's Nest and its KS-23 blast are the game's.

---

## Resources

| Resource | URL |
|---|---|
| WTT-CommonLib | https://github.com/WelcomeToThursday/WTT-CommonLib |
| SPT Server C# | https://github.com/SP-Tushonka/server-csharp |
| Server Mod Examples | https://github.com/SP-Tushonka/server-mod-examples |
| SPT Wiki — Modding Resources | https://wiki.sp-tushonka.com/en/modding/Modding_Resources |
| SPT Scaffold | https://github.com/viniHNS/spt-scaffold |
