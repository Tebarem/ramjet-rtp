# Ramjet RTP

Random teleport for Ramjet servers: `/rtp` and `/wild` with destination profiles, square / circle / rectangle areas with a gap around
the centre, an even or gaussian distribution, a warm-up, a cooldown, a destination menu and a cache of safe spots so `/rtp` is
instant. Its feature set and config layout follow Jake's RTP; the code is written against the Ramjet API.

- `src/`: the plugin, a Ramjet source plugin (`namespace RandomTeleport`, plugin id `randomteleport`). Deploy it as
  `plugins/RandomTeleport/`; the server compiles it. `src/rtp.csproj` exists only for an out-of-server compile check.

Searches never block the tick: a random column's chunk is held with a chunk ticket (generated if needed), then the columns around it
are checked for a solid, harmless floor with two free blocks above. Top-down in the overworld and the end, middle-out in the nether
(below the roof).

## Commands

| Command | Node | What it does |
|---|---|---|
| `/rtp` | `randomteleport.use` | Opens the destination menu (with `randomteleport.usebyname` and `Ui.MenuOnRtp`), else teleports with the best profile for your world. |
| `/rtp <profile>` | `randomteleport.usebyname` | Teleport with a named profile. |
| `/wild` | `randomteleport.use` | Teleport with the best profile for your world, no menu. |
| `/forcertp <players> [profile <name> \| world <world>]` | `randomteleport.others` | Teleport other players (no warm-up, no cooldown). |
| `/rtp-admin reload` | `randomteleport.admin` | Read `config.json` again. |
| `/rtp-admin status` | `randomteleport.admin` | Profiles, cached spots, running searches. |

Other nodes: `randomteleport.use.<profile>` (profiles with `RequireExplicitPermission`), `randomteleport.nocooldown` (op),
`randomteleport.nowarmup`, `randomteleport.rtpondeath` (when `OnDeath.RequirePermission` is on).

## Configuration

`plugins/RandomTeleport/data/config.json` is written with the defaults on first start; `/rtp-admin reload` applies changes (an invalid
file keeps the old settings). Sections:

- `Profiles`: one entry per destination (`default` = overworld, `nether`). Landing world, worlds it may be used from (regexes),
  shape, centre (`spawn`, `player` or `custom`), radii and gaps, `Gaussian`, y bounds, search mode, attempts, cached spots, cooldown,
  warm-up, menu name / icon / description and the messages after the teleport (`%player% %world% %x% %y% %z%` and the `_old` variants).
- `FirstJoin`, `OnDeath`: teleport new players, or respawn dead players (beds and respawn anchors can win), with a profile.
- `Cache`: background searches that keep safe spots ready (profiles centred on the player are never cached).
- `Ui`: menu, titles, particles and sounds. `Logging`: which teleports are logged.

Players seen before are kept in `seen.json` (for the first-join teleport).

## Building

Set `RAMJET_HOME` to an engine build's `build/bin` (the folder with `dotnet/Mc.Scripting.Api.dll`):

```bash
dotnet build src/rtp.csproj
```

## License

MIT, see `LICENSE`.
