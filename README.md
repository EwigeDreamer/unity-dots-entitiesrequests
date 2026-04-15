# Entities Events
Provides inter-system request messaging functionality to Unity ECS

[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE.md)

> **Remark:**
> This package was created following the design and pattern of [EntitiesEvents](https://github.com/annulusgames/EntitiesEvents) by [annulusgames](https://github.com/annulusgames). The key difference is that EntitiesEvents implements one-to-many communication (broadcast), while EntitiesRequests implements many-to-one communication (command). This is useful for implementing commands that one system can send to another. It is also very helpful for communication between systems running at different update rates (e.g., fixed step logic to simulation step logic and vice versa).

## Overview

Entities Requests is a library that adds request‑response style messaging to Unity's Entity Component System (ECS). It allows systems to send requests that persist until explicitly processed, using ```RequestWriter``` and ```RequestReader```.

## Features

* Reliable inter‑system request passing with `RequestWriter` / `RequestReader`
* Custom request container `Requests<T>`

### Requirements

* Unity 2022.3 or higher
* Entities 1.0.0 or higher

### Installation

1. Open the Package Manager from Window > Package Manager.
2. Click the "+" button and select "Add package from git URL."
3. Enter the following URL:

```
https://github.com/EwigeDreamer/unity-dots-entitiesrequests.git
```

## Basic Usage

First, define an unmanaged struct for your request:

```csharp
public struct MyRequest { }
```

Register the request type using the `RegisterRequest` attribute (assembly‑level):

```csharp
using EntitiesRequests;

[assembly: RegisterRequest(typeof(MyRequest))]
```

In the sending system, obtain a `RequestWriter<T>` and write requests:

```csharp
using Unity.Burst;
using Unity.Entities;
using EntitiesRequests;

[BurstCompile]
public partial struct WriteRequestSystem : ISystem
{
    RequestWriter<MyRequest> requestWriter;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        requestWriter = state.GetRequestWriter<MyRequest>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        requestWriter.Write(new MyRequest());
    }
}
```

In the receiving system, obtain a `RequestReader<T>`, read the pending requests, and clear them after processing:

```csharp
using Unity.Burst;
using Unity.Entities;
using EntitiesRequests;

[BurstCompile]
public partial struct ReadRequestSystem : ISystem
{
    RequestReader<MyRequest> requestReader;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        requestReader = state.GetRequestReader<MyRequest>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var request in requestReader.Read())
        {
            // Process each request
        }
        // Explicitly clear the read buffer after processing
        requestReader.Clear();
    }
}
```

If your system inherits from `SystemBase`, you can use `this.GetRequestWriter<T>()` / `this.GetRequestReader<T>()`:

```csharp
[BurstCompile]
public partial class WriteRequestSystemClass : SystemBase
{
    RequestWriter<MyRequest> requestWriter;

    protected override void OnCreate()
    {
        requestWriter = this.GetRequestWriter<MyRequest>();
    }

    protected override void OnUpdate()
    {
        requestWriter.Write(new MyRequest());
    }
}
```

> **Warning**
> Always cache `RequestWriter` and `RequestReader` in `OnCreate`. The reader remembers its state and calling `GetRequestReader()` every frame may lead to unexpected behaviour. Also, do not forget to call `Clear()` on the reader – requests persist until explicitly cleared.

## Request Mechanism

For each type registered with `RegisterRequestAttribute`, Entities Requests creates:

* A singleton entity holding a `RequestSingleton<T>` component
* An internal `RequestSystemBase<T>` that runs inside `RequestSystemGroup` and moves pending writes from the write buffer to the read buffer every frame.

Unlike events, requests are not automatically cleared after being read. The receiving system must call `requestReader.Clear()` to remove processed requests from the read buffer. This allows multiple systems (or the same system over several frames) to inspect the same requests before clearing them.

The double‑buffering has fixed roles:
* `writeBuffer` – always used by `RequestWriter` (supports parallel writing).
* `readBuffer` – always used by `RequestReader` (read and clear).

Every frame, `RequestSystemBase<T>` atomically moves all items from the write buffer to the end of the read buffer, then clears the write buffer. This guarantees that requests written in the current frame are available for reading in the next system update (or later, if the reader chooses not to clear them).

## Requests<T>

A custom `NativeContainer` `Requests<T>` is provided for manual control.

```csharp
using Unity.Collections;
using EntitiesRequests;

// Create a new request container
var requests = new Requests<MyRequest>(32, Allocator.Temp);
```

Call `Update()` to move pending writes to the read buffer (normally done automatically by `RequestSystemBase<T>`):

```csharp
requests.Update();
```

Write and read using `GetWriter()` / `GetReader()`:

```csharp
var writer = requests.GetWriter();
writer.Write(new MyRequest());

var reader = requests.GetReader();
foreach (var req in reader.Read()) { /* process */ }
reader.Clear();   // explicit clear
```

Always dispose the container to avoid memory leaks:

```csharp
requests.Dispose();
```

## License

[MIT License](LICENSE.md)