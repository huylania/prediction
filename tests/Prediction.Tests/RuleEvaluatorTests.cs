using Prediction.Core.Engine;
using Prediction.Core.Entities;
using static Prediction.Tests.TestData;

namespace Prediction.Tests;

public class RuleEvaluatorTests
{
    private const int Mu = 33;
    private const int Everton = 45;

    private static Rule MuRule() => new()
    {
        Name = "MU gặp đội xếp dưới → chọn MU",
        Market = Market.AsianHandicap,
        Selection = RuleSelection.Subject,
        SubjectTeamId = Mu,
        MinRankGap = 1,
        MinMatchesPlayed = 5,
    };

    private static MatchContext Ctx(int home, int away, int homeRank, int awayRank, MarketMovement? ah = null, MarketMovement? ou = null, ApiAdvice? advice = null, bool hasTable = true) => new()
    {
        Fixture = Match(1, 0, home, away, hasTable: hasTable),
        Home = hasTable ? Team(home, homeRank) : null,
        Away = hasTable ? Team(away, awayRank) : null,
        Handicap = ah,
        Totals = ou,
        Advice = advice,
    };

    [Fact]
    public void Mu_away_against_lower_team_picks_mu_with_away_line()
    {
        var ah = Movement(Market.AsianHandicap, (0.75m, 2.05m, 1.85m));
        var pick = RuleEvaluator.Evaluate(MuRule(), Ctx(Everton, Mu, 14, 6, ah));

        Assert.NotNull(pick);
        Assert.Equal(PickSide.Away, pick.Side);
        Assert.Equal(-0.75m, pick.Line);
        Assert.Equal(1.85m, pick.Odds);
        Assert.Equal(6, pick.SubjectRank);
    }

    [Fact]
    public void Mu_against_higher_team_does_not_match()
    {
        var ah = Movement(Market.AsianHandicap, (0m, 1.9m, 1.9m));
        Assert.Null(RuleEvaluator.Evaluate(MuRule(), Ctx(Mu, Everton, 8, 3, ah)));
    }

    [Fact]
    public void Needs_odds_in_full_mode_but_not_in_table_only_mode()
    {
        var ctx = Ctx(Mu, Everton, 3, 15);

        Assert.Null(RuleEvaluator.Evaluate(MuRule(), ctx));
        Assert.Equal(PickSide.Home, RuleEvaluator.Evaluate(MuRule(), ctx, EvaluationMode.TableOnly)!.Side);
    }

    [Fact]
    public void Too_few_matches_played_blocks_rank_conditions()
    {
        var ctx = new MatchContext
        {
            Fixture = Match(1, 0, Mu, Everton),
            Home = Team(Mu, 1, played: 3),
            Away = Team(Everton, 10, played: 3),
            Handicap = Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m)),
        };

        Assert.Null(RuleEvaluator.Evaluate(MuRule(), ctx));
    }

    [Fact]
    public void Knockout_without_table_does_not_match_rank_rule()
    {
        var ah = Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m));
        Assert.Null(RuleEvaluator.Evaluate(MuRule(), Ctx(Mu, Everton, 0, 0, ah, hasTable: false)));
    }

    [Fact]
    public void Handicap_range_is_from_subject_perspective()
    {
        var rule = MuRule();
        rule.AhLineMin = -1m;
        rule.AhLineMax = -0.25m;

        var within = Movement(Market.AsianHandicap, (-0.75m, 1.9m, 1.9m));
        var tooDeep = Movement(Market.AsianHandicap, (-1.5m, 1.9m, 1.9m));
        var awayWithin = Movement(Market.AsianHandicap, (0.5m, 1.9m, 1.9m));

        Assert.NotNull(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, within)));
        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, tooDeep)));
        Assert.NotNull(RuleEvaluator.Evaluate(rule, Ctx(Everton, Mu, 15, 3, awayWithin)));
    }

    [Fact]
    public void Movement_condition_toward_subject()
    {
        var rule = MuRule();
        rule.AhMovement = AhMovementFilter.TowardSubject;

        var toward = Movement(Market.AsianHandicap, (-0.75m, 1.9m, 1.9m), (-1m, 1.85m, 1.95m));
        var against = Movement(Market.AsianHandicap, (-0.75m, 1.9m, 1.9m), (-0.5m, 1.85m, 1.95m));

        Assert.NotNull(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, toward)));
        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, against)));
    }

    [Fact]
    public void Api_conditions()
    {
        var rule = MuRule();
        rule.RequireApiWinnerSubject = true;
        rule.MinApiWinPercentSubject = 50;
        var ah = Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m));

        var agrees = new ApiAdvice { WinnerTeamId = Mu, PercentHome = 55 };
        var weak = new ApiAdvice { WinnerTeamId = Mu, PercentHome = 45 };
        var other = new ApiAdvice { WinnerTeamId = Everton, PercentHome = 55 };

        Assert.NotNull(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, ah, advice: agrees)));
        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, ah, advice: weak)));
        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, ah, advice: other)));
        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, ah)));
    }

    [Fact]
    public void Totals_rule_with_api_under_over_agreement()
    {
        var rule = new Rule
        {
            Name = "Tài",
            Market = Market.OverUnder,
            Selection = RuleSelection.Over,
            MinAvgTotalGoals = 2.5m,
            RequireApiUnderOverAgree = true,
        };
        var ou = Movement(Market.OverUnder, (2.5m, 1.95m, 1.85m));

        var pick = RuleEvaluator.Evaluate(rule, Ctx(1, 2, 3, 4, ou: ou, advice: new ApiAdvice { UnderOver = "+2.5" }));
        Assert.NotNull(pick);
        Assert.Equal(PickSide.Over, pick.Side);
        Assert.Equal(2.5m, pick.Line);
        Assert.Equal(1.95m, pick.Odds);

        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(1, 2, 3, 4, ou: ou, advice: new ApiAdvice { UnderOver = "-3.5" })));
    }

    [Fact]
    public void Contradicting_perspectives_give_no_pick()
    {
        var rule = new Rule { Name = "Mọi đội A", Market = Market.AsianHandicap, Selection = RuleSelection.Subject };
        var ah = Movement(Market.AsianHandicap, (0m, 1.9m, 1.9m));

        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(1, 2, 3, 4, ah)));
    }

    [Fact]
    public void League_filter()
    {
        var rule = MuRule();
        rule.LeagueId = 140;
        var ah = Movement(Market.AsianHandicap, (-1m, 1.9m, 1.9m));

        Assert.Null(RuleEvaluator.Evaluate(rule, Ctx(Mu, Everton, 3, 15, ah)));
    }

    [Fact]
    public void Validation_messages()
    {
        var rule = new Rule { Name = "", Market = Market.OverUnder, Selection = RuleSelection.Subject, AhLineMin = 0.3m };

        var errors = rule.Validate().ToList();

        Assert.Contains(errors, e => e.Contains("tên"));
        Assert.Contains(errors, e => e.Contains("Tài hoặc Xỉu"));
        Assert.Contains(errors, e => e.Contains("0.25"));
    }
}
