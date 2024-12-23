# Code Patterns

This document holds some of the code patterns I've arrived at when writing code in the project.

## Implementing Components

### `OnStart()`

> Called when the component is enabled for the first time

Override this if your component is only going to be enabled once or if you do not have resources that need to be added
or
removed when its enabled / disabled.

If you do need to do cleanup and setup on enable / disable, override `OnEnabled` and `OnDisabled` instead.

## Instantiating Components

Components don't have constructors, so you need another way of providing dependencies to them when they are instantiated
elsewhere. There's probably a better way to do this, but for now we just configure them immediately after calling
`GameObject.AddComponent<T>()`:

```csharp
var client = gameObject.AddComponent<Client>();
client.Connection = channel;
```

Shortly after this execution scope (I'm not sure exactly when) its `OnStart()` and `OnEnabled()` methods will
be called, which contain the initialization logic.

As long as the component is configured correctly after it's been added, it should work correctly, but that raises the
question: How do we manage the validation of properties in the Component implementation? I haven't found a satisfying
answer
yet.

