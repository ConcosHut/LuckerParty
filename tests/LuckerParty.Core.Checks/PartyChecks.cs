using LuckerParty.Core;

internal static class PartyChecks
{
    public static void Run(Action<bool, string> require)
    {
        require(PartyCoordinator.Beats(RpsChoice.Rock, RpsChoice.Scissors) &&
            PartyCoordinator.Beats(RpsChoice.Scissors, RpsChoice.Paper) &&
            PartyCoordinator.Beats(RpsChoice.Paper, RpsChoice.Rock), "Rock beats scissors, scissors beats paper, paper beats rock");
        foreach (var hand in Enum.GetValues<RpsChoice>())
        foreach (var other in Enum.GetValues<RpsChoice>())
            require(hand == other ? !PartyCoordinator.Beats(hand, other)
                : PartyCoordinator.Beats(hand, other) != PartyCoordinator.Beats(other, hand), $"RPS comparison {hand}/{other}");

        var full = new PartyCoordinator();
        for (var peer = 1; peer <= 9; peer++) full.Join(peer, "Player " + peer, 0);
        require(full.SnapshotFor(1, 0).Members.Length == 8 && full.SnapshotFor(1, 0).Members.Select(m => m.ColorIndex).Distinct().Count() == 8,
            "Eight connected members receive unique colors and a ninth cannot enter");

        var lobby = new PartyCoordinator();
        lobby.Join(1, "Host", 0); lobby.Join(2, "Friend", 0);
        require(!lobby.Start(2, 0) && !lobby.Start(1, 0), "Only the host can start, after all players ready");
        require(!lobby.Configure(2, 1) && !lobby.SetColor(2, 0) && !lobby.SetReady(99, true),
            "Foreign host settings, used colors and unknown members are rejected");
        lobby.SetReady(1, true); lobby.SetReady(2, true);
        require(lobby.Configure(1, 1) && lobby.SnapshotFor(1, 0).Members.All(m => !m.Ready), "Changing throws resets readiness");
        lobby.SetReady(1, true); lobby.SetReady(2, true); lobby.Start(1, 0);
        lobby.Join(3, "Arrival", 1);
        require(lobby.Phase == PartyPhase.Lobby && lobby.SnapshotFor(1, 1).Members.All(m => !m.Ready),
            "Joining during start countdown cancels it and resets readiness");

        var rules = Choosing(3);
        require(!rules.Submit(1, rules.PartyId - 1, 1, 1, RpsChoice.Rock, .3) &&
            !rules.Submit(1, rules.PartyId, 2, 1, RpsChoice.Rock, .3) &&
            !rules.Submit(1, rules.PartyId, 1, 1, (RpsChoice)99, .3), "Wrong party, stale throw and invalid hands are rejected");
        require(rules.Submit(1, rules.PartyId, 1, 1, RpsChoice.Rock, .3), "A valid hand locks once");
        var privateState = rules.SnapshotFor(1, .3); var publicState = rules.SnapshotFor(2, .3);
        require(privateState.YourChoice == RpsChoice.Rock && publicState.YourChoice is null &&
            publicState.Members.All(m => m.Choice is null), "Only the owner sees its locked hand before reveal");
        require(!rules.Submit(1, rules.PartyId, 1, 2, RpsChoice.Paper, .31), "A submitted hand cannot be changed");
        rules.Submit(2, rules.PartyId, 1, 1, RpsChoice.Rock, .32);
        rules.Submit(3, rules.PartyId, 1, 1, RpsChoice.Scissors, .33);
        require(rules.Phase == PartyPhase.Reveal && rules.SnapshotFor(3, .33).Members.All(m => m.Choice is not null),
            "All locked hands reveal together and resolve on the server");
        rules.Advance(.44); rules.Advance(.55);
        var final = rules.SnapshotFor(1, .55);
        require(final.Phase == PartyPhase.FinalResults && final.Members.Select(m => m.Score).SequenceEqual([10, 10, 3]) && final.Winners.Length == 2,
            "Tied first places share 10 points, skip second and crown joint winners");
        rules.Advance(100);
        require(rules.SnapshotFor(1, 100).Members.Select(m => m.Score).SequenceEqual([10, 10, 3]), "Results award points exactly once");
        require(!rules.PlayAgain(2) && rules.PlayAgain(1) && rules.SnapshotFor(1, 100).Members.All(m => !m.Ready && !m.Playing),
            "Host replay returns the same members to an unready lobby");
        var oldParty = rules.PartyId;
        foreach (var member in final.Members) rules.SetReady(member.PeerId, true);
        rules.Start(1, 101);
        require(rules.PartyId > oldParty && rules.SnapshotFor(1, 101).Members.All(m => m.Score == 0 && m.Wins == 0),
            "A second party clears scores and gets a new command identity");

        var missing = Choosing(2);
        require(!missing.Submit(2, missing.PartyId, 1, 1, RpsChoice.Rock, 1.2), "A hand arriving at the server deadline is too late");
        missing.Submit(1, missing.PartyId, 1, 1, RpsChoice.Paper, .3);
        missing.Advance(1.3); missing.Advance(1.5); missing.Advance(1.7);
        require(missing.SnapshotFor(1, 1.7).Members.Select(m => m.Score).SequenceEqual([10, 0]),
            "A missed hand forfeits and zero participation receives no points");
        var nobody = Choosing(2); nobody.Advance(2); nobody.Advance(3); nobody.Advance(4);
        require(nobody.SnapshotFor(1, 4).Winners.Length == 0 && nobody.SnapshotFor(1, 4).Members.All(m => m.Score == 0),
            "All missing players cannot win a party");

        var departing = Choosing(3);
        departing.Join(4, "Late spectator", .3);
        require(departing.SnapshotFor(4, .3).Members.Last().Role == PartyRole.Spectator &&
            !departing.Submit(4, departing.PartyId, 1, 1, RpsChoice.Rock, .3), "Late joins spectate and cannot submit hands");
        var oldId = departing.SnapshotFor(1, .3).Members.Single(m => m.PeerId == 3).PlayerId;
        departing.Leave(3, .3); departing.Join(3, "Replacement", .4);
        require(departing.SnapshotFor(3, .4).Members.Last().PlayerId != oldId && !departing.SnapshotFor(3, .4).Members.Last().Playing,
            "Reused transport IDs cannot inherit participation or scores");
        departing.Submit(1, departing.PartyId, 1, 1, RpsChoice.Rock, .5);
        departing.Submit(2, departing.PartyId, 1, 1, RpsChoice.Scissors, .5);
        require(departing.Phase == PartyPhase.Reveal, "Departed participants do not stall reveal");
        departing.Leave(2, .6);
        require(departing.Phase == PartyPhase.Lobby && departing.SnapshotFor(1, .6).Members.All(m => m.Score == 0),
            "Fewer than two active players cancels without awarding the current game");
        Console.WriteLine("CORE_PARTY_CHECK_PASS: lobby ownership, hidden hands, deadlines, scoring, late joins, disconnect and replay");
    }

    private static PartyCoordinator Choosing(int count)
    {
        var rules = new PartyCoordinator(timing: new PartyTiming(.1, .1, 1, .1, .1));
        for (var peer = 1; peer <= count; peer++) rules.Join(peer, "Player " + peer, 0);
        rules.Configure(1, 1);
        for (var peer = 1; peer <= count; peer++) rules.SetReady(peer, true);
        rules.Start(1, 0); rules.Advance(.1); rules.Advance(.2);
        return rules;
    }
}
