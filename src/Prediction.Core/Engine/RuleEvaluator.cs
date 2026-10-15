using Prediction.Core.Entities;

namespace Prediction.Core.Engine;

/// <summary>Mọi dữ liệu cần để xét quy tắc cho một trận tại một thời điểm.</summary>
public sealed class MatchContext
{
    public required Fixture Fixture { get; init; }

    public TeamSnapshot? Home { get; init; }

    public TeamSnapshot? Away { get; init; }

    /// <summary>Biến động kèo chấp tới thời điểm xét (Current = kèo hiện tại hoặc kèo đóng).</summary>
    public MarketMovement? Handicap { get; init; }

    public MarketMovement? Totals { get; init; }

    /// <summary>Tài xỉu phạt góc.</summary>
    public MarketMovement? Corners { get; init; }

    /// <summary>Chấp phạt góc (mức theo đội nhà).</summary>
    public MarketMovement? CornersHandicap { get; init; }

    /// <summary>Tài xỉu thẻ phạt.</summary>
    public MarketMovement? Cards { get; init; }

    public ApiAdvice? Advice { get; init; }

    public MarketMovement? MovementFor(Market market) => market switch
    {
        Market.AsianHandicap => Handicap,
        Market.OverUnder => Totals,
        Market.CornersOverUnder => Corners,
        Market.CornersHandicap => CornersHandicap,
        Market.CardsOverUnder => Cards,
        _ => null,
    };
}

/// <summary>Một lựa chọn sinh ra khi quy tắc khớp.</summary>
public sealed record Pick(
    PickSide Side,
    decimal Line,
    decimal Odds,
    PickSide SubjectSide,
    int? SubjectRank,
    int? OpponentRank);

public enum EvaluationMode
{
    /// <summary>Xét mọi điều kiện, cần có kèo của loại kèo quy tắc chọn.</summary>
    Full,

    /// <summary>Chỉ xét điều kiện đội/BXH/phong độ, bỏ qua kèo, biến động và tư vấn API (thống kê không cần kèo).</summary>
    TableOnly,
}

public static class RuleEvaluator
{
    /// <summary>
    /// Xét quy tắc cho một trận từ cả hai góc nhìn (A = đội nhà, A = đội khách).
    /// Trả về null nếu không khớp, hoặc nếu hai góc nhìn chọn hai cửa đối nghịch nhau.
    /// </summary>
    public static Pick? Evaluate(Rule rule, MatchContext ctx, EvaluationMode mode = EvaluationMode.Full)
    {
        if (rule.LeagueId.HasValue && rule.LeagueId != ctx.Fixture.LeagueId)
        {
            return null;
        }

        var home = EvaluatePerspective(rule, ctx, PickSide.Home, mode);
        var away = EvaluatePerspective(rule, ctx, PickSide.Away, mode);

        return (home, away) switch
        {
            (null, null) => null,
            ({ } h, null) => h,
            (null, { } a) => a,
            ({ } h, { } a) when h.Side == a.Side => h,
            _ => null,
        };
    }

    private static Pick? EvaluatePerspective(Rule rule, MatchContext ctx, PickSide subjectSide, EvaluationMode mode)
    {
        var fixture = ctx.Fixture;
        var subjectTeamId = subjectSide == PickSide.Home ? fixture.HomeTeamId : fixture.AwayTeamId;
        var subject = subjectSide == PickSide.Home ? ctx.Home : ctx.Away;
        var opponent = subjectSide == PickSide.Home ? ctx.Away : ctx.Home;

        if (rule.SubjectTeamId.HasValue && rule.SubjectTeamId != subjectTeamId)
        {
            return null;
        }

        if (rule.SubjectVenue == VenueFilter.Home && subjectSide != PickSide.Home ||
            rule.SubjectVenue == VenueFilter.Away && subjectSide != PickSide.Away)
        {
            return null;
        }

        if (!TableConditions(rule, subject, opponent))
        {
            return null;
        }

        var side = rule.Selection switch
        {
            RuleSelection.Subject => subjectSide,
            RuleSelection.Opponent => subjectSide.Opposite(),
            RuleSelection.Over => PickSide.Over,
            _ => PickSide.Under,
        };

        if (mode == EvaluationMode.TableOnly)
        {
            return new Pick(side, 0, 0, subjectSide, subject?.Rank, opponent?.Rank);
        }

        var market = ctx.MovementFor(rule.Market);
        if (market is null)
        {
            return null;
        }

        if (!MarketConditions(rule, ctx, subjectSide) || !AdviceConditions(rule, ctx, subjectSide, subjectTeamId, side))
        {
            return null;
        }

        var current = market.Current;
        var line = LineText.LineForSide(side, current.Line);
        var odds = side is PickSide.Home or PickSide.Over ? current.PriceA : current.PriceB;

        if (rule.MinOdds.HasValue && odds < rule.MinOdds)
        {
            return null;
        }

        return new Pick(side, line, odds, subjectSide, subject?.Rank, opponent?.Rank);
    }

    private static bool TableConditions(Rule rule, TeamSnapshot? subject, TeamSnapshot? opponent)
    {
        if (rule.UsesTableConditions)
        {
            if (subject?.Rank is not { } subjectRank || opponent?.Rank is not { } opponentRank)
            {
                return false;
            }

            if (rule.MinMatchesPlayed is { } minPlayed && (subject.Played < minPlayed || opponent.Played < minPlayed))
            {
                return false;
            }

            var rankGap = opponentRank - subjectRank;
            if (rankGap < rule.MinRankGap || rankGap > rule.MaxRankGap)
            {
                return false;
            }

            if (subjectRank > rule.SubjectMaxRank || opponentRank < rule.OpponentMinRank)
            {
                return false;
            }

            if (subject.Points - opponent.Points < rule.MinPointsGap)
            {
                return false;
            }
        }

        if (rule.SubjectMinFormPoints.HasValue && (subject is null || subject.FormPoints < rule.SubjectMinFormPoints))
        {
            return false;
        }

        if (rule.OpponentMaxFormPoints.HasValue && (opponent is null || opponent.Form.Length == 0 || opponent.FormPoints > rule.OpponentMaxFormPoints))
        {
            return false;
        }

        if (rule.MinAvgTotalGoals.HasValue || rule.MaxAvgTotalGoals.HasValue)
        {
            if (subject?.AvgTotalGoals is not { } a || opponent?.AvgTotalGoals is not { } b)
            {
                return false;
            }

            var avg = (a + b) / 2;
            if (avg < rule.MinAvgTotalGoals || avg > rule.MaxAvgTotalGoals)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MarketConditions(Rule rule, MatchContext ctx, PickSide subjectSide)
    {
        if (rule.AhLineMin.HasValue || rule.AhLineMax.HasValue)
        {
            if (ctx.Handicap is null)
            {
                return false;
            }

            var subjectLine = LineText.LineForSide(subjectSide, ctx.Handicap.Current.Line);
            if (subjectLine < rule.AhLineMin || subjectLine > rule.AhLineMax)
            {
                return false;
            }
        }

        if (rule.OuLineMin.HasValue || rule.OuLineMax.HasValue)
        {
            if (ctx.Totals is null || ctx.Totals.Current.Line < rule.OuLineMin || ctx.Totals.Current.Line > rule.OuLineMax)
            {
                return false;
            }
        }

        if (rule.AhMovement != AhMovementFilter.Any)
        {
            if (ctx.Handicap is null)
            {
                return false;
            }

            var lean = ctx.Handicap.LeanFor(subjectSide, rule.MinAhLineMove, rule.MinPriceDrop);
            var expected = rule.AhMovement switch
            {
                AhMovementFilter.TowardSubject => Lean.Toward,
                AhMovementFilter.TowardOpponent => Lean.Against,
                _ => Lean.Neutral,
            };

            if (lean != expected)
            {
                return false;
            }
        }

        if (rule.OuMovement != OuMovementFilter.Any)
        {
            if (ctx.Totals is null)
            {
                return false;
            }

            var lean = ctx.Totals.LeanFor(PickSide.Over, rule.MinOuLineMove, rule.MinPriceDrop);
            var expected = rule.OuMovement switch
            {
                OuMovementFilter.TowardOver => Lean.Toward,
                OuMovementFilter.TowardUnder => Lean.Against,
                _ => Lean.Neutral,
            };

            if (lean != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AdviceConditions(Rule rule, MatchContext ctx, PickSide subjectSide, int subjectTeamId, PickSide side)
    {
        if (!rule.UsesApiAdvice)
        {
            return true;
        }

        var advice = ctx.Advice;
        if (advice is null)
        {
            return false;
        }

        if (rule.RequireApiWinnerSubject && advice.WinnerTeamId != subjectTeamId)
        {
            return false;
        }

        if (rule.MinApiWinPercentSubject is { } minPercent)
        {
            var percent = subjectSide == PickSide.Home ? advice.PercentHome : advice.PercentAway;
            if (percent is null || percent < minPercent)
            {
                return false;
            }
        }

        if (rule.RequireApiUnderOverAgree && rule.Market == Market.OverUnder && advice.UnderOverSide != side)
        {
            return false;
        }

        return true;
    }
}
