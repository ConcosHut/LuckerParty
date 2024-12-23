# Game Design

This document details how Lucker Party is intended to work by describing how the game flows from start to finish.

But right now it's just a brainstorming document for the technical architecture.

## Terms

- **Lobby** - The initial game state where players get ready to play a Round.
- **Round** - A number of minigames played back-to-back. A Round starts when the Host starts the game from the Lobby.
- **Minigame** - An individual custom game that all players score points in.
- **Player** - an active member of the round. Players can get points and will be included in the Round stats
- **Spectator** - A Client that's not an active member of the round. The minigame can choose what controls the Spectator
  receives to interact with the scene.

## Lobby

### Creation

When the lobby is initially created by the host, a Lobby UI is presented to the host and anyone else who joins
thereafter.

### Lobby UI

The Lobby UI shows:

- The list of Clients
- Game Settings
    - Number of minigames
- Client Settings
    - Color - This player's visual tint for identification in the mini games
- A Start Game button

When the Start Game button is pressed a countdown starts, and after the first minigame starts

## Round Manager

Manages the current Round. Starting, stopping, triggering minigames from the manager, etc.

Think of the Round Manager as the orchestrator of the set of minigames for the round.

The round manager has different states that is passes through from start to finish of a Round. Some ideas that aren't
implemented yet:

- Countdown before the first minigame
- Starting a minigame
- Pausing a minigame
- Receives signal from the Minigame that it has finished