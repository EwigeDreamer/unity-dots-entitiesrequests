# Entities Requests

[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE)

A library that adds request‑response style messaging to Unity's Entity Component System (ECS). Enables reliable command passing between systems using `RequestWriter` and `RequestReader`.

> **Note:** The Entities Requests library implements a **command (many‑to‑one)** communication pattern. Multiple systems can write requests of the same type, but a single consumer system is expected to read and explicitly clear them. Requests persist until cleared, making them suitable for commands, work orders, or any scenario where a request must be reliably processed exactly once.

## Concepts
* **Bank** — the per‑type container that owns the request buffers. It is created lazily by the first request of a type and closed by the generated owner system.
* **Card** — a client handle of a bank. `RequestWriter<T>` writes requests into the bank, `RequestReader<T>` reads them; a card is created in `OnCreate` and disposed in `OnDestroy`.

## Features
* Reliable inter‑system request passing with `RequestWriter` / `RequestReader`.
* Requests persist until explicitly cleared by the consuming system.
* Parallel writing from multi‑threaded jobs (`IJobParallelFor`, `IJobParallelForBatch`) with a thread‑safe `ParallelWriter`.
* Fully Burst friendly: the generated request system and its merge job are Burst compiled.
* Source generator: one assembly attribute per request type emits the system that owns and drains it.
* Works with both managed (`SystemBase`) and unmanaged (`ISystem`) systems.
* A self‑contained core that can be used without ECS.

## Requirements
* Unity 6000.0 or higher
* Packages:
  * `com.unity.entities` 1.4.5 or higher
  * `com.unity.collections` 2.6.5 or higher
  * `com.unity.burst` 1.8.27 or higher

## Installation
Add the package via Package Manager using the git URL:
```
https://github.com/EwigeDreamer/unity-dots-entitiesrequests.git
```

## Basic Usage
Define a request struct:

```csharp
public struct MyRequest
{
    public int Value;
}
```

Register the request type with an assembly attribute. The source generator emits the system that owns the requests of this type:

```csharp
using ED.DOTS.EntitiesRequests;

[assembly: RegisterRequest(typeof(MyRequest))]
```

Sender system:

```csharp
using Unity.Entities;
using ED.DOTS.EntitiesRequests;

public partial class SenderSystem : SystemBase
{
    private RequestWriter<MyRequest> _writer;

    protected override void OnCreate()
    {
        _writer = this.GetRequestWriter<MyRequest>();
    }

    protected override void OnDestroy()
    {
        _writer.Dispose();
    }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _writer.Write(new MyRequest { Value = 123 });
    }
}
```

Receiver system:

```csharp
using Unity.Entities;
using ED.DOTS.EntitiesRequests;

public partial class ReceiverSystem : SystemBase
{
    private RequestReader<MyRequest> _reader;

    protected override void OnCreate()
    {
        _reader = this.GetRequestReader<MyRequest>();
    }

    protected override void OnDestroy()
    {
        _reader.Dispose();
    }

    protected override void OnUpdate()
    {
        foreach (var req in _reader.Read())
        {
            Debug.Log($"Received: {req.Value}");
        }
        // Must explicitly clear the read buffer after processing
        _reader.Clear();
    }
}
```

> **Note:** The generated owner merges all pending writes at the end of the simulation phase, after `LateSimulationSystemGroup`. A reader that runs during the simulation therefore sees the requests of the **previous tick**, never the current one.

The same extensions are available on `ISystem` for Burst‑compiled systems, taking a `ref SystemState`:

```csharp
_writer = state.GetRequestWriter<MyRequest>();
_reader = state.GetRequestReader<MyRequest>();
```

## Parallel Writing from Jobs

Use `RequestWriter<T>.ParallelWriter` for writing from multi‑threaded jobs. `WriteNoResize` is thread‑safe and supports both `IJobParallelFor` and `IJobParallelForBatch` with `ScheduleParallel`, allowing many threads to write concurrently without data corruption.

```csharp
[BurstCompile]
struct ParallelJob : IJobParallelFor
{
    public RequestWriter<MyRequest>.ParallelWriter Writer;

    public void Execute(int index)
    {
        Writer.WriteNoResize(new MyRequest { Value = index });
    }
}
```

In your system, reserve capacity first and then schedule the job:

```csharp
private RequestWriter<MyRequest> _writer;
private const int RequestCount = 1000;

protected override void OnCreate()
{
    _writer = this.GetRequestWriter<MyRequest>(RequestCount);
}

protected override void OnUpdate()
{
    // WriteNoResize never grows the buffer, so reserve capacity before scheduling
    _writer.EnsureCapacity(RequestCount);

    var job = new ParallelJob { Writer = _writer.AsParallelWriter() };
    Dependency = job.Schedule(RequestCount, 64, Dependency);
}
```

## Manual Usage (Without ECS)
The core is a plain heap structure and can be used directly, without a world:

```csharp
using ED.DOTS.EntitiesRequests;
using Unity.Collections;

var bank = new RequestBank<int>(Allocator.Persistent, 128);
var writer = new RequestWriter<int>(bank, Allocator.Persistent, 128);
var reader = new RequestReader<int>(bank, Allocator.Persistent);

writer.Write(42);
bank.Merge(); // moves every pending write into the shared read buffer

foreach (var value in reader.Read())
{
    Debug.Log(value); // 42
}
reader.Clear(); // explicit clear

writer.Dispose();
reader.Dispose();
bank.Dispose();
```

The bank owns its buffers and is created first. Each card allocates its own memory from the allocator you pass to it and frees it on `Dispose`, so a card safely outlives the bank; that allocator must outlive the card. `Merge` must run in a job‑free window.

## Notes and Semantics
* The pattern is **many‑to‑one**: many writers, one reader per request type. For one‑to‑many fan‑out use the sibling EntitiesEvents package.
* Requests accumulate in a shared read buffer and stay there until you call `Clear()`.
* Register each request type **exactly once** with `[assembly: RegisterRequest(typeof(T))]`. Registering the same type twice fails the build; different request types may share a short name across namespaces.
* Take cards in `OnCreate` and dispose them in `OnDestroy`. Taking a card in `OnUpdate` is not supported.
* If the request type is registered but its generated owner is not present in this world, the bank is not created: taking a card yields a logged no‑op instead of a bank nobody would ever close.
* The `[assembly: RegisterRequest(typeof(T))]` attribute is mandatory. Without it the request marker component is never registered, the engine cannot resolve it, and taking a card throws — there is no graceful fallback.
* A parallel writer never resizes its buffer: reserve capacity with `EnsureCapacity` before scheduling the job that writes through it.

## Performance Considerations
* Cache `RequestWriter` and `RequestReader` in `OnCreate` – they safely reference internal buffers.
  Always call `Dispose()` on your cards (in system `OnDestroy`) to free their blocks.
* Reserve capacity with `EnsureCapacity` before parallel writes to avoid reallocations.
* Call `Clear()` on the reader as soon as processing is done to avoid accumulating stale requests.

## License

[MIT License](LICENSE.md)

## Acknowledgements
This project was inspired by the original [EntitiesEvents](https://github.com/annulusgames/EntitiesEvents) concept by [annulusgames](https://github.com/annulusgames), but has been completely rewritten around an independent writer/reader ownership model, explicit disposal, and Burst‑compiled drain systems.

Created with the support of artificial intelligence.
