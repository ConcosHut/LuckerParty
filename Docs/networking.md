# Networking

In order for multiplayer to work correctly, we need every developer to know how networking works in S&box.

Here are the tools we have in our toolbox to architect our code with networking in mind:

- Networked Objects
    - Ownership
- Sync Properties
- RPC Messages
- Network Events

This list is taken from the S&box dev docs: https://sbox.game/dev/doc/networking-multiplayer/

This document will summarize those and expand on them with information I find relevant.

## Networked Objects

[S&box docs](https://sbox.game/dev/doc/networking-multiplayer/networked-objects/)

All `GameObject`s have a Network Mode:

- `NetworkMode.Never` - Isn't networked to others
- (Default) `NetworkMode.Snapshot` - Sent by the host to clients when they join
- `NetworkMode.Object` - Sent to other clients on command and can have Sync Properties and RPCs

NOTE: `Snapshot` and `Object` are swapped in the above list in the docs but I think it makes more sense to list them in
order
of increasing network-capability

The only ways to change the NetworkMode from the default of `Snapshot` is from a dropdown in the editor inspector or at
runtime with `GameObject.NetworkSpawn()`.

NOTE: Technically the `NetworkMode` property on `GameObject` has a setter,
but [according to Carson (Facepunch dev) on Discord "network spawn sets the mode, afaik you shouldnt be able to directly set the networkmode on a gameobject"](https://discord.com/channels/833983068468936704/833983416390385685/1263135637838495794).
So we shouldn't use that.

Calling `NetworkSpawn()` on a `GameObject` has the following effects:

1. The owner will automatically send Transform (position, rotation, scale) updates to everyone for this `GameObject` and
   its descendants.
2. The owner will send updates for Synced Properties on this `GameObject` and its descendants
3. RPC Messages can be called on this `GameObject` and its descendants