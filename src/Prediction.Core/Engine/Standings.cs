using Prediction.Core.Entities;

namespace Prediction.Core.Engine;

/// <summary>Tình trạng của một đội tại một thời điểm (trước một trận cụ thể).</summary>
public sealed class TeamSnapshot
{
    public int TeamId { get; init; }

    /// <summary>Thứ hạng; null nếu trận không thuộc giai đoạn có BXH (vd knock-out C1).</summary>
    public int? Rank { get; init; }

    public int Played { get; init; }
    public int Won { get; init; }
    public int Drawn { get; init; }
    public int Lost { get; init; }
    public int GoalsFor { get; init; }
    public int GoalsAgainst { get; init; }
    public int Points { get; init; }
    public int GoalDiff => GoalsFor - GoalsAgainst;

    /// <summary>Kết quả 5 trận gần nhất, cũ → mới ('W', 'D', 'L').</summary>
    public string Form { get; init; } = "";

    public int FormPoints => Form.Sum(c => c switch { 'W' => 3, 'D' => 1, _ => 0 });

    /// <summary>Trung bình tổng số bàn/trận trong 5 trận gần nhất của đội.</summary>
    public decimal? AvgTotalGoals { get; init; }
}

public sealed class StandingRow
{
    public int Rank { get; set; }
    public int TeamId { get; init; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int Points => Won * 3 + Drawn;
    public int GoalDiff => GoalsFor - GoalsAgainst;
}

/// <summary>
/// Tính BXH và phong độ TRƯỚC mỗi trận của một giải-mùa, chỉ dùng kết quả đã biết lúc bóng lăn,
/// để gợi ý trực tiếp và kiểm chứng lịch sử dùng chung một cách tính, không "nhìn trước" kết quả.
/// </summary>
public static class StandingsTimeline
{
    /// <summary>Một trận được coi là đã có kết quả nếu bắt đầu sớm hơn ít nhất ngần này trước trận đang xét.</summary>
    public static readonly TimeSpan ResultKnownAfter = TimeSpan.FromMinutes(150);

    private const int FormLength = 5;

    /// <summary>
    /// Trả về (đội nhà, đội khách) trước mỗi trận trong <paramref name="seasonFixtures"/>
    /// (tất cả trận của MỘT giải-mùa, gồm cả trận chưa đá).
    /// </summary>
    public static Dictionary<int, (TeamSnapshot Home, TeamSnapshot Away)> Build(IEnumerable<Fixture> seasonFixtures)
    {
        var all = seasonFixtures.OrderBy(f => f.KickoffUtc).ThenBy(f => f.Id).ToList();
        var finished = all.Where(f => f.IsFinished).ToList();
        var tableTeams = all.Where(f => f.HasTable).SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToHashSet();

        var table = new Table(tableTeams);
        var recent = new Dictionary<int, List<(char Result, int TotalGoals)>>();
        var result = new Dictionary<int, (TeamSnapshot, TeamSnapshot)>();
        var next = 0;

        foreach (var fixture in all)
        {
            while (next < finished.Count && finished[next].KickoffUtc + ResultKnownAfter <= fixture.KickoffUtc)
            {
                Apply(finished[next], table, recent);
                next++;
            }

            result[fixture.Id] = (
                Snapshot(fixture.HomeTeamId, fixture.HasTable, table, recent),
                Snapshot(fixture.AwayTeamId, fixture.HasTable, table, recent));
        }

        return result;
    }

    /// <summary>BXH hiện tại từ mọi trận đã đá thuộc giai đoạn có BXH.</summary>
    public static IReadOnlyList<StandingRow> CurrentTable(IEnumerable<Fixture> seasonFixtures)
    {
        var all = seasonFixtures.Where(f => f.HasTable).ToList();
        var table = new Table(all.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }));
        foreach (var fixture in all.Where(f => f.IsFinished))
        {
            table.Add(fixture);
        }

        return table.Ranked();
    }

    private static void Apply(Fixture f, Table table, Dictionary<int, List<(char, int)>> recent)
    {
        if (f.HasTable)
        {
            table.Add(f);
        }

        var hg = f.HomeGoals!.Value;
        var ag = f.AwayGoals!.Value;
        Push(recent, f.HomeTeamId, hg > ag ? 'W' : hg == ag ? 'D' : 'L', hg + ag);
        Push(recent, f.AwayTeamId, ag > hg ? 'W' : hg == ag ? 'D' : 'L', hg + ag);
    }

    private static void Push(Dictionary<int, List<(char, int)>> recent, int teamId, char result, int total)
    {
        if (!recent.TryGetValue(teamId, out var list))
        {
            recent[teamId] = list = [];
        }

        list.Add((result, total));
        if (list.Count > FormLength)
        {
            list.RemoveAt(0);
        }
    }

    private static TeamSnapshot Snapshot(int teamId, bool hasTable, Table table, Dictionary<int, List<(char Result, int TotalGoals)>> recent)
    {
        recent.TryGetValue(teamId, out var form);
        var row = table.Row(teamId);
        return new TeamSnapshot
        {
            TeamId = teamId,
            Rank = hasTable && row is not null ? table.RankOf(teamId) : null,
            Played = row?.Played ?? 0,
            Won = row?.Won ?? 0,
            Drawn = row?.Drawn ?? 0,
            Lost = row?.Lost ?? 0,
            GoalsFor = row?.GoalsFor ?? 0,
            GoalsAgainst = row?.GoalsAgainst ?? 0,
            Points = row?.Points ?? 0,
            Form = form is null ? "" : new string(form.Select(x => x.Result).ToArray()),
            AvgTotalGoals = form is { Count: > 0 } ? Math.Round((decimal)form.Average(x => x.TotalGoals), 2) : null,
        };
    }

    private sealed class Table
    {
        private readonly Dictionary<int, StandingRow> _rows;
        private Dictionary<int, int>? _ranks;

        public Table(IEnumerable<int> teamIds)
        {
            _rows = teamIds.Distinct().ToDictionary(id => id, id => new StandingRow { TeamId = id });
        }

        public StandingRow? Row(int teamId) => _rows.GetValueOrDefault(teamId);

        public void Add(Fixture f)
        {
            var home = Ensure(f.HomeTeamId);
            var away = Ensure(f.AwayTeamId);
            var hg = f.HomeGoals!.Value;
            var ag = f.AwayGoals!.Value;

            home.Played++;
            away.Played++;
            home.GoalsFor += hg;
            home.GoalsAgainst += ag;
            away.GoalsFor += ag;
            away.GoalsAgainst += hg;

            if (hg > ag)
            {
                home.Won++;
                away.Lost++;
            }
            else if (hg < ag)
            {
                away.Won++;
                home.Lost++;
            }
            else
            {
                home.Drawn++;
                away.Drawn++;
            }

            _ranks = null;
        }

        public int RankOf(int teamId)
        {
            _ranks ??= Ranked().ToDictionary(r => r.TeamId, r => r.Rank);
            return _ranks[teamId];
        }

        public IReadOnlyList<StandingRow> Ranked()
        {
            var ordered = _rows.Values
                .OrderByDescending(r => r.Points)
                .ThenByDescending(r => r.GoalDiff)
                .ThenByDescending(r => r.GoalsFor)
                .ThenByDescending(r => r.Won)
                .ThenBy(r => r.TeamId)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Rank = i + 1;
            }

            return ordered;
        }

        private StandingRow Ensure(int teamId)
        {
            if (!_rows.TryGetValue(teamId, out var row))
            {
                _rows[teamId] = row = new StandingRow { TeamId = teamId };
            }

            return row;
        }
    }
}
