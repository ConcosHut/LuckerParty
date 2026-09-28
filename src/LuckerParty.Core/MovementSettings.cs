namespace LuckerParty.Core;

// Values shared by movement implementations; collision and input stay in the engine.
public sealed record MovementSettings(
    float WalkSpeed = 6f,
    float SprintSpeed = 9f,
    float JumpSpeed = 7.5f,
    float Gravity = 22f);
