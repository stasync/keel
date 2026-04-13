# Core

A collection of foundational C# libraries: a utility belt, a UDP networking stack, and an IoC dependency injection container.

---

## Project Structure

```
core/
├── utils/                      # Utilities (logging, threading, reflection, security, math)
├── utils_tests/
├── networking/                 # High-performance UDP networking stack
├── networking_tests/
├── dependency_injection/       # IoC container with scopes and event broadcasting
├── dependency_injection_tests/
└── build.sh                    # Build script
```

---

## Building

```bash
sh build.sh
```

Cleans, builds, and tests all three projects in `release` configuration. Artifacts are written to `artifacts/`.

---

## Projects

### `utils`

Cross-cutting utilities for type reflection, command execution, threading, security, and math.

#### Type Reflection

**`TypeExtensions`**
| Member | Description |
|---|---|
| `GetStableHashCode(this Type)` | Consistent hash code for a type across app runs |
| `IsBlittable(this Type)` | Whether a type is blittable (safe for P/Invoke) |

**`Types`**
| Member | Description |
|---|---|
| `AllTracked` | All types tracked across loaded assemblies |
| `TryGetTypeStableHashCode<T>(out int)` | Stable hash for a generic type |
| `TryGetTypeStableHashCode(Type, out int)` | Stable hash for a runtime type |
| `Lookup<TParent>` | All types assignable to `TParent` |

**`TypeAttributeLookup<TAttribute>`**
| Member | Description |
|---|---|
| `Value` | All types in loaded assemblies that carry `TAttribute` |
| `LookupData` | Struct pairing a type with its attribute instance |

**`TypeInfo`**
| Member | Description |
|---|---|
| `IsBlittable(Type)` | Validates blittability via pinned handle and recursive field checks |

**`DomainUtils`**
| Member | Description |
|---|---|
| `Platform` | Current OS (`Windows` / `Unix` / `Unknown`) |
| `DomainDirectory` | Application base directory |

---

#### Command Execution

**`Command`** (static)
| Member | Description |
|---|---|
| `ExecuteProcessList()` | Returns running OS process list |
| `StartProcess(path, name, title, args[])` | Launches a process with custom working path |
| `StartProcessFromCurrentDirectory(name, args[])` | Launches a process from the current directory |
| `Execute(cmd)` | Runs a shell command asynchronously |
| `ExecuteAndWaitForResult(cmd)` | Runs a shell command and returns its output |

**`CommandLine`** (static)
| Member | Description |
|---|---|
| `Raw` | Raw command-line string |
| `CreateInstance<T>()` | Creates `T` and injects matching command-line arguments |
| `InjectTo<T>(ref T)` | Injects arguments into an existing instance |

**`InjectCommandArgumentAttribute`** — Marks a field or property for command-line injection. `ArgumentName` overrides the default (field name).

---

#### Unique IDs

**`UidProvider`**
| Member | Description |
|---|---|
| `Next()` | Monotonically incrementing `uint` |

---

#### Logging

**`Logger`** (static)
| Member | Description |
|---|---|
| `LogLevel` | Minimum level to emit (`All` / `Warning` / `Error` / `Exception` / `Disabled`) |
| `LogReceivedThreaded` | Event fired on any log write |
| `SetCustomOutput(ILogOutput)` | Replace the default console output |
| `SetLogLevel(LogLevel)` | Change minimum log level at runtime |
| `LogInfo(msg)` / `LogWarning(msg)` / `LogError(msg)` / `LogException(e)` | Write a log entry |

**`ILogOutput`** — Implement to provide a custom log sink:
```csharp
void Info(string msg);
void Warning(string msg);
void Error(string msg);
void Exception(Exception e);
```

**`LogWorker : Worker, ILogOutput`** — File-backed log sink running on a worker thread.
| Member | Description |
|---|---|
| `BuildAndStart(WorkerScope, outputPath?)` | Create and start the worker |
| `Stopped` | Event fired when the worker stops |

---

#### Threading

**`Worker`** (abstract)
| Member | Description |
|---|---|
| `Name` | Thread name |
| `Uid` | Unique worker identifier |
| `Start(WorkerScope)` | Start the update loop |
| `Stop()` | Stop the loop |
| `ElapsedSinceLastUpdate()` | Time since the last `OnUpdate` call |
| `OnStart()` / `OnUpdate()` / `OnStop()` | Override in subclasses |

**`WorkerScope`** enum — `CurrentThread`, `NewForegroundThread`, `NewBackgroundThread`

**`WorkerWatchDog : Worker`** — Monitors workers for deadlocks and logs warnings when update cycles exceed expected time steps.

---

#### Security

**`EncryptionUtility`** (static)
| Member | Description |
|---|---|
| `Encrypt(data, secret)` | AES-256-CBC encryption; returns base64-encoded ciphertext with prepended IV |

**`SecuredInt`** (struct) — XOR-obfuscated integer. Supports all standard arithmetic and comparison operators.

**`MemConsistency`** (static)
| Member | Description |
|---|---|
| `IsValid()` | Detects memory corruption |

---

#### Math

**`RandomExtended`**
| Member | Description |
|---|---|
| `Default` | Thread-static singleton |
| `Value` | `float` in `[0, 1]` |
| `Int(min, max)` | Random integer in range |
| `String(length)` | Random alphanumeric string |
| `Shuffle<T>(collection)` | Fisher-Yates shuffle |
| `GetUniqueIdString()` | Pseudo-unique string combining timestamp and random data |

---

### `networking`

UDP networking stack with reliable delivery, packet ordering, MTU-aware serialization, and a pluggable data-transfer pipeline.

#### Serialization

**`NetWriter`**
| Member | Description |
|---|---|
| `Position` | Current write offset (`short`) |
| `SeekZero()` | Reset write position |
| `ToArray()` | Copy of written bytes |
| `AsArraySegment()` | Zero-copy view of written bytes |
| `WritePackedUInt32(uint)` | Variable-length uint encoding |
| `WritePackedUInt64(ulong)` | Variable-length ulong encoding |
| `WriteString(string)` | UTF-8 string with length prefix |
| `WriteBytesAndSize(ArraySegment<byte>)` | Length-prefixed byte payload |

**`NetReader`**
| Member | Description |
|---|---|
| `Position` | Current read offset |
| `Length` | Buffer length |
| `ReadPackedUInt32()` | Variable-length uint |
| `ReadPackedUInt64()` | Variable-length ulong |
| `ReadString()` | UTF-8 string |
| `ReadBytesAndSize()` | Length-prefixed bytes |

`NetReader` can be constructed from a `NetWriter`, a `byte[]`, or empty.

**`NetBuffer`** — Low-level byte buffer with `ReadByte` / `WriteByte`, `ReadUnmanaged<T>` / `WriteUnmanaged<T>`, `ReadBytesAsArraySegment` (zero-copy), and `Replace` / `SeekZero`.

---

#### Network Utilities

**`Utils`** (static)
| Member | Description |
|---|---|
| `GetIpAddressEthernet()` | IP of the Ethernet interface |
| `GetIPv4Address()` | First available IPv4 address |
| `GetAvailableUdpPort()` | Find a free UDP port |
| `GetAvailableTcpPort()` | Find a free TCP port |
| `IsUdpPortAvailable(int)` | Check if a UDP port is free |
| `CanStartHttpListener(string)` | Verify an HTTP prefix can be bound |

---

#### High-Level UDP (recommended entry points)

**`ReliableUdpListener`** — Server-side connection manager.
| Member | Description |
|---|---|
| `Port` | Bound port |
| `Listen(maxConnections)` | Start accepting connections |
| `Send(connectionUid, data, method)` | Send to a connected client |
| `Disconnect(connectionUid)` | Force-disconnect a client |
| `Update()` | Drive connection and data processing (call every frame/tick) |
| `Connected` | Event — new client connected |
| `Disconnected` | Event — client disconnected |
| `DataReceived` | Event — data arrived from client |
| `ConnectionRejected` | Event — connection rejected |
| `ValidateConnection` | Delegate — custom connection validation |

**`ReliableUdpClient`** — Client-side connection.
| Member | Description |
|---|---|
| `IsConnected` | Connection state |
| `ConnectionUid` | Assigned connection ID |
| `Connect(IPEndPoint, connectionData?)` | Initiate connection |
| `Send(data, method)` | Send data to server |
| `Disconnect()` | Disconnect |
| `Update()` | Drive connection state and heartbeats (call every frame/tick) |
| `Connected` | Event |
| `Disconnected` | Event |
| `ConnectionRejected` | Event |
| `DataReceived` | Event |
| `ReliabilityFailure` | Event — ACK timeout reached |

**`DgramDeliveryMethod`** enum
| Value | Description |
|---|---|
| `Unreliable` | Fire-and-forget |
| `Reliable` | Guaranteed delivery with retransmission |
| `ReliableOrdered` | Guaranteed delivery in send order |

---

#### Low-Level UDP

**`UdpFullProtocol`** — Sits below `ReliableUdpListener` / `ReliableUdpClient`. Handles reliable delivery, duplicate filtering, and ordered queues.

**`UdpReliableProtocol`** — Core UDP socket wrapper with ACK tracking and resend logic.

**`IncomingDataSnapshot`** (struct)
| Member | Description |
|---|---|
| `Uid` | Datagram unique ID |
| `EndPoint` | Sender endpoint |
| `Buffer` | Payload bytes |
| `ProtocolPrefix` | Protocol-layer prefix byte |

---

#### Data Transfer Layer Pipeline

Attach layers to `UdpReliableProtocol` to intercept all outgoing and incoming bytes.

**`DataTransferLayer`** (abstract)
```csharp
void ProcessOutgoingData(IPEndPoint endpoint, ref ArraySegment<byte> data);
void ProcessIncomingData(IPEndPoint endpoint, ref ArraySegment<byte> data);
```

Built-in layers:

| Layer | Description |
|---|---|
| `DataTransferAmountCaptureLayer` | Tracks `Sent` and `Received` byte counters |
| `DataTransferXorEncryptionLayer` | Rotating-key XOR encryption/decryption |

---

#### MTU

`MtuBuffer.SIZE = 1452` bytes (Ethernet 1500 − IP header 20 − UDP header 8). Pooled via `Rent` / `Release`.

---

### `dependency_injection`

Lightweight IoC container with hierarchical scopes, attribute-based injection, pluggable factories, circular-dependency detection, and a built-in pub/sub event system.

#### Binding

**`ScopeBinder`** — Fluent binding configuration.
| Method | Description |
|---|---|
| `Bind<TInterface, TImplementation>(ImplementationBehaviour)` | Explicit interface-to-implementation binding |
| `Bind<TInterface, TImplementation>(Func<TImpl>)` | Binding with a delegate factory |
| `BindToSelf<TImpl>(ImplementationBehaviour)` | Bind a concrete type to itself |
| `BindToAllImplementedInterfaces<TImpl>(ImplementationBehaviour)` | Bind to every interface the type implements |
| `BindToDefaultImplementation<TInterface>(ImplementationBehaviour)` | Bind using `[DefaultImplementation]` attribute |
| `Build()` | Produce a `ScopeDependencyMap` |

**`ImplementationBehaviour`** enum
| Value | Description |
|---|---|
| `Singleton` | One instance shared within a scope |
| `Transient` | New instance per resolution |

---

#### Scopes

**`Scope`** — Resolves dependencies and manages instance lifetimes.
| Member | Description |
|---|---|
| `Provide<TInterface>()` | Resolve `TInterface` to its bound implementation |
| `Provide(Type)` | Non-generic resolution |
| `CreateNestedScope()` | Create a child scope (inherits bindings) |
| `Dispose()` | Dispose all singleton instances in this scope |

**`RootScope : Scope`** — Top-level scope; holds the `DependencyResolvingContext` used during graph construction.

---

#### Attributes

**`[DefaultImplementation(typeof(MyImpl))]`** — Placed on an interface to declare its default implementation. Optionally specify a `FactoryType` for custom instantiation.

**`[Inject]`** — Placed on a field or property to have it automatically populated during resolution.

---

#### Diagnostics

**`CircularDependencyDetector`** (static)
| Member | Description |
|---|---|
| `Validate(ScopeDependencyMap)` | Throws an exception if any circular dependency is detected in the binding map |

---

#### Factories

**`InstanceFactory`** (abstract) — Override `Produce()` to control how an instance is created.

Built-in implementations:
- **`DefaultFactory`** — Uses reflection and `[Inject]` fields.
- **`ProxyFactory<T>`** — Wraps a `Func<T>` delegate.

---

#### Events (pub/sub)

**`IBroadcaster`**
| Member | Description |
|---|---|
| `RegisterObject(object)` | Auto-discover and register all methods marked with `[EventListener]` |
| `UnregisterObject(object)` | Remove all listeners registered from an object |
| `AddListener(eventCode, Action<object[]>)` | Add a listener for an event code |
| `AddListener(channel, eventCode, Action<object[]>)` | Add a channel-scoped listener |
| `RemoveListener(...)` | Remove a listener |
| `Invoke(eventCode, requireReceiver, args[])` | Fire an event on the default channel |
| `Invoke(channel, eventCode, requireReceiver, args[])` | Fire an event on a specific channel |
| `ChannelEventCollection` | Inspect registered listeners |
| `Clear()` | Remove all listeners |

**`Broadcaster : IBroadcaster`** — Concrete implementation. Thread-safe listener registration.

**`[EventListener(eventCode, channel?)]`** — Marks a method as an event handler. Method signature must be `void Method(params object[] args)`.

**`EventParameters`** (struct) — Helper for reading typed arguments from an `object[]` payload.
```csharp
var p = new EventParameters(args);
var x = p.Next<int>();
var name = p.At<string>(1);
```

**`IScopeListener`** — Implement on any class that needs a callback after the DI container finishes resolving the full object graph:
```csharp
void OnResolved();
```

---

#### Introspection

| Interface | Description |
|---|---|
| `IReadOnlyEvent` | `ListenerCount`, `GetListeners()` |
| `IReadOnlyEventCollection` | Event code → `IReadOnlyEvent` |
| `IReadOnlyChannelEventCollection` | Channel → `IReadOnlyEventCollection` |

---

## Quick-Start Examples

### Dependency Injection

```csharp
var binder = new ScopeBinder();
binder.Bind<IService, MyService>(ImplementationBehaviour.Singleton);

var map = binder.Build();
CircularDependencyDetector.Validate(map);

var scope = new RootScope(map);
var svc = scope.Provide<IService>();
```

### Event Broadcasting

```csharp
const ushort OnPlayerJoined = 1;

var broadcaster = new Broadcaster();
broadcaster.AddListener(OnPlayerJoined, args => Console.WriteLine($"Player joined: {args[0]}"));
broadcaster.Invoke(OnPlayerJoined, requireReceiver: false, "Alice");

// Attribute-based registration
class GameManager {
    [EventListener(OnPlayerJoined)]
    void HandleJoin(params object[] args) { ... }
}
broadcaster.RegisterObject(new GameManager());
```

### UDP Client / Server

```csharp
// Server
var listener = new ReliableUdpListener();
listener.DataReceived += (uid, data) => { /* handle */ };
listener.Listen(maxConnections: 32);
while (true) { listener.Update(); }

// Client
var client = new ReliableUdpClient();
client.Connected += uid => Console.WriteLine("Connected");
client.Connect(new IPEndPoint(IPAddress.Loopback, 7777));
while (true) { client.Update(); }
client.Send(payload, DgramDeliveryMethod.Reliable);
```

