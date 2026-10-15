namespace Prediction.Core.Entities;

/// <summary>Một lần đối đầu giữa hai đội (lấy từ API /fixtures/headtohead, mọi giải và cúp).</summary>
public class H2HMeeting
{
    /// <summary>Id trận trên API-Football.</summary>
    public int FixtureId { get; set; }

    /// <summary>"idNhỏ-idLớn" của hai đội.</summary>
    public string PairKey { get; set; } = "";

    public DateTime KickoffUtc { get; set; }

    public string? LeagueName { get; set; }

    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }

    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }

    public int? HalftimeHome { get; set; }
    public int? HalftimeAway { get; set; }

    public static string Key(int teamA, int teamB) => teamA < teamB ? $"{teamA}-{teamB}" : $"{teamB}-{teamA}";
}

/// <summary>Lần cuối lấy lịch sử đối đầu của một cặp đội.</summary>
public class H2HCheck
{
    public string PairKey { get; set; } = "";

    public DateTime CheckedAtUtc { get; set; }
}
