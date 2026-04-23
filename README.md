# Entities Requests

[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE)

A library that adds request‑response style messaging to Unity's Entity Component System (ECS). Enables reliable command passing between systems using `RequestWriter` and `RequestReader`.

> **Note:** The Entities Requests library implements a **command (many‑to‑one)** communication pattern. Multiple systems can write requests of the same type, but a single consumer system is expected to read and explicitly clear them. Requests persist until cleared, making them suitable for commands, work orders, or any scenario where a request must be reliably processed exactly once.

## Features
* Reliable inter‑system request passing with `RequestWriter` / `RequestReader`.
* Requests persist until explicitly cleared by the consuming system.
* Supports parallel writing from multi‑threaded jobs (`IJobParallelFor`, `IJobParallelForBatch`).
* Automatic lifecycle management via ECS singletons and generated systems.
* Source generator eliminates manual request type registration.

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

Register the request type with an assembly attribute (generates the required system):

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

## Parallel Writing from Jobs

Use `RequestWriter<T>.ParallelWriter` for writing from multi‑threaded jobs. The writer is thread‑safe and supports both `IJobParallelFor` and `IJobParallelForBatch` with `ScheduleParallel`, allowing many threads to write concurrently without data corruption.

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

In your system, cache the writer and schedule the job:

```csharp
private RequestWriter<MyRequest> _writer;

protected override void OnCreate()
{
    _writer = this.GetRequestWriter<MyRequest>();
}

protected override void OnUpdate()
{
    // Ensure capacity before scheduling
    this.EnsureRequestBufferCapacity<MyRequest>(requestCount);

    var parallelWriter = _writer.AsParallelWriter();
    var job = new ParallelJob { Writer = parallelWriter };
    Dependency = job.Schedule(requestCount, 64, Dependency);
}
```

For batch parallel jobs, use `IJobParallelForBatch` and `ScheduleParallel` — the writer remains safe under high contention.

Always call `EnsureRequestBufferCapacity` before parallel writes to avoid reallocations inside the job.

## Manual Usage (Without ECS)
You can create an `Requests<T>` container directly:

```csharp
var requests = new Requests<int>(64, Allocator.Persistent);
var writer = requests.GetWriter();
var reader = requests.GetReader();

writer.Write(42);
requests.Update(); // moves writes to read buffer

foreach (int val in reader.Read())
{
    Debug.Log(val); // 42
}
reader.Clear(); // explicit clear

requests.Dispose();
```

## Performance Considerations
* Cache `RequestWriter` and `RequestReader` in `OnCreate` – they safely update internal pointers when buffers are updated.
* Always call `EnsureRequestBufferCapacity` before parallel writes to avoid reallocations inside jobs.
* Ensure all write jobs are completed before the end of the frame – the request system automatically waits for dependencies declared via `GetComponentTypeHandle`.
* Call `Clear()` on the reader as soon as processing is done to avoid accumulating stale requests.

## License

[MIT License](LICENSE.md)

## Acknowledgements
This project is based on the design of [EntitiesEvents](https://github.com/annulusgames/EntitiesEvents) by [annulusgames](https://github.com/annulusgames), adapted for many‑to‑one command messaging with explicit clearing.

Created with the support of artificial intelligence.