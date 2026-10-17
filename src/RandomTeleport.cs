using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

[assembly: McPlugin("randomteleport", version: "1.0.0", name: "RandomTeleport", main: "RandomTeleport.RtpPlugin", api: "1.0",
    description: "/rtp and /wild: random teleport with profiles, shapes, gaussian distribution, warm-up, cooldown and a location cache (a port of Jake's RTP)")]

namespace RandomTeleport;

// ---------------------------------------------------------------------------------------------------------------- settings
// plugins/randomteleport/config.json, written with these defaults on first start. /rtp-admin reload reads it again.

/// <summary>The whole configuration.</summary>
public sealed class RtpConfig
{
    /// <summary>Teleport new players to a random spot when they join for the first time.</summary>
    public FirstJoinSettings FirstJoin { get; set; } = new();
    /// <summary>Respawn dead players at a random spot.</summary>
    public DeathSettings OnDeath { get; set; } = new();
    /// <summary>Safe spots searched ahead of time, so /rtp is instant.</summary>
    public CacheSettings Cache { get; set; } = new();
    /// <summary>What gets logged.</summary>
    public LoggingSettings Logging { get; set; } = new();
    /// <summary>The menu, titles, particles and sounds.</summary>
    public UiSettings Ui { get; set; } = new();
    /// <summary>The rtp profiles by name. /rtp picks the highest-priority profile that may be used from the player's world; /rtp &lt;name&gt; picks one by name.</summary>
    public Dictionary<string, RtpProfile> Profiles { get; set; } = new()
    {
        ["default"] = new RtpProfile(),
        ["nether"] = new RtpProfile
        {
            DisplayName = "&c&lNether", Icon = "minecraft:netherrack", Description = ["&7Somewhere in the nether,", "&7safely below the roof"],
            LandingWorld = "minecraft:the_nether", Priority = 2, RadiusMin = 100, RadiusMax = 1000, BoundsLow = 32, BoundsHigh = 120, CacheLocations = 3,
        },
    };
}

/// <summary>The menu and the effects.</summary>
public sealed class UiSettings
{
    /// <summary>/rtp opens a menu to pick the destination (/wild and /rtp &lt;profile&gt; still teleport straight away).</summary>
    public bool MenuOnRtp { get; set; } = true;
    /// <summary>The menu title (&amp; colours).</summary>
    public string MenuTitle { get; set; } = "&8Random Teleport";
    /// <summary>Countdown, search and arrival titles.</summary>
    public bool Titles { get; set; } = true;
    /// <summary>Portal particles during the warm-up, where the player leaves and where he lands.</summary>
    public bool Particles { get; set; } = true;
    /// <summary>Sound ids (empty = silent).</summary>
    public SoundSettings Sounds { get; set; } = new();
}

/// <summary>The sounds.</summary>
public sealed class SoundSettings
{
    /// <summary>The menu opens.</summary>
    public string MenuOpen { get; set; } = "minecraft:block.ender_chest.open";
    /// <summary>A destination is picked.</summary>
    public string MenuClick { get; set; } = "minecraft:ui.button.click";
    /// <summary>Every second of the warm-up (the pitch rises).</summary>
    public string Countdown { get; set; } = "minecraft:block.note_block.pling";
    /// <summary>The moment of the teleport.</summary>
    public string Teleport { get; set; } = "minecraft:entity.enderman.teleport";
    /// <summary>After landing.</summary>
    public string Arrive { get; set; } = "minecraft:entity.player.levelup";
    /// <summary>The warm-up was cancelled or no spot was found.</summary>
    public string Cancel { get; set; } = "minecraft:entity.villager.no";
    /// <summary>Not allowed right now (cooldown, wrong world).</summary>
    public string Denied { get; set; } = "minecraft:block.note_block.bass";
}

/// <summary>rtp-on-first-join.</summary>
public sealed class FirstJoinSettings
{
    /// <summary>On or off.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>The profile used.</summary>
    public string Profile { get; set; } = "default";
}

/// <summary>rtp-on-death.</summary>
public sealed class DeathSettings
{
    /// <summary>On or off.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>The profile used.</summary>
    public string Profile { get; set; } = "default";
    /// <summary>Players with a bed or a charged respawn anchor respawn there instead.</summary>
    public bool RespectSpawnPoints { get; set; } = true;
    /// <summary>Only players with randomteleport.rtpondeath are teleported.</summary>
    public bool RequirePermission { get; set; } = true;
}

/// <summary>location-cache-filler.</summary>
public sealed class CacheSettings
{
    /// <summary>On or off (profiles centred on the player are never cached).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Ticks between two background searches (20 = one second).</summary>
    public int TicksBetweenSearches { get; set; } = 20;
    /// <summary>Seconds a chunk may take to load or generate before the spot is given up.</summary>
    public int ChunkLoadTimeoutSeconds { get; set; } = 15;
}

/// <summary>logging.</summary>
public sealed class LoggingSettings
{
    /// <summary>Log teleports of /rtp.</summary>
    public bool OnCommand { get; set; } = true;
    /// <summary>Log teleports of /forcertp.</summary>
    public bool OnForceCommand { get; set; } = true;
    /// <summary>Log first-join teleports.</summary>
    public bool OnFirstJoin { get; set; } = true;
    /// <summary>Log death respawns.</summary>
    public bool OnRespawn { get; set; } = true;
    /// <summary>Log every cached spot (noisy).</summary>
    public bool OnCache { get; set; } = false;
}

/// <summary>One rtp profile (rtpSettings + distribution of Jake's RTP).</summary>
public sealed class RtpProfile
{
    /// <summary>Loaded at all.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>The name in the menu and the titles (&amp; colours).</summary>
    public string DisplayName { get; set; } = "&a&lOverworld";
    /// <summary>The menu icon (an item id).</summary>
    public string Icon { get; set; } = "minecraft:grass_block";
    /// <summary>Lines under the name in the menu.</summary>
    public List<string> Description { get; set; } = ["&7Somewhere wild in the overworld"];
    /// <summary>Players may use it with /rtp (false: only first join, death and /forcertp).</summary>
    public bool CommandEnabled { get; set; } = true;
    /// <summary>Players also need randomteleport.use.&lt;name&gt;.</summary>
    public bool RequireExplicitPermission { get; set; } = false;
    /// <summary>Higher wins when several profiles can be used from a world.</summary>
    public int Priority { get; set; } = 1;
    /// <summary>The dimension players land in.</summary>
    public string LandingWorld { get; set; } = "minecraft:overworld";
    /// <summary>Regexes of the dimensions /rtp may be used from with this profile.</summary>
    public List<string> CallFromWorlds { get; set; } = [".*"];

    /// <summary>square, circle or rectangle.</summary>
    public string Shape { get; set; } = "square";
    /// <summary>spawn (the world spawn), player (where the player stands) or custom (CenterX / CenterZ).</summary>
    public string Center { get; set; } = "spawn";
    /// <summary>Custom centre x.</summary>
    public int CenterX { get; set; }
    /// <summary>Custom centre z.</summary>
    public int CenterZ { get; set; }
    /// <summary>square / circle: the largest distance from the centre.</summary>
    public int RadiusMax { get; set; } = 2000;
    /// <summary>square / circle: the gap around the centre nobody lands in.</summary>
    public int RadiusMin { get; set; } = 1000;
    /// <summary>rectangle: half the width (x).</summary>
    public int RectangleXRadius { get; set; } = 2000;
    /// <summary>rectangle: half the depth (z).</summary>
    public int RectangleZRadius { get; set; } = 1000;
    /// <summary>rectangle: half the width of the gap in the middle (0 = none).</summary>
    public int RectangleGapX { get; set; } = 0;
    /// <summary>rectangle: half the depth of the gap.</summary>
    public int RectangleGapZ { get; set; } = 0;
    /// <summary>Gaussian distribution between RadiusMin and RadiusMax instead of an even one.</summary>
    public GaussianSettings Gaussian { get; set; } = new();

    /// <summary>auto (middle-out in the nether, top-down elsewhere), top-down or middle-out.</summary>
    public string SearchMode { get; set; } = "auto";
    /// <summary>Lowest y a player may land on.</summary>
    public int BoundsLow { get; set; } = 32;
    /// <summary>Highest y a player may land on.</summary>
    public int BoundsHigh { get; set; } = 319;
    /// <summary>Columns this many blocks around the random spot are tried too (within its chunk).</summary>
    public int CheckRadius { get; set; } = 2;
    /// <summary>Random spots tried before giving up.</summary>
    public int MaxAttempts { get; set; } = 10;
    /// <summary>Safe spots kept ready (0 = search when asked).</summary>
    public int CacheLocations { get; set; } = 10;

    /// <summary>Seconds between two /rtp (randomteleport.nocooldown skips it).</summary>
    public int CooldownSeconds { get; set; } = 30;
    /// <summary>Warm-up before the teleport (randomteleport.nowarmup skips it).</summary>
    public WarmupSettings Warmup { get; set; } = new();
    /// <summary>Messages to the player after the teleport, with &amp; colours and %player% %world% %x% %y% %z% %x_old% %y_old% %z_old% %world_old%.</summary>
    public List<string> ThenMessage { get; set; } = ["&aYou have been teleported to &e%x% %y% %z%"];
}

/// <summary>gaussian-distribution.</summary>
public sealed class GaussianSettings
{
    /// <summary>On or off.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Larger values pack the spots closer around Center.</summary>
    public double Shrink { get; set; } = 4;
    /// <summary>0 = around RadiusMin, 1 = around RadiusMax.</summary>
    public double Center { get; set; } = 0.25;
}

/// <summary>warmup.</summary>
public sealed class WarmupSettings
{
    /// <summary>Seconds (0 = none).</summary>
    public int Seconds { get; set; } = 3;
    /// <summary>Moving cancels the teleport.</summary>
    public bool CancelOnMove { get; set; } = true;
    /// <summary>Show a countdown on the action bar.</summary>
    public bool CountDown { get; set; } = true;
}

/// <summary>Players seen before (for the first-join teleport).</summary>
public sealed class SeenPlayers
{
    /// <summary>Profile ids.</summary>
    public HashSet<string> Players { get; set; } = [];
}

[JsonSerializable(typeof(RtpConfig))]
[JsonSerializable(typeof(SeenPlayers))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class RtpJson : JsonSerializerContext;

// ---------------------------------------------------------------------------------------------------------------- plugin

/// <summary>Random teleport. A search picks a random column of the profile's area, loads its chunk with a chunk ticket (generating it if needed, without blocking the
/// tick), then looks for a safe floor with two free blocks above around it; a profile not centred on the player keeps a few safe spots cached so /rtp is instant.</summary>
internal sealed class RtpPlugin : Plugin
{
    private const string Prefix = "&8[&6RTP&8]&r ";

    private Config<RtpConfig> _config = null!;
    private SeenPlayers _seen = new();
    private readonly Dictionary<string, Queue<Location>> _cache = [];
    private readonly List<Search> _searches = [];
    private readonly Dictionary<Uuid, ulong> _lastUse = [];
    private readonly Dictionary<Uuid, Warmup> _warmups = [];
    private readonly HashSet<string> _registeredNodes = [];
    private readonly Random _rng = new();
    private ulong _nextCacheSearch;

    protected override void OnEnable()
    {
        Permissions.Register("randomteleport.use", PermissionDefault.True, "Use /rtp");
        Permissions.Register("randomteleport.usebyname", PermissionDefault.True, "Pick the destination (/rtp menu, /rtp <profile>)");
        Permissions.Register("randomteleport.rtpondeath", PermissionDefault.False, "Respawn at a random spot (when OnDeath is on)");
        Permissions.Register("randomteleport.nocooldown", PermissionDefault.Op, "No /rtp cooldown");
        Permissions.Register("randomteleport.nowarmup", PermissionDefault.False, "No /rtp warm-up");
        Permissions.Register("randomteleport.others", PermissionDefault.Op, "Use /forcertp on other players");
        Permissions.Register("randomteleport.admin", PermissionDefault.Op, "Use /rtp-admin");

        _config = Config.Load(RtpJson.Default.RtpConfig);
        ApplyConfig();
        if (Config.ReadText("seen.json") is { } json)
        {
            try { _seen = JsonSerializer.Deserialize(json, RtpJson.Default.SeenPlayers) ?? new(); }
            catch (JsonException e) { Log.Error("seen.json is invalid, starting empty", e); }
        }
        foreach (var p in Server.Players) _seen.Players.Add(p.Uuid.ToString());  // nobody online now is "new" after an install or a reload

        Commands.Register(Command.Literal("rtp").Permission("randomteleport.use")
            .Executes(ctx =>
            {
                var player = ctx.RequirePlayer();
                if (_config.Value.Ui.MenuOnRtp && player.HasPermission("randomteleport.usebyname")) { OpenMenu(player); return 1; }
                return RtpCommand(ctx, null);
            })
            .Then(Command.Argument("profile", Arg.Word()).Suggests(UsableProfileNames).Executes(ctx => RtpCommand(ctx, ctx.GetString("profile")))),
            description: "Teleport to a random place");
        Commands.Register(Command.Literal("wild").Permission("randomteleport.use").Executes(ctx => RtpCommand(ctx, null)),
            description: "Teleport to a random place in this world");

        Commands.Register(Command.Literal("forcertp").Permission("randomteleport.others")
            .Then(Command.Argument("players", Arg.Players())
                .Executes(ctx => ForceRtp(ctx, null, null))
                .Then(Command.Literal("profile").Then(Command.Argument("name", Arg.Word()).Suggests(_ => EnabledProfileNames())
                    .Executes(ctx => ForceRtp(ctx, ctx.GetString("name"), null))))
                .Then(Command.Literal("world").Then(Command.Argument("world", Arg.GreedyString()).Suggests(_ => Server.Worlds.Select(w => w.Key))
                    .Executes(ctx => ForceRtp(ctx, null, ctx.GetString("world")))))),
            description: "Teleport players to a random place");

        Commands.Register(Command.Literal("rtp-admin").Permission("randomteleport.admin")
            .Then(Command.Literal("reload").Executes(ctx =>
            {
                if (!_config.Reload()) throw new CommandFailure("config.json is invalid (see the log); the old settings stay");
                ApplyConfig();
                ctx.Reply(Component.Legacy(Prefix + "&aReloaded config.json"));
                return 1;
            }))
            .Then(Command.Literal("status").Executes(ctx =>
            {
                foreach (var (name, p) in Profiles())
                    ctx.Reply(Component.Legacy(Prefix + $"&e{name}&7: {p.Shape} in {p.LandingWorld}, cached {(_cache.TryGetValue(name, out var q) ? q.Count : 0)}/{(Cacheable(p) ? p.CacheLocations : 0)}"));
                ctx.Reply(Component.Legacy(Prefix + $"&7{_searches.Count} searches running, {ChunkTickets.ActiveCount} chunks held"));
                return 1;
            })));

        Events.On((ref PlayerJoinEvent e) =>
        {
            var player = e.Player;
            var c = _config.Value.FirstJoin;
            if (!c.Enabled || !_seen.Players.Add(player.Uuid.ToString())) return;
            Config.WriteText("seen.json", JsonSerializer.Serialize(_seen, RtpJson.Default.SeenPlayers));
            if (Profile(c.Profile) is not { } profile) return;
            Scheduler.RunLater(1, () =>
            {
                if (player.IsOnline) Teleport(player, c.Profile, profile, Trigger.FirstJoin, null);
            });
        });

        Events.On((ref PlayerRespawnEvent e) =>
        {
            var c = _config.Value.OnDeath;
            if (!c.Enabled || e.Reason != RespawnReason.Death) return;
            if (c.RespectSpawnPoints && e.IsSpawnPoint) return;
            if (c.RequirePermission && !e.Player.HasPermission("randomteleport.rtpondeath")) return;
            if (Profile(c.Profile) is not { } profile) return;
            if (Cacheable(profile) && TakeCached(c.Profile) is { } spot)
            {
                e.World = spot.World;
                e.Position = spot.Position;
                if (_config.Value.Logging.OnRespawn) Log.Info($"{e.Player.Name} respawns at {Describe(spot)} ({c.Profile})");
                return;
            }
            var player = e.Player;
            Scheduler.RunLater(1, () =>
            {
                if (player.IsOnline) Teleport(player, c.Profile, profile, Trigger.Respawn, null);
            });
        });

        Events.On((ref PlayerQuitEvent e) =>
        {
            _warmups.Remove(e.Uuid);
            var uuid = e.Uuid;
            foreach (var s in _searches) if (s.Player is { } p && p.Uuid == uuid) s.Cancelled = true;
        });

        Scheduler.RunEvery(1, Tick);
    }

    protected override void OnDisable()
    {
        foreach (var s in _searches) s.Ticket?.Release();
        _searches.Clear();
    }

    // ------------------------------------------------------------------------------------------------------------ profiles

    private IEnumerable<(string Name, RtpProfile Profile)> Profiles()
        => _config.Value.Profiles.Where(p => p.Value.Enabled).Select(p => (p.Key, p.Value));

    private RtpProfile? Profile(string name)
        => _config.Value.Profiles.TryGetValue(name, out var p) && p.Enabled ? p : null;

    private IEnumerable<string> EnabledProfileNames() => Profiles().Select(p => p.Name).Order();

    private IEnumerable<string> UsableProfileNames(CommandContext ctx)
        => ctx.Player is { } player && player.HasPermission("randomteleport.usebyname")
            ? Profiles().Where(p => p.Profile.CommandEnabled && MayUse(player, p.Name, p.Profile)).Select(p => p.Name).Order()
            : [];

    private static bool MayUse(Player player, string name, RtpProfile p)
        => !p.RequireExplicitPermission || player.HasPermission($"randomteleport.use.{name}");

    private static bool CallableFrom(RtpProfile p, World world)
        => p.CallFromWorlds.Any(pattern => Regex.IsMatch(world.Key, $"^(?:{pattern})$"));

    private static bool Cacheable(RtpProfile p) => !p.Center.Equals("player", StringComparison.OrdinalIgnoreCase) && p.CacheLocations > 0;

    private void ApplyConfig()
    {
        foreach (var (name, _) in Profiles())
        {
            string node = $"randomteleport.use.{name}";
            if (_registeredNodes.Add(node)) Permissions.Register(node, PermissionDefault.Op, $"Use the rtp profile {name}");
        }
        foreach (var (name, p) in Profiles())
            if (Server.GetWorld(p.LandingWorld) is null) Log.Warn($"profile {name}: there is no world {p.LandingWorld}");
        foreach (var name in _cache.Keys.ToList())
            if (Profile(name) is not { } p || !Cacheable(p)) _cache.Remove(name);
            else _cache[name].Clear();  // the area may have changed
    }

    // ------------------------------------------------------------------------------------------------------------ commands

    private enum Trigger { Command, Force, FirstJoin, Respawn }

    private int RtpCommand(CommandContext ctx, string? requested)
    {
        var player = ctx.RequirePlayer();
        string name;
        RtpProfile profile;
        if (requested is not null)
        {
            name = requested;
            if (!player.HasPermission("randomteleport.usebyname")) throw new CommandFailure("You may not choose an rtp profile");
            profile = Profile(name) is { CommandEnabled: true } p && MayUse(player, name, p) ? p : throw new CommandFailure($"There is no rtp profile '{name}' you can use");
            if (!CallableFrom(profile, player.World)) throw new CommandFailure($"The rtp profile '{name}' can't be used from this world");
        }
        else
        {
            var here = player.World.Key;
            var best = Profiles().Where(p => p.Profile.CommandEnabled && MayUse(player, p.Name, p.Profile) && CallableFrom(p.Profile, player.World))
                .OrderByDescending(p => p.Profile.LandingWorld == here).ThenByDescending(p => p.Profile.Priority).FirstOrDefault();
            if (best.Profile is null) throw new CommandFailure("You can't use /rtp in this world");
            (name, profile) = best;
        }

        if (Begin(player, name, profile) is { } error)
        {
            Play(player, _config.Value.Ui.Sounds.Denied);
            throw new CommandFailure(error);
        }
        return 1;
    }

    /// <summary>The checks of a player's own rtp (busy, cooldown), then the warm-up or the teleport. The reason when it can't start.</summary>
    private string? Begin(Player player, string name, RtpProfile profile)
    {
        if (_warmups.ContainsKey(player.Uuid) || _searches.Any(s => s.Player is { } sp && sp.Uuid == player.Uuid && !s.Cancelled))
            return "You are already being teleported";
        if (CooldownLeft(player, profile) is > 0 and var wait) return $"You can use /rtp again in {wait} seconds";
        if (profile.Warmup.Seconds > 0 && !player.HasPermission("randomteleport.nowarmup"))
        {
            var w = new Warmup(player, name, profile, player.Position, Server.CurrentTick + (ulong)profile.Warmup.Seconds * 20);
            _warmups[player.Uuid] = w;
            player.SendMessage(Component.Legacy(Prefix + $"&7Teleporting to {profile.DisplayName}&7 in &e{profile.Warmup.Seconds}&7 seconds{(profile.Warmup.CancelOnMove ? ", don't move" : "")}..."));
            WarmupSecond(w, profile.Warmup.Seconds);
            return null;
        }
        Teleport(player, name, profile, Trigger.Command, null);
        return null;
    }

    /// <summary>Seconds until the player may /rtp again (0 = now).</summary>
    private long CooldownLeft(Player player, RtpProfile profile)
    {
        if (player.HasPermission("randomteleport.nocooldown") || !_lastUse.TryGetValue(player.Uuid, out ulong last)) return 0;
        long wait = profile.CooldownSeconds * 20L - (long)(Server.CurrentTick - last);
        return wait > 0 ? (wait + 19) / 20 : 0;
    }

    // ------------------------------------------------------------------------------------------------------------ menu

    /// <summary>One icon per destination the player may pick, with its range, warm-up, cooldown and whether it can be used now.</summary>
    private void OpenMenu(Player player)
    {
        var ui = _config.Value.Ui;
        var choices = Profiles().Where(p => p.Profile.CommandEnabled && MayUse(player, p.Name, p.Profile))
            .OrderByDescending(p => p.Profile.Priority).ThenBy(p => p.Name).Take(7).ToList();
        if (choices.Count == 0)
        {
            Play(player, ui.Sounds.Denied);
            player.SendMessage(Component.Legacy(Prefix + "&cThere is no destination you can use"));
            return;
        }
        var menu = Menus.Chest(3, Component.Legacy(ui.MenuTitle));
        menu.FillEmpty(new ItemStack("minecraft:gray_stained_glass_pane").WithName(Component.Text(" ")));
        int[] slots = choices.Count switch
        {
            1 => [13],
            2 => [11, 15],
            3 => [11, 13, 15],
            4 => [10, 12, 14, 16],
            5 => [9, 11, 13, 15, 17],
            _ => [.. Enumerable.Range(10, choices.Count)],
        };
        for (int i = 0; i < choices.Count; i++)
        {
            var (name, profile) = choices[i];
            menu.Slot(slots[i], Icon(player, profile), click =>
            {
                click.Close();
                if (Unavailable(player, profile) is { } why)
                {
                    Play(player, ui.Sounds.Denied);
                    player.SendActionBar(Component.Legacy("&c" + why));
                    return;
                }
                Play(player, ui.Sounds.MenuClick);
                if (Begin(player, name, profile) is { } error)
                {
                    Play(player, ui.Sounds.Denied);
                    player.SendMessage(Component.Legacy(Prefix + "&c" + error));
                }
            });
        }
        menu.Slot(22, new ItemStack("minecraft:barrier").WithItemName(Component.Legacy("&cClose")), click => click.Close());
        menu.OnClose(_ => Scheduler.RunLater(1, () => menu.Dispose()));
        if (menu.Open(player)) Play(player, ui.Sounds.MenuOpen, 1.2f, 0.6f);
        else menu.Dispose();
    }

    private string? Unavailable(Player player, RtpProfile profile)
    {
        if (!CallableFrom(profile, player.World)) return "Not available from this world";
        if (Server.GetWorld(profile.LandingWorld) is null) return "This world does not exist";
        if (CooldownLeft(player, profile) is > 0 and var wait) return $"Cooldown: {wait}s";
        return null;
    }

    private ItemStack Icon(Player player, RtpProfile profile)
    {
        static Component Line(string text) => Component.Legacy(text).Italic(false);
        var lore = new List<Component>();
        foreach (var d in profile.Description) lore.Add(Line(d));
        lore.Add(Component.Empty);
        string range = profile.Shape.Equals("rectangle", StringComparison.OrdinalIgnoreCase)
            ? $"&f±{profile.RectangleXRadius} × ±{profile.RectangleZRadius}&7 blocks"
            : $"&f{Math.Min(profile.RadiusMin, profile.RadiusMax)}–{Math.Max(profile.RadiusMin, profile.RadiusMax)}&7 blocks";
        string from = profile.Center.ToLowerInvariant() switch { "player" => "from you", "custom" => $"from {profile.CenterX}, {profile.CenterZ}", _ => "from spawn" };
        lore.Add(Line($"&8» &7Distance: {range} {from}"));
        bool noWarmup = profile.Warmup.Seconds <= 0 || player.HasPermission("randomteleport.nowarmup");
        lore.Add(Line($"&8» &7Warm-up: &f{(noWarmup ? "none" : profile.Warmup.Seconds + "s")}"));
        lore.Add(Line($"&8» &7Cooldown: &f{(player.HasPermission("randomteleport.nocooldown") ? "none" : profile.CooldownSeconds + "s")}"));
        lore.Add(Component.Empty);
        string? why = Unavailable(player, profile);
        lore.Add(Line(why is null ? "&a▶ Click to teleport" : "&c✖ " + why));
        var icon = SafeItem(profile.Icon).WithItemName(Component.Legacy(profile.DisplayName)).WithLore([.. lore]);
        return why is null ? icon.WithComponent("minecraft:enchantment_glint_override", new NbtByte(1)) : icon;
    }

    private ItemStack SafeItem(string id)
    {
        try { return new ItemStack(id); }
        catch (ArgumentException)
        {
            Log.Warn($"unknown menu icon {id}, using a compass");
            return new ItemStack("minecraft:compass");
        }
    }

    // ------------------------------------------------------------------------------------------------------------ effects

    private static void Play(Player player, string sound, float pitch = 1f, float volume = 1f)
    {
        if (sound.Length > 0) player.PlaySound(sound, SoundCategory.Players, volume, pitch);
    }

    private void Burst(World world, Vec3d at, string particle, int count)
    {
        if (_config.Value.Ui.Particles) world.SpawnParticle(particle, new Vec3d(at.X, at.Y + 1, at.Z), count, new Vec3d(0.4, 0.8, 0.4), 0.6f);
    }

    /// <summary>One second of the countdown: title, a pling that rises with every second.</summary>
    private void WarmupSecond(Warmup w, long secondsLeft)
    {
        var ui = _config.Value.Ui;
        if (w.Profile.Warmup.CountDown)
        {
            if (ui.Titles)
                w.Player.ShowTitle(Component.Legacy($"&6&l{secondsLeft}"),
                    Component.Legacy($"&7Teleporting to {w.Profile.DisplayName}{(w.Profile.Warmup.CancelOnMove ? "&7 • don't move" : "")}"), new TitleTimes(0, 25, 5));
            else w.Player.SendActionBar(Component.Legacy($"&7Teleporting in &e{secondsLeft}&7..."));
        }
        int total = Math.Max(1, w.Profile.Warmup.Seconds);
        Play(w.Player, ui.Sounds.Countdown, 0.7f + 1.0f * (total - secondsLeft) / total);
    }

    /// <summary>A ring of portal particles that climbs around the player during the warm-up.</summary>
    private void WarmupParticles(Warmup w, ulong now)
    {
        if (!_config.Value.Ui.Particles) return;
        var pos = w.Player.Position;
        double a = now * 0.35;
        for (int i = 0; i < 3; i++)
        {
            double t = a + i * Math.PI * 2 / 3;
            w.Player.World.SpawnParticle("minecraft:portal", new Vec3d(pos.X + Math.Cos(t) * 0.9, pos.Y + 0.2 + (now % 20) * 0.09, pos.Z + Math.Sin(t) * 0.9), 2, default, 0.1f);
        }
    }

    private int ForceRtp(CommandContext ctx, string? profileName, string? world)
    {
        var players = ctx.GetPlayers("players");
        if (players.Count == 0) throw new CommandFailure("No player was found");
        string name;
        RtpProfile profile;
        if (profileName is not null)
        {
            profile = Profile(profileName) ?? throw new CommandFailure($"There is no rtp profile '{profileName}'");
            name = profileName;
        }
        else if (world is not null)
        {
            var best = Profiles().Where(p => p.Profile.LandingWorld == world).OrderByDescending(p => p.Profile.Priority).FirstOrDefault();
            if (best.Profile is null) throw new CommandFailure($"No rtp profile lands in {world}");
            (name, profile) = best;
        }
        else
        {
            var w = players[0].World;
            var best = Profiles().Where(p => CallableFrom(p.Profile, w)).OrderByDescending(p => p.Profile.Priority).FirstOrDefault();
            if (best.Profile is null) best = Profiles().OrderByDescending(p => p.Profile.Priority).FirstOrDefault();
            if (best.Profile is null) throw new CommandFailure("There is no enabled rtp profile");
            (name, profile) = best;
        }
        foreach (var p in players) Teleport(p, name, profile, Trigger.Force, ctx.Source.Player);
        ctx.Reply(Component.Legacy(Prefix + $"&aTeleporting {(players.Count == 1 ? players[0].Name : $"{players.Count} players")} with profile &e{name}"));
        return players.Count;
    }

    // ------------------------------------------------------------------------------------------------------------ teleporting

    /// <summary>Teleports now when a cached spot is ready, else starts a search that teleports when it finds one.</summary>
    private void Teleport(Player player, string name, RtpProfile profile, Trigger trigger, Player? by)
    {
        if (Server.GetWorld(profile.LandingWorld) is not { } world)
        {
            player.SendMessage(Component.Legacy(Prefix + $"&cThe world {profile.LandingWorld} does not exist"));
            return;
        }
        if (Cacheable(profile) && TakeCached(name) is { } spot)
        {
            Arrive(player, name, profile, spot, trigger, by);
            return;
        }
        var center = CenterOf(profile, world, player);
        if (_config.Value.Ui.Titles) player.ShowTitle(Component.Legacy("&6&lSearching"), Component.Legacy($"&7for a safe spot in {profile.DisplayName}"), new TitleTimes(0, 200, 10));
        else player.SendActionBar(Component.Legacy("&7Looking for a safe spot..."));
        _searches.Add(new Search(name, profile, world, center.X, center.Z, player, trigger, by));
    }

    private void Arrive(Player player, string name, RtpProfile profile, Location spot, Trigger trigger, Player? by)
    {
        if (!player.IsOnline) return;
        var old = player.Location;
        if (!player.Teleport(spot))
        {
            player.SendMessage(Component.Legacy(Prefix + "&cThe teleport was cancelled"));
            return;
        }
        if (trigger == Trigger.Command) _lastUse[player.Uuid] = Server.CurrentTick;
        var ui = _config.Value.Ui;
        Burst(old.World, old.Position, "minecraft:portal", 60);
        Burst(spot.World, spot.Position, "minecraft:reverse_portal", 50);
        Play(player, ui.Sounds.Teleport);
        if (ui.Sounds.Arrive.Length > 0) Scheduler.RunLater(4, () => { if (player.IsOnline) Play(player, ui.Sounds.Arrive, 1.4f, 0.5f); });
        if (ui.Titles)
            player.ShowTitle(Component.Legacy("&a&lTeleported!"), Component.Legacy($"{profile.DisplayName} &8• &7{Math.Floor(spot.X)}, {Math.Floor(spot.Y)}, {Math.Floor(spot.Z)}"),
                new TitleTimes(5, 50, 15));
        foreach (var line in profile.ThenMessage) player.SendMessage(Component.Legacy(Fill(line, player, spot, old)));
        bool log = trigger switch
        {
            Trigger.Command => _config.Value.Logging.OnCommand,
            Trigger.Force => _config.Value.Logging.OnForceCommand,
            Trigger.FirstJoin => _config.Value.Logging.OnFirstJoin,
            _ => _config.Value.Logging.OnRespawn,
        };
        if (log) Log.Info($"{player.Name} rtp'd to {Describe(spot)} ({name}, {trigger}{(by is { } b ? $" by {b.Name}" : "")})");
    }

    private static string Fill(string line, Player player, Location at, Location old) => line
        .Replace("%player%", player.Name, StringComparison.OrdinalIgnoreCase)
        .Replace("%world_old%", old.World.Key, StringComparison.OrdinalIgnoreCase)
        .Replace("%x_old%", ((int)Math.Floor(old.X)).ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("%y_old%", ((int)Math.Floor(old.Y)).ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("%z_old%", ((int)Math.Floor(old.Z)).ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("%world%", at.World.Key, StringComparison.OrdinalIgnoreCase)
        .Replace("%x%", ((int)Math.Floor(at.X)).ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("%y%", ((int)Math.Floor(at.Y)).ToString(), StringComparison.OrdinalIgnoreCase)
        .Replace("%z%", ((int)Math.Floor(at.Z)).ToString(), StringComparison.OrdinalIgnoreCase);

    private static string Describe(Location l) => $"{l.World.Key} {Math.Floor(l.X)} {Math.Floor(l.Y)} {Math.Floor(l.Z)}";

    private Location? TakeCached(string name)
    {
        if (!_cache.TryGetValue(name, out var q)) return null;
        while (q.Count > 0)
        {
            var spot = q.Dequeue();
            // a cached spot was safe when it was found; someone may have built or dug there since (only checkable while its chunk is loaded)
            var feet = new BlockPos((int)Math.Floor(spot.X), (int)Math.Floor(spot.Y), (int)Math.Floor(spot.Z));
            if (!spot.World.IsBlockLoaded(feet) || IsSafeSpot(spot.World, feet)) return spot;
        }
        return null;
    }

    private static Vec3d CenterOf(RtpProfile p, World world, Player? player)
    {
        if (p.Center.Equals("player", StringComparison.OrdinalIgnoreCase) && player is { } pl && pl.World == world) return pl.Position;
        if (p.Center.Equals("custom", StringComparison.OrdinalIgnoreCase)) return new Vec3d(p.CenterX, 0, p.CenterZ);
        var spawn = World.Overworld.SpawnLocation;  // the nether and the end have no spawn of their own: centre on the origin there
        return world == World.Overworld ? spawn.Position : new Vec3d(0, 0, 0);
    }

    // ------------------------------------------------------------------------------------------------------------ the tick

    private sealed class Warmup(Player player, string name, RtpProfile profile, Vec3d start, ulong at)
    {
        public readonly Player Player = player;
        public readonly string Name = name;
        public readonly RtpProfile Profile = profile;
        public readonly Vec3d Start = start;
        public readonly ulong At = at;
    }

    private sealed class Search(string name, RtpProfile profile, World world, double cx, double cz, Player? player, Trigger trigger, Player? by)
    {
        public readonly string Name = name;
        public readonly RtpProfile Profile = profile;
        public readonly World World = world;
        public readonly double CenterX = cx, CenterZ = cz;
        public readonly Player? Player = player;  // null: fills the cache
        public readonly Trigger Trigger = trigger;
        public readonly Player? By = by;
        public int Attempts;
        public int X, Z;
        public ChunkTicket? Ticket;
        public ulong Deadline;
        public bool Cancelled;
    }

    private void Tick()
    {
        ulong now = Server.CurrentTick;

        // warm-ups
        if (_warmups.Count > 0)
            foreach (var (uuid, w) in _warmups.ToList())
            {
                if (!w.Player.IsOnline) { _warmups.Remove(uuid); continue; }
                var pos = w.Player.Position;
                double dx = pos.X - w.Start.X, dy = pos.Y - w.Start.Y, dz = pos.Z - w.Start.Z;
                if (w.Profile.Warmup.CancelOnMove && dx * dx + dy * dy + dz * dz > 0.25)
                {
                    _warmups.Remove(uuid);
                    if (_config.Value.Ui.Titles) w.Player.ShowTitle(Component.Legacy("&c&lCancelled"), Component.Legacy("&7You moved"), new TitleTimes(0, 30, 10));
                    else w.Player.SendActionBar(Component.Legacy("&cTeleport cancelled: you moved"));
                    Play(w.Player, _config.Value.Ui.Sounds.Cancel);
                    continue;
                }
                if (now >= w.At)
                {
                    _warmups.Remove(uuid);
                    Teleport(w.Player, w.Name, w.Profile, Trigger.Command, null);
                    continue;
                }
                if ((w.At - now) % 20 == 0) WarmupSecond(w, (long)(w.At - now) / 20);
                if (now % 2 == 0) WarmupParticles(w, now);
            }

        // searches: player searches first, at most two chunk loads for the cache at once
        int timeout = Math.Max(1, _config.Value.Cache.ChunkLoadTimeoutSeconds) * 20;
        for (int i = 0; i < _searches.Count; i++)
        {
            var s = _searches[i];
            if (s.Cancelled || Step(s, now, timeout))
            {
                s.Ticket?.Release();
                _searches.RemoveAt(i--);
            }
        }

        // cache filler
        var cache = _config.Value.Cache;
        if (!cache.Enabled || now < _nextCacheSearch || _searches.Count(s => s.Player is null) >= 2) return;
        foreach (var (name, p) in Profiles())
        {
            if (!Cacheable(p) || Server.GetWorld(p.LandingWorld) is not { } world) continue;
            if (!_cache.TryGetValue(name, out var q)) _cache[name] = q = new Queue<Location>();
            int pending = _searches.Count(s => s.Player is null && s.Name == name);
            if (q.Count + pending >= p.CacheLocations) continue;
            var c = CenterOf(p, world, null);
            _searches.Add(new Search(name, p, world, c.X, c.Z, null, Trigger.Command, null));
            _nextCacheSearch = now + (ulong)Math.Max(1, cache.TicksBetweenSearches);
            break;
        }
    }

    /// <summary>Advances one search. True when it is finished (found, or out of attempts).</summary>
    private bool Step(Search s, ulong now, int timeout)
    {
        if (s.Player is { } waiting && !waiting.IsOnline) return true;
        if (s.Ticket is null)
        {
            if (s.Attempts >= Math.Max(1, s.Profile.MaxAttempts))
            {
                if (s.Player is { } unlucky)
                {
                    unlucky.SendMessage(Component.Legacy(Prefix + "&cNo safe spot was found, try again"));
                    if (_config.Value.Ui.Titles) unlucky.ShowTitle(Component.Legacy("&c&lNo luck"), Component.Legacy("&7No safe spot was found, try again"), new TitleTimes(0, 40, 10));
                    Play(unlucky, _config.Value.Ui.Sounds.Cancel);
                }
                if (s.Player is null) _nextCacheSearch = now + 200;  // a profile whose area has no safe spot must not keep the workers busy
                return true;
            }
            s.Attempts++;
            var (dx, dz) = Sample(s.Profile);
            s.X = (int)Math.Floor(s.CenterX + dx);
            s.Z = (int)Math.Floor(s.CenterZ + dz);
            try { s.Ticket = ChunkTickets.Add(s.World, s.X >> 4, s.Z >> 4); }
            catch (Exception e) when (e is ArgumentOutOfRangeException or InvalidOperationException) { return false; }  // outside the border: next attempt
            s.Deadline = now + (ulong)timeout;
            return false;
        }
        if (!s.Ticket.IsLoaded)
        {
            if (now < s.Deadline) return false;
            s.Ticket.Release();
            s.Ticket = null;
            return false;
        }

        var found = FindSafe(s.World, s.Profile, s.X, s.Z);
        s.Ticket.Release();
        s.Ticket = null;
        if (found is not { } feet) return false;

        var spot = new Location(s.World, feet.X + 0.5, feet.Y, feet.Z + 0.5, (float)(_rng.NextDouble() * 360 - 180), 0f);
        if (s.Player is { } player) Arrive(player, s.Name, s.Profile, spot, s.Trigger, s.By);
        else
        {
            if (!_cache.TryGetValue(s.Name, out var q)) _cache[s.Name] = q = new Queue<Location>();
            q.Enqueue(spot);
            if (_config.Value.Logging.OnCache) Log.Info($"cached {Describe(spot)} for {s.Name} ({q.Count}/{s.Profile.CacheLocations})");
        }
        return true;
    }

    // ------------------------------------------------------------------------------------------------------------ distribution

    /// <summary>A random offset from the centre (Jake's RTP distributions: even or gaussian, square / circle with a gap, rectangle with a gap).</summary>
    private (double, double) Sample(RtpProfile p)
    {
        string shape = p.Shape.ToLowerInvariant();
        if (shape == "rectangle")
        {
            int rx = Math.Max(1, p.RectangleXRadius), rz = Math.Max(1, p.RectangleZRadius);
            for (int i = 0; i < 1000; i++)
            {
                double x = (_rng.NextDouble() * 2 - 1) * rx, z = (_rng.NextDouble() * 2 - 1) * rz;
                if (Math.Abs(x) >= p.RectangleGapX || Math.Abs(z) >= p.RectangleGapZ) return (x, z);
            }
            return (rx, rz);
        }
        double min = Math.Max(0, Math.Min(p.RadiusMin, p.RadiusMax)), max = Math.Max(1, Math.Max(p.RadiusMin, p.RadiusMax));
        double angle = _rng.NextDouble() * Math.PI * 2;
        if (shape == "circle")
        {
            double r = p.Gaussian.Enabled
                ? min + (max - min) * GaussianT(p.Gaussian)
                : Math.Sqrt(min * min + _rng.NextDouble() * (max * max - min * min));  // even over the ring's area
            return (r * Math.Cos(angle), r * Math.Sin(angle));
        }
        // square
        if (p.Gaussian.Enabled)
        {
            double r = min + (max - min) * GaussianT(p.Gaussian);
            double t = _rng.NextDouble() * 8 - 4;  // a point on the square's outline at "radius" r
            int side = (int)Math.Floor(t + 4) & 3;
            double along = (t - Math.Floor(t)) * 2 - 1;
            return side switch { 0 => (r, along * r), 1 => (-r, along * r), 2 => (along * r, r), _ => (along * r, -r) };
        }
        for (int i = 0; i < 1000; i++)
        {
            double x = (_rng.NextDouble() * 2 - 1) * max, z = (_rng.NextDouble() * 2 - 1) * max;
            if (Math.Max(Math.Abs(x), Math.Abs(z)) >= min) return (x, z);
        }
        return (max, max);
    }

    /// <summary>0..1, normally distributed around Center with a spread of 1 / Shrink (redrawn when it falls outside).</summary>
    private double GaussianT(GaussianSettings g)
    {
        double shrink = g.Shrink <= 0 ? 1 : g.Shrink;
        for (int i = 0; i < 100; i++)
        {
            double u1 = 1 - _rng.NextDouble(), u2 = _rng.NextDouble();
            double n = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            double t = g.Center + n / shrink;
            if (t is >= 0 and <= 1) return t;
        }
        return Math.Clamp(g.Center, 0, 1);
    }

    // ------------------------------------------------------------------------------------------------------------ safety

    /// <summary>The feet position of a safe spot near (x, z) inside its chunk, or null.</summary>
    private static BlockPos? FindSafe(World world, RtpProfile p, int x, int z)
    {
        string mode = p.SearchMode.ToLowerInvariant();
        bool middleOut = mode == "middle-out" || (mode == "auto" && world == World.Nether);
        int low = Math.Max(p.BoundsLow, world.MinY), high = Math.Min(p.BoundsHigh, world.MaxY - 2);
        int r = Math.Clamp(p.CheckRadius, 0, 7);
        int x0 = x & ~15, z0 = z & ~15;
        // the random column first, then the others of the square around it, nearest first
        var columns = new List<(int, int)>();
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                int cx = Math.Clamp(x + dx, x0, x0 + 15), cz = Math.Clamp(z + dz, z0, z0 + 15);
                if (!columns.Contains((cx, cz))) columns.Add((cx, cz));
            }
        columns.Sort((a, b) => (Math.Abs(a.Item1 - x) + Math.Abs(a.Item2 - z)).CompareTo(Math.Abs(b.Item1 - x) + Math.Abs(b.Item2 - z)));
        foreach (var (cx, cz) in columns)
        {
            if (middleOut)
            {
                int mid = (low + high) / 2;
                for (int d = 0; d <= high - low; d++)
                {
                    int y = (d & 1) == 0 ? mid + d / 2 : mid - (d + 1) / 2;
                    if (y < low || y > high) continue;
                    var feet = new BlockPos(cx, y + 1, cz);
                    if (IsSafeSpot(world, feet)) return feet;
                }
            }
            else
            {
                int top = world.HighestBlockY(cx, cz, HeightMap.MotionBlockingNoLeaves);
                if (top < low || top > high) continue;
                var feet = new BlockPos(cx, top + 1, cz);
                if (IsSafeSpot(world, feet)) return feet;
            }
        }
        return null;
    }

    /// <summary>A solid, harmless floor below the feet and room for the player (two free blocks).</summary>
    private static bool IsSafeSpot(World world, BlockPos feet)
    {
        var floor = world.GetBlock(new BlockPos(feet.X, feet.Y - 1, feet.Z));
        return IsSafeFloor(floor) && IsFree(world.GetBlock(feet)) && IsFree(world.GetBlock(new BlockPos(feet.X, feet.Y + 1, feet.Z)));
    }

    private static bool IsFree(BlockState s)
    {
        if (s.IsAir) return true;
        string key = s.BlockKey;
        return Passable.Contains(key) || key.EndsWith("_carpet", StringComparison.Ordinal);
    }

    private static bool IsSafeFloor(BlockState s)
    {
        if (s.IsAir || IsFree(s)) return false;
        string key = s.BlockKey;
        if (Dangerous.Contains(key) || key.EndsWith("_leaves", StringComparison.Ordinal)) return false;
        return !s.Key.Contains("waterlogged=true", StringComparison.Ordinal);
    }

    private static readonly HashSet<string> Passable =
    [
        "minecraft:cave_air", "minecraft:void_air", "minecraft:short_grass", "minecraft:tall_grass", "minecraft:fern", "minecraft:large_fern",
        "minecraft:dead_bush", "minecraft:bush", "minecraft:short_dry_grass", "minecraft:tall_dry_grass", "minecraft:firefly_bush", "minecraft:snow",
        "minecraft:dandelion", "minecraft:poppy", "minecraft:blue_orchid", "minecraft:allium", "minecraft:azure_bluet", "minecraft:red_tulip",
        "minecraft:orange_tulip", "minecraft:white_tulip", "minecraft:pink_tulip", "minecraft:oxeye_daisy", "minecraft:cornflower",
        "minecraft:lily_of_the_valley", "minecraft:torchflower", "minecraft:sunflower", "minecraft:lilac", "minecraft:rose_bush", "minecraft:peony",
        "minecraft:pink_petals", "minecraft:wildflowers", "minecraft:leaf_litter", "minecraft:glow_lichen", "minecraft:vine", "minecraft:moss_carpet",
        "minecraft:pale_moss_carpet", "minecraft:brown_mushroom", "minecraft:red_mushroom", "minecraft:crimson_roots", "minecraft:warped_roots",
        "minecraft:nether_sprouts", "minecraft:crimson_fungus", "minecraft:warped_fungus", "minecraft:closed_eyeblossom", "minecraft:open_eyeblossom",
    ];

    private static readonly HashSet<string> Dangerous =
    [
        "minecraft:water", "minecraft:lava", "minecraft:bubble_column", "minecraft:seagrass", "minecraft:tall_seagrass", "minecraft:kelp",
        "minecraft:kelp_plant", "minecraft:magma_block", "minecraft:cactus", "minecraft:fire", "minecraft:soul_fire", "minecraft:campfire",
        "minecraft:soul_campfire", "minecraft:sweet_berry_bush", "minecraft:powder_snow", "minecraft:pointed_dripstone", "minecraft:wither_rose",
        "minecraft:cobweb", "minecraft:nether_portal", "minecraft:end_portal", "minecraft:end_gateway", "minecraft:bedrock", "minecraft:scaffolding",
    ];
}
