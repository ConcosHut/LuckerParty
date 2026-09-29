namespace LuckerParty.Core;

public enum PartyPhase { Lobby, Countdown, Instructions, Choosing, Reveal, GameResults, FinalResults }
public enum PartyRole { Player, Spectator }
public enum RpsChoice { Rock, Paper, Scissors }

public sealed record PartyTiming(double Countdown = 3, double Instructions = 5, double Choice = 8,
    double Reveal = 3, double Results = 4);

public sealed record PartyMemberView(long PlayerId, int PeerId, string Name, int ColorIndex, PartyRole Role,
    bool Ready, bool Connected, bool Playing, int Score, int Wins, bool Participated, bool Submitted, RpsChoice? Choice);

// Only this recipient-specific projection goes onto the wire. Hidden hands stay in PartyCoordinator.
public sealed record PartySnapshot(long Revision, long PartyId, PartyPhase Phase, double ServerTime, double Deadline,
    int Throw, int ThrowCount, string Message, string StartBlocker, PartyMemberView[] Members,
    long[] Winners, RpsChoice? YourChoice);

public static class PartyPalette
{
    public static readonly string[] Colors = ["#FF6678", "#278568", "#E4AF34", "#477FCE", "#9764CF", "#E88740", "#38A6B2", "#BC5D8D"];
}
