# Keel

[![Build](https://github.com/stasync/keel-dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/stasync/keel-dotnet/actions/workflows/build.yml)

Keel is a small set of C# libraries that serve as a base layer for other projects. It has three parts:

- **Networking** is a UDP client and server that can deliver messages reliably and in order. Use it where TCP is too slow or too rigid, such as real-time games and simulations.
- **Dependency Injection** is a lightweight container for wiring services together, with an event system built in.
- **Utils** covers the everyday pieces: logging, background worker threads, command-line settings, encryption, and a few reflection helpers.

The libraries target .NET Standard 2.1 and .NET 10, so they run on any runtime that supports either. Networking and Dependency Injection both depend on Utils, but not on each other, so you can use just the one you need.

## Getting started

The quickest way to get the libraries is to download them from [Releases](https://github.com/stasync/keel-dotnet/releases). Every release has two zips, one for .NET 10 and one for .NET Standard 2.1. Each contains `Keel.Utils.dll`, `Keel.Networking.dll` and `Keel.DependencyInjection.dll`. Reference the DLLs you need from your project.

To build from source instead, you need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone git@github.com:stasync/keel-dotnet.git
cd keel
sh build.sh
```

The build script compiles everything in release mode, runs the tests, and copies the finished DLLs into `artifacts/delivery/`, with one folder per target framework:

```
artifacts/delivery/
  release_net10.0/
  release_netstandard2.1/
```

To use the libraries, reference the DLLs from your project, or add this repository as a git submodule and reference the `.csproj` files directly.

If you need a change that hasn't made it into a numbered release yet, use the [latest build](https://github.com/stasync/keel-dotnet/releases/tag/latest-build). It's rebuilt and tested automatically after every change to `main`, so it's always current, but it isn't a stable version.

## Networking

The networking library sends messages over UDP. Plain UDP gives no guarantees: packets can be lost, duplicated, or arrive out of order. This library adds the missing pieces on top, and you choose how much of them each message needs.

| Delivery method | What you get | Good for |
|---|---|---|
| `Unreliable` | Sent once, may be lost | Frequent updates where only the latest value matters, like positions |
| `Reliable` | Always arrives, possibly out of order | One-off events, like "item picked up" |
| `ReliableOrdered` | Always arrives, in the order it was sent | Chat, commands, anything where order matters |

Nothing runs in the background. Both the server and the client do their work when you call `Update()`, so you decide when networking happens. That fits a game loop or a fixed-rate server tick.

### A simple server

```csharp
using System;
using System.Threading;
using Keel.Networking.Udp;

// Port 0 picks any free port; read it back from server.Port.
var server = new ReliableUdpServer(maxConnections: 32, port: 7777, protocolKey: 0);

server.Connected += (connectionUid, request) =>
    Console.WriteLine($"Client {connectionUid} connected from {request.EndPoint}");

server.Disconnected += connectionUid =>
    Console.WriteLine($"Client {connectionUid} left");

// Echo every message back to whoever sent it, using the same delivery method.
server.DataReceived += (connectionUid, data, deliveryMethod) =>
    server.SendTo(connectionUid, data, deliveryMethod);

while (true)
{
    server.Update();
    Thread.Sleep(10);
}
```

### A simple client

```csharp
using System;
using System.Net;
using System.Threading;
using Keel.Networking.Udp;
using Keel.Networking.Udp.LowLevel;

var client = new ReliableUdpClient();

client.Connected += () =>
{
    Console.WriteLine($"Connected with id {client.ConnectionUid}");
    client.Send(new byte[] { 1, 2, 3 }, UdpFullProtocol.DgramDeliveryMethod.Reliable);
};

client.DataReceived += (data, deliveryMethod) =>
    Console.WriteLine($"Server sent back {data.Length} bytes");

client.ConnectionRejected += reason =>
    Console.WriteLine($"Server refused the connection: {reason}");

client.Connect(new IPEndPoint(IPAddress.Loopback, 7777));

while (true)
{
    client.Update();
    Thread.Sleep(10);
}
```

### Sending structured data

Messages are plain byte arrays. Use `NetWriter` and `NetReader` to pack values into them and read them back, in the same order.

```csharp
using Keel.Networking;

var writer = new NetWriter();
writer.WriteInt32(42);           // player id
writer.WriteString("Alice");     // name
writer.WriteSingle(3.5f);        // speed

client.Send(writer.AsArraySegment(), UdpFullProtocol.DgramDeliveryMethod.ReliableOrdered);
```

```csharp
server.DataReceived += (connectionUid, data, deliveryMethod) =>
{
    var reader = new NetReader(data);
    var playerId = reader.ReadInt32();
    var name = reader.ReadString();
    var speed = reader.ReadSingle();
};
```

`NetWriter` also has compact encodings for whole numbers (`WritePackedUInt32`, `WritePackedUInt64`), which use fewer bytes for small values.

### Deciding who can connect

A client can send a short string when it connects, such as a login token or a game version. The server looks at it and decides whether to accept the connection.

```csharp
// Server
server.ValidateConnection += (in ReliableUdpServer.ConnectionRequest request) =>
    request.ConnectionData == "game-v1.4";

// Client
client.Connect(serverEndPoint, connectionData: "game-v1.4");
```

A rejected client gets a `ConnectionRejected` event, with the reason: either the server is full or validation failed.

Misbehaving addresses can be blocked for a while with `server.AddOrUpdateAddressInBlacklist(address, millisecondsToAdd)`.

### Testing on a bad network

Everything works on your local machine, but real networks lose and delay packets. You can attach simulators to see how your code copes:

```csharp
using Keel.Networking.Udp.LowLevel.Simulators;

client.RegisterIncomingPacketSimulator(new SimulatePacketLossByChance { PacketLossChancePercent = 5 });
client.RegisterIncomingPacketSimulator(new SimulatePacketDelay { PacketMinDelayMs = 50, PacketMaxDelayMs = 150 });
```

There are also simulators for duplicated packets and for dropping everything, to test disconnects.

### Measuring and transforming traffic

Data transfer layers let you inspect or modify every packet going in or out. Two layers are built in: one counts traffic and one applies simple XOR encryption.

```csharp
using Keel.Networking.Udp.LowLevel.DataTransferLayers;

var traffic = new DataTransferAmountCaptureLayer();
client.RegisterDataTransferLayer(traffic);

// later
Console.WriteLine($"Sent {traffic.BytesSent} bytes in {traffic.PacketsSent} packets");
```

To write your own layer, inherit from `UdpReliableProtocol.DataTransferLayer`.

## Dependency Injection

The container builds your objects for you and fills in the services they depend on. You describe which implementation goes with each interface, then ask the scope for what you need.

```csharp
using System;
using Keel.DependencyInjection;

public interface IClock
{
    DateTime Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}

public interface IGreeter
{
    string Greet(string name);
}

public sealed class Greeter : IGreeter
{
    [Inject] private readonly IClock _clock = null;

    public string Greet(string name) => $"Hello {name}, it is {_clock.Now:t}";
}
```

```csharp
var binder = new ScopeBinder();
binder.Bind<IClock, SystemClock>();
binder.Bind<IGreeter, Greeter>();

using var scope = new RootScope(binder.ToDependencyMap());

var greeter = scope.Provide<IGreeter>();
Console.WriteLine(greeter.Greet("Alice"));
```

Dependencies are filled into fields and auto-properties marked with `[Inject]`. Classes are created through their parameterless constructor.

### Lifetimes

Each binding is either a `Singleton` (the default), where one instance is shared within the scope, or `Transient`, where every request creates a new instance.

```csharp
binder.Bind<IGreeter, Greeter>(ImplementationBehaviour.Transient);
```

### Other ways to bind

```csharp
// Bind a class to itself, without an interface.
binder.BindToSelf<GameSession>();

// Bind a class to every interface it implements.
binder.BindToAllImplementedInterfaces<AudioSystem>();

// Build the instance yourself.
binder.Bind<ISettings, Settings>(() => Settings.Load("settings.json"));
```

An interface can also declare its usual implementation, so callers don't have to repeat it:

```csharp
[DefaultImplementation(typeof(SystemClock))]
public interface IClock { DateTime Now { get; } }

binder.BindToDefaultImplementation<IClock>();
```

### Child scopes

A child scope adds its own bindings and can still use everything from its parent. This works well for things with a shorter life than the application, such as a single match or a user session.

```csharp
var matchBinder = new ScopeBinder();
matchBinder.Bind<IMatchState, MatchState>();

using var match = scope.CreateChildScope(matchBinder.ToDependencyMap());

// MatchState can [Inject] IClock from the parent scope.
var state = match.Provide<IMatchState>();
```

### Running code once everything is wired

Injected fields are empty while an object is being constructed. If a service needs to do setup work that uses its dependencies, implement `IScopeListener`. `OnResolved` is called after the whole object graph has been built.

```csharp
using Keel.DependencyInjection.Interface;

public sealed class Scoreboard : IScopeListener
{
    [Inject] private readonly IClock _clock = null;

    public void OnResolved()
    {
        // _clock is ready to use here.
    }
}
```

Circular dependencies (A needs B, B needs A) are detected when the scope is created, and throw an exception that names the types involved.

## Events

The event system lets parts of your program talk to each other without holding references to each other. One side sends an event with a numeric code, and anyone listening for that code receives it.

```csharp
using System;
using Keel.DependencyInjection.Events;

const ushort PlayerJoined = 1;

var events = new Broadcaster();

events.AddListener(PlayerJoined, args =>
{
    var p = new EventParameters(args);
    var name = p.Next<string>();
    var level = p.Next<int>();
    Console.WriteLine($"{name} joined at level {level}");
});

events.Invoke(PlayerJoined, requireReceiver: false, "Alice", 12);
```

Set `requireReceiver` to `true` if nobody listening would be a mistake. An error is then logged whenever an event goes unheard.

The broadcaster fits well with the container. Bind `IBroadcaster`, inject it, and register whole objects. Their methods marked with `[EventListener]` are picked up automatically.

```csharp
public static class GameEvents
{
    public const ushort PlayerJoined = 1;
}

public sealed class Scoreboard : IScopeListener
{
    [Inject] private readonly IBroadcaster _events = null;

    public void OnResolved() => _events.RegisterObject(this);

    [EventListener(GameEvents.PlayerJoined)]
    private void OnPlayerJoined(object[] args)
    {
        var name = new EventParameters(args).Next<string>();
        // update the scoreboard
    }
}
```

```csharp
binder.BindToDefaultImplementation<IBroadcaster>();
binder.BindToSelf<Scoreboard>();
```

Events can also be split into channels, so the same code can mean different things in different parts of the program. Pass `channel` to `AddListener`, `Invoke` and `[EventListener]`.

## Utils

### Logging

```csharp
using Keel.Utils.Debug;

Logger.LogInfo("Server started");
Logger.LogWarning("Config file missing, using defaults");
Logger.LogError("Could not bind port");

Logger.SetLogLevel(LogLevel.Warning); // hide info messages
```

Nothing is printed until you choose where logs should go. `LogWorker` prints them to the console in color on a background thread, and can also save them to a file:

```csharp
using Keel.Utils.Threading;

var log = LogWorker.BuildAndStart(WorkerScope.NewBackgroundThread, "logs/server.log");
Logger.SetCustomOutput(log);
```

Leave out the file path to log to the console only. To send logs anywhere else, implement `ILogOutput`.

### Background workers

A `Worker` runs a piece of code in a loop at a fixed interval, on its own thread or on the current one.

```csharp
using Keel.Utils.Debug;
using Keel.Utils.Threading;

public sealed class AutoSave : Worker
{
    public AutoSave() : base(millisecondsTimeStep: 60_000, workerName: "AutoSave", monitor: false) { }

    protected override void OnUpdate() => Logger.LogInfo("Saving...");
}

var autoSave = new AutoSave();
autoSave.Start(WorkerScope.NewBackgroundThread);

// later
autoSave.Stop();
```

`OnStart` and `OnStop` can be overridden as well. `WorkerWatchDog` can watch the other workers and warn when one of them stalls.

### Launch arguments

Mark fields with `[InjectCommandArgument]` and they are filled from the command line.

```csharp
using Keel.Utils;

public sealed class ServerSettings
{
    [InjectCommandArgument("port")] public int Port = 7777;
    [InjectCommandArgument] public int maxPlayers = 16;
}

// MyServer -port 9000 -maxPlayers 64
var settings = LaunchArguments.CreateInstance<ServerSettings>();
```

Values can also come from files placed next to the executable. A file named `port.cmdparam` that contains `9000` has the same effect as passing `-port 9000`.

### Encryption

```csharp
using Keel.Utils.Security;

var secret = "6f9b2c1e-4d7a-4e8b-9a3f-1c5d7e9b2a40"; // a long random value, not a password
var encrypted = EncryptionUtility.Encrypt("secret message", secret);
var decrypted = EncryptionUtility.Decrypt(encrypted, secret);
```

This uses AES-256 with an HMAC-SHA256 integrity check, so `Decrypt` throws if the data was modified or the secret is wrong. The result is a plain string, so it's easy to store or send.

The secret is hashed straight into a key, without a slow password hash, so use a long random value such as a GUID rather than something a person would pick.

### Other helpers

- `RandomExtended` generates random numbers, strings and IDs, and shuffles collections.
- `UidProvider` hands out increasing unique numbers.
- `Types` and `TypeAttributeLookup<T>` find types across the loaded assemblies, for example every class that implements an interface or carries an attribute.
- `DomainUtils` tells you the current platform and the application directory.

## Project layout

```
utils/                        shared helpers used by the other two libraries
utils_tests/
networking/                   UDP client, server and serialization
networking_tests/
dependency_injection/         container, scopes and events
dependency_injection_tests/
build.sh                      builds, tests and packages everything
```

## Running the tests

```bash
dotnet test utils_tests
dotnet test networking_tests
dotnet test dependency_injection_tests
```

`build.sh` runs all three suites as part of the full build.

## License

Keel is released under the [MIT License](LICENSE). You can use it in any project, commercial or not, as long as you keep the copyright notice.
