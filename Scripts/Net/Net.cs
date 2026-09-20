using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Net;

// Networking. One rule: the server runs the simulation - every NPC brain,
// animal, ledger, the clock, the weather - and clients are windows onto it.
// A client owns its own player's movement and sends everything else as a
// request the server carries out. Solo play is a host with nobody else
// connected, on the same code path (Godot's offline peer runs the RPCs
// locally).
public partial class Net : Node
{
    public static Net I { get; private set; }

    public enum Mode { Offline, Host, Client, Dedicated }
    public Mode Current { get; private set; } = Mode.Offline;

    public const int DefaultPort = 7777;
    public string LocalName = "Thomas Ross";
    public string JoinAddress = "127.0.0.1";

    // True where the simulation runs.
    public static bool IsServer => I == null || I.Current != Mode.Client;
    public static bool IsDedicated => I != null && I.Current == Mode.Dedicated;

    public int LocalPeerId => Multiplayer.MultiplayerPeer == null ? 1 : Multiplayer.GetUniqueId();
    public PlayerController LocalPlayer { get; private set; }

    // peer id -> that peer's player, on every machine.
    public readonly Dictionary<int, PlayerController> Players = new();
    private readonly Dictionary<int, string> _names = new();

    // Agents by net id, on every machine.
    public readonly Dictionary<int, Agent> Agents = new();
    private int _nextNetId = 1;

    private float _snapshotTimer, _stateTimer, _slowTimer;
    private Node2D _spawnParent;
    private PackedScene _playerScene, _npcScene, _animalScene;

    public override void _Ready()
    {
        I = this;
        _playerScene = GD.Load<PackedScene>("res://Scenes/Player/Player.tscn");
        _npcScene = GD.Load<PackedScene>("res://Scenes/Npc/Npc.tscn");
        _animalScene = GD.Load<PackedScene>("res://Scenes/Animals/Animal.tscn");
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += () => { RpcId(1, nameof(Hello), LocalName); Sim.Notice("Connected to the host."); };
        Multiplayer.ConnectionFailed += () => Sim.Notice("Could not reach the host.");
        Multiplayer.ServerDisconnected += () => Sim.Notice("The host has gone. You are on your own now.");
    }

    // ---- session setup --------------------------------------------------

    public Error StartHost(int port = DefaultPort, bool dedicated = false)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(port, 8);
        if (err != Error.Ok) return err;
        Multiplayer.MultiplayerPeer = peer;
        Current = dedicated ? Mode.Dedicated : Mode.Host;
        _names[1] = LocalName;
        return Error.Ok;
    }

    public Error StartClient(string address, int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(address, port);
        if (err != Error.Ok) return err;
        Multiplayer.MultiplayerPeer = peer;
        Current = Mode.Client;
        return Error.Ok;
    }

    public void StartSolo()
    {
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        Current = Mode.Offline;
        _names[1] = LocalName;
    }

    // Called by the world scene once its systems are up.
    public void WorldReady(Node2D spawnParent)
    {
        _spawnParent = spawnParent;
        if (IsServer && Current != Mode.Dedicated) SpawnPlayer(1, _names.GetValueOrDefault(1, LocalName));
    }

    // Back to the menu: drop the connection (or the hosted game) and forget the world.
    public void Leave()
    {
        try { Multiplayer.MultiplayerPeer?.Close(); } catch { }
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        Current = Mode.Offline;
        _names.Clear();
        ResetWorld();
    }

    // The world scene is (re)loading: forget every agent.
    public void ResetWorld()
    {
        Agents.Clear(); Players.Clear(); _nextNetId = 1; LocalPlayer = null; _spawnParent = null;
    }

    // The host is loading a save: every client drops its world and comes
    // back in as if it had just connected.
    public void AnnounceReload()
    {
        if (IsServer && Current != Mode.Offline) Rpc(nameof(ReloadWorld));
    }

    private bool _rehello;
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReloadWorld()
    {
        _rehello = true;
        Sim.Notice("The host is loading a saved world.");
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/World/YorkFactoryExterior.tscn");
    }

    // A client's world is up again after the host reloaded: ask to be re-seated.
    public void ClientWorldReady()
    {
        if (IsServer || !_rehello) return;
        _rehello = false;
        GetTree().CreateTimer(1.5).Timeout += () => RpcId(1, nameof(Hello), LocalName);
    }

    public int PlayerCount => Players.Count;

    // ---- peers ------------------------------------------------------------

    private void OnPeerConnected(long id)
    {
        if (!IsServer) return;
        // Wait for Hello (their name); then spawn.
    }

    private void OnPeerDisconnected(long id)
    {
        if (!IsServer) return;
        if (Players.TryGetValue((int)id, out var p))
        {
            Sim.Notice($"{p.DisplayName} has left.");
            Rpc(nameof(DespawnAgent), p.NetId);
            DespawnAgent(p.NetId);
        }
        _names.Remove((int)id);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Hello(string name)
    {
        if (!IsServer) return;
        int peer = Multiplayer.GetRemoteSenderId();
        if (_spawnParent == null) { GetTree().CreateTimer(1.0).Timeout += () => { if (_spawnParent != null && !Players.ContainsKey(peer)) Hello_Seat(peer, name); }; return; }
        Hello_Seat(peer, name);
    }

    private void Hello_Seat(int peer, string name)
    {
        _names[peer] = name;
        // Bring the newcomer up to date: everything that exists, then their player.
        RpcId(peer, nameof(WorldSync), GameCalendar.Instance.Year, GameCalendar.Instance.Month, GameCalendar.Instance.Day, GameCalendar.Instance.Hour, (int)GameCalendar.Instance.Weather.Current);
        foreach (var a in Agents.Values) RpcId(peer, nameof(SpawnAgent), Describe(a));
        foreach (var kv in Players) RpcId(peer, nameof(BindPlayer), kv.Key, kv.Value.NetId, kv.Value.DisplayName);
        Sim.Structures?.SendCellStateTo(peer);
        SpawnPlayer(peer, name);
        Sim.Notice($"{name} has joined.");
    }

    // ---- agents -----------------------------------------------------------

    // Every agent gets a net id when the server creates it.
    public int Register(Agent a)
    {
        if (!IsServer) return a.NetId;
        if (a.NetId == 0) a.NetId = _nextNetId++;
        Agents[a.NetId] = a;
        return a.NetId;
    }

    public void Unregister(Agent a)
    {
        if (a.NetId != 0) Agents.Remove(a.NetId);
    }

    // Tell every client about a new agent (server side).
    public void Announce(Agent a)
    {
        if (!IsServer || Current == Mode.Offline) return;
        Register(a);
        Rpc(nameof(SpawnAgent), Describe(a));
    }

    public void AnnounceDespawn(Agent a)
    {
        if (!IsServer || Current == Mode.Offline) return;
        Rpc(nameof(DespawnAgent), a.NetId);
    }

    private static Godot.Collections.Dictionary Describe(Agent a)
    {
        var d = new Godot.Collections.Dictionary
        {
            ["id"] = a.NetId, ["name"] = a.DisplayName, ["x"] = a.GlobalPosition.X, ["y"] = a.GlobalPosition.Y,
            ["faction"] = (int)a.Faction, ["dead"] = a.IsDead,
        };
        switch (a)
        {
            case PlayerController p: d["kind"] = "player"; break;
            case Animal an: d["kind"] = "animal"; d["species"] = (int)an.Species; break;
            case Npc n: d["kind"] = "npc"; d["frames"] = n.FramesPath; d["tint"] = n.Tint; d["scale"] = n.SpriteScale; d["role"] = (int)n.Role; break;
        }
        return d;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SpawnAgent(Godot.Collections.Dictionary d)
    {
        if (_spawnParent == null) return;
        int id = (int)d["id"];
        if (Agents.ContainsKey(id)) return;
        string kind = (string)d["kind"];
        Agent a;
        switch (kind)
        {
            case "player":
                var p = _playerScene.Instantiate<PlayerController>();
                p.IsPuppet = true;
                a = p;
                break;
            case "animal":
                var an = _animalScene.Instantiate<Animal>();
                an.Species = (Ecology.Species)(int)d["species"];
                an.IsPuppet = true;
                a = an;
                break;
            default:
                var n = _npcScene.Instantiate<Npc>();
                n.IsPuppet = true;
                n.Role = (Role)(int)d["role"];
                n.FramesPath = (string)d["frames"]; n.Tint = (Color)d["tint"]; n.SpriteScale = (float)d["scale"];
                a = n;
                break;
        }
        a.NetId = id;
        a.DisplayName = (string)d["name"];
        a.Faction = (Faction)(int)d["faction"];
        a.Position = new Vector2((float)d["x"], (float)d["y"]);
        a.Name = $"{kind}_{id}";
        _spawnParent.AddChild(a);
        if (a is Npc npc) npc.ApplyAppearance();
        if ((bool)d["dead"]) a.PuppetDie();
        Agents[id] = a;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DespawnAgent(int id)
    {
        if (Agents.TryGetValue(id, out var a))
        {
            Agents.Remove(id);
            foreach (var kv in new List<KeyValuePair<int, PlayerController>>(Players)) if (kv.Value == a) Players.Remove(kv.Key);
            if (IsInstanceValid(a)) a.QueueFree();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AgentDied(int id)
    {
        if (Agents.TryGetValue(id, out var a)) a.PuppetDie();
    }

    public void AnnounceDeath(Agent a)
    {
        if (IsServer && Current != Mode.Offline) Rpc(nameof(AgentDied), a.NetId);
    }

    // ---- players ----------------------------------------------------------

    private void SpawnPlayer(int peer, string name)
    {
        var p = _playerScene.Instantiate<PlayerController>();
        p.DisplayName = name;
        p.OwnerPeer = peer;
        p.IsPuppet = peer != LocalPeerId;      // on the host, other peers' players are puppets for rendering; needs still tick server-side
        p.Position = IsoGrid.ToWorld(WorldLayout.GateOutside) + new Vector2(GD.RandRange(-60, 60), GD.RandRange(-30, 30));
        Register(p);
        p.Name = $"player_{p.NetId}";
        _spawnParent.AddChild(p);
        Players[peer] = p;
        SaveGame.ApplyPlayer(p);
        if (p.Task.Kind == Player.TaskKind.None && p.Faction == Faction.HBC) p.AssignTask(Player.TaskKind.Report, 1, "you are the Company's new hand");
        if (peer == LocalPeerId) SetLocal(p);
        if (Current != Mode.Offline)
        {
            Rpc(nameof(SpawnAgent), Describe(p));
            Rpc(nameof(BindPlayer), peer, p.NetId, name);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BindPlayer(int peer, int netId, string name)
    {
        if (!Agents.TryGetValue(netId, out var a) || a is not PlayerController p) return;
        p.OwnerPeer = peer;
        p.DisplayName = name;
        Players[peer] = p;
        if (peer == LocalPeerId) { p.IsPuppet = false; SetLocal(p); }
    }

    private void SetLocal(PlayerController p)
    {
        LocalPlayer = p;
        Sim.Player = p;
        p.BecomeLocal();
        Sim.LocalPlayerChanged?.Invoke(p);
    }

    // ---- per-frame replication -----------------------------------------------

    public override void _Process(double delta)
    {
        if (Current == Mode.Offline || _spawnParent == null) return;
        if (IsServer)
        {
            _snapshotTimer -= (float)delta;
            if (_snapshotTimer <= 0f) { _snapshotTimer = 1f / 15f; SendSnapshot(); }
            _stateTimer -= (float)delta;
            if (_stateTimer <= 0f) { _stateTimer = 0.4f; SendPlayerStates(); }
            _slowTimer -= (float)delta;
            if (_slowTimer <= 0f) { _slowTimer = 1f; SendSlowState(); }
        }
        else if (LocalPlayer != null && IsInstanceValid(LocalPlayer))
        {
            _snapshotTimer -= (float)delta;
            if (_snapshotTimer <= 0f)
            {
                _snapshotTimer = 1f / 20f;
                RpcId(1, nameof(PlayerMove), LocalPlayer.GlobalPosition, LocalPlayer.CurrentAnimation, LocalPlayer.FlipH, LocalPlayer.IsSneaking, LocalPlayer.IsSprinting);
            }
        }
    }

    // Split into packets that fit under ENet's MTU (unreliable sends can't fragment).
    private void SendSnapshot()
    {
        var arr = new Godot.Collections.Array();
        foreach (var a in Agents.Values)
        {
            if (!IsInstanceValid(a)) continue;
            arr.Add(new Godot.Collections.Array { a.NetId, a.GlobalPosition.X, a.GlobalPosition.Y, a.CurrentAnimation, a.FlipH, a is PlayerController pl && pl.Aboard != null });
            if (arr.Count >= 12) { Rpc(nameof(Snapshot), arr); arr = new Godot.Collections.Array(); }
        }
        if (arr.Count > 0) Rpc(nameof(Snapshot), arr);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void Snapshot(Godot.Collections.Array arr)
    {
        foreach (var e in arr)
        {
            var row = (Godot.Collections.Array)e;
            int id = (int)row[0];
            if (!Agents.TryGetValue(id, out var a) || !IsInstanceValid(a)) continue;
            if (a == LocalPlayer) continue;   // we own our own movement
            a.ApplySnapshot(new Vector2((float)row[1], (float)row[2]), (string)row[3], (bool)row[4]);
            if (a is PlayerController pp && row.Count > 5) pp.SetAfloatMirror((bool)row[5]);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PlayerMove(Vector2 pos, string anim, bool flip, bool sneaking, bool sprinting)
    {
        if (!IsServer) return;
        int peer = Multiplayer.GetRemoteSenderId();
        if (Players.TryGetValue(peer, out var p) && !p.IsDead)
        {
            // Trusting the client with its own feet; sanity-clamp the step.
            if (p.GlobalPosition.DistanceTo(pos) < 600f) p.GlobalPosition = pos;
            p.ApplySnapshot(pos, anim, flip);
            p.SetRemoteMotion(sneaking, sprinting);
        }
    }

    private void SendPlayerStates()
    {
        foreach (var kv in Players)
        {
            if (kv.Key == LocalPeerId || !IsInstanceValid(kv.Value)) continue;
            RpcId(kv.Key, nameof(PlayerState), kv.Value.PackState());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PlayerState(Godot.Collections.Dictionary d)
    {
        LocalPlayer?.UnpackState(d);
    }

    private void SendSlowState()
    {
        var cal = GameCalendar.Instance;
        Rpc(nameof(WorldSync), cal.Year, cal.Month, cal.Day, cal.Hour, (int)cal.Weather.Current);
        var hearths = new Godot.Collections.Array();
        if (Sim.Structures != null) foreach (var h in Sim.Structures.AllHearths) hearths.Add(new Godot.Collections.Array { h.Cell.X, h.Cell.Y, h.FuelHours });
        var ship = Sim.Ship;
        var canoes = new Godot.Collections.Array();
        if (Sim.Spawner != null) foreach (var c in Sim.Spawner.Canoes) canoes.Add(new Godot.Collections.Array { c.Index, c.GlobalPosition.X, c.GlobalPosition.Y, c.RiderNetId });
        var fires = new Godot.Collections.Array();
        if (Fire.I != null) foreach (var c in Fire.I.BurningCells) fires.Add(new Godot.Collections.Array { c.X, c.Y });
        Rpc(nameof(SlowState), hearths, ship != null ? ship.GlobalPosition : Vector2.Zero, ship != null ? (int)ship.Current : 0, SkipStatus(), canoes, fires);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void WorldSync(int year, int month, int day, float hour, int sky)
    {
        GameCalendar.Instance?.ClientSync(year, month, day, hour, (SkyState)sky);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SlowState(Godot.Collections.Array hearths, Vector2 shipPos, int shipState, string skipStatus, Godot.Collections.Array canoes, Godot.Collections.Array fires)
    {
        Fire.I?.ClientSync(fires);
        if (Sim.Spawner != null)
        {
            foreach (var e in canoes)
            {
                var row = (Godot.Collections.Array)e; int idx = (int)row[0];
                if (idx < Sim.Spawner.Canoes.Count) Sim.Spawner.Canoes[idx].SetState(new Vector2((float)row[1], (float)row[2]), (int)row[3]);
            }
        }
        if (Sim.Structures != null)
        {
            foreach (var e in hearths)
            {
                var row = (Godot.Collections.Array)e;
                Sim.Structures.SetHearthFuel(new Vector2I((int)row[0], (int)row[1]), (float)row[2]);
            }
        }
        Sim.Ship?.ClientSync(shipPos, (Ship.State)shipState);
        SkipStatusText = skipStatus;
    }

    // ---- world changes ------------------------------------------------------

    public void BroadcastCell(int layer, Vector2I cell, int source)
    {
        if (IsServer && Current != Mode.Offline) Rpc(nameof(SetCell), layer, cell.X, cell.Y, source);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SetCell(int layer, int x, int y, int source)
    {
        Sim.Structures?.ApplyRemoteCell(layer, new Vector2I(x, y), source);
    }

    public void BroadcastItemGone(int itemIndex)
    {
        if (IsServer && Current != Mode.Offline) Rpc(nameof(ItemGone), itemIndex);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ItemGone(int itemIndex) => Sim.Spawner?.RemoveItem(itemIndex);

    // ---- notices and chat -----------------------------------------------------

    // World-wide notice (the server's Sim.Notice fans out here).
    public void BroadcastNotice(string text)
    {
        if (IsServer && Current != Mode.Offline) Rpc(nameof(NoticeAll), text);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NoticeAll(string text) => Sim.NoticeLocal(text);

    // A line for one player only (their own actions' results).
    public void NoticeTo(PlayerController p, string text)
    {
        if (p == null || string.IsNullOrEmpty(text)) return;
        if (p.OwnerPeer == LocalPeerId) p.EmitSignal(PlayerController.SignalName.Notice, text);
        else if (Current != Mode.Offline) RpcId(p.OwnerPeer, nameof(NoticeOne), text);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NoticeOne(string text) => LocalPlayer?.EmitSignal(PlayerController.SignalName.Notice, text);

    public void SendChat(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (IsServer) Chat(LocalName, text); else RpcId(1, nameof(ChatRequest), text);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ChatRequest(string text)
    {
        if (!IsServer) return;
        Chat(_names.GetValueOrDefault(Multiplayer.GetRemoteSenderId(), "?"), text);
    }

    private void Chat(string who, string text)
    {
        Sim.Notice($"{who}: {text}");
    }

    // ---- player actions (client -> server) ----------------------------------------

    public void Act(string action, Godot.Collections.Array args = null)
    {
        args ??= new Godot.Collections.Array();
        if (IsServer) DoAction(LocalPeerId, action, args);
        else RpcId(1, nameof(ActionRequest), action, args);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ActionRequest(string action, Godot.Collections.Array args)
    {
        if (!IsServer) return;
        DoAction(Multiplayer.GetRemoteSenderId(), action, args);
    }

    private void DoAction(int peer, string action, Godot.Collections.Array args)
    {
        if (!Players.TryGetValue(peer, out var p) || !IsInstanceValid(p)) return;
        string result = p.ServerAction(action, args);
        NoticeTo(p, result);
    }

    // ---- dialogue (server builds it, client shows it) ------------------------------

    public void DialogueRequest(int npcId, int option)
    {
        if (IsServer) HandleDialogue(LocalPeerId, npcId, option);
        else RpcId(1, nameof(DialogueRpc), npcId, option);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DialogueRpc(int npcId, int option)
    {
        if (!IsServer) return;
        HandleDialogue(Multiplayer.GetRemoteSenderId(), npcId, option);
    }

    public void PushDialogue(PlayerController p, Godot.Collections.Dictionary state)
    {
        if (p.OwnerPeer == LocalPeerId) UI.DialogueMenu.Instance?.ShowState(state);
        else RpcId(p.OwnerPeer, nameof(DialogueState), state);
    }

    private void HandleDialogue(int peer, int npcId, int option)
    {
        if (!Players.TryGetValue(peer, out var p)) return;
        var state = p.Conversation.Step(npcId, option);
        if (peer == LocalPeerId) UI.DialogueMenu.Instance?.ShowState(state);
        else RpcId(peer, nameof(DialogueState), state);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DialogueState(Godot.Collections.Dictionary state) => UI.DialogueMenu.Instance?.ShowState(state);

    // ---- agreed time skips -------------------------------------------------------

    public string SkipStatusText = "";

    // Everyone who's alive has to want it before the clock jumps.
    public string SkipStatus()
    {
        int want = 0, alive = 0;
        foreach (var p in Players.Values) { if (!IsInstanceValid(p) || p.IsDead) continue; alive++; if (p.WantsSkipHours > 0f) want++; }
        return want == 0 ? "" : $"Turning in: {want}/{alive}";
    }

    public void ServerCheckSkip()
    {
        int alive = 0; float hours = float.MaxValue;
        foreach (var p in Players.Values)
        {
            if (!IsInstanceValid(p) || p.IsDead) continue;
            alive++;
            if (p.WantsSkipHours <= 0f) return;
            hours = Mathf.Min(hours, p.WantsSkipHours);
        }
        if (alive == 0 || hours == float.MaxValue) return;
        GameCalendar.Instance.SkipHours(hours);
        foreach (var p in Players.Values) if (IsInstanceValid(p)) p.WantsSkipHours = 0f;
        if (Current != Mode.Offline) Sim.Notice(alive > 1 ? "Everyone turns in." : "");
    }
}
