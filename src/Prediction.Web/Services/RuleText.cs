using Prediction.Core.Engine;
using Prediction.Core.Entities;

namespace Prediction.Web.Services;

/// <summary>Diễn giải quy tắc thành câu tiếng Việt.</summary>
public static class RuleText
{
    public static string Selection(Rule rule) => rule.Selection switch
    {
        RuleSelection.Subject => "Chọn đội A (kèo chấp)",
        RuleSelection.Opponent => "Chọn đội B (kèo chấp)",
        RuleSelection.Over => "Chọn Tài",
        _ => "Chọn Xỉu",
    };

    public static List<string> Conditions(Rule rule, string? leagueName, string? teamName)
    {
        var list = new List<string>();
        list.Add(leagueName is null ? "Mọi giải" : $"Giải: {leagueName}");
        if (rule.SubjectTeamId.HasValue)
        {
            list.Add($"Đội A là {teamName ?? $"#{rule.SubjectTeamId}"}");
        }

        if (rule.SubjectVenue != VenueFilter.Any)
        {
            list.Add(rule.SubjectVenue == VenueFilter.Home ? "A đá sân nhà" : "A đá sân khách");
        }

        if (rule.MinRankGap.HasValue)
        {
            list.Add(rule.MinRankGap > 0 ? $"A xếp trên B ít nhất {rule.MinRankGap} bậc" : $"Chênh hạng (B − A) ≥ {rule.MinRankGap}");
        }

        if (rule.MaxRankGap.HasValue)
        {
            list.Add($"Chênh hạng (B − A) ≤ {rule.MaxRankGap}");
        }

        if (rule.SubjectMaxRank.HasValue)
        {
            list.Add($"A trong top {rule.SubjectMaxRank}");
        }

        if (rule.OpponentMinRank.HasValue)
        {
            list.Add($"B xếp từ hạng {rule.OpponentMinRank} trở xuống");
        }

        if (rule.MinPointsGap.HasValue)
        {
            list.Add($"A hơn B ít nhất {rule.MinPointsGap} điểm");
        }

        if (rule.UsesTableConditions && rule.MinMatchesPlayed.HasValue)
        {
            list.Add($"Hai đội đã đá ≥ {rule.MinMatchesPlayed} trận");
        }

        if (rule.SubjectMinFormPoints.HasValue)
        {
            list.Add($"A ≥ {rule.SubjectMinFormPoints} điểm/5 trận gần nhất");
        }

        if (rule.OpponentMaxFormPoints.HasValue)
        {
            list.Add($"B ≤ {rule.OpponentMaxFormPoints} điểm/5 trận gần nhất");
        }

        if (rule.MinAvgTotalGoals.HasValue || rule.MaxAvgTotalGoals.HasValue)
        {
            list.Add($"TB tổng bàn 5 trận gần nhất {Range(rule.MinAvgTotalGoals, rule.MaxAvgTotalGoals)}");
        }

        if (rule.AhLineMin.HasValue || rule.AhLineMax.HasValue)
        {
            list.Add($"Mức chấp của A {SignedRange(rule.AhLineMin, rule.AhLineMax)}");
        }

        if (rule.OuLineMin.HasValue || rule.OuLineMax.HasValue)
        {
            list.Add($"Mức tài xỉu {Range(rule.OuLineMin, rule.OuLineMax)}");
        }

        if (rule.MinOdds.HasValue)
        {
            list.Add($"Odds ≥ {LineText.Odds(rule.MinOdds.Value)}");
        }

        if (rule.AhMovement != AhMovementFilter.Any)
        {
            list.Add(rule.AhMovement switch
            {
                AhMovementFilter.TowardSubject => "Kèo chấp nghiêng về A (kèo tăng cho A hoặc tiền A giảm)",
                AhMovementFilter.TowardOpponent => "Kèo chấp nghiêng về B",
                _ => "Kèo chấp đứng yên",
            } + Thresholds(rule.MinAhLineMove, rule.MinPriceDrop));
        }

        if (rule.OuMovement != OuMovementFilter.Any)
        {
            list.Add(rule.OuMovement switch
            {
                OuMovementFilter.TowardOver => "Tài xỉu nghiêng về Tài (mức tăng hoặc tiền Tài giảm)",
                OuMovementFilter.TowardUnder => "Tài xỉu nghiêng về Xỉu",
                _ => "Tài xỉu đứng yên",
            } + Thresholds(rule.MinOuLineMove, rule.MinPriceDrop));
        }

        if (rule.RequireApiWinnerSubject)
        {
            list.Add("API dự đoán A thắng");
        }

        if (rule.MinApiWinPercentSubject.HasValue)
        {
            list.Add($"API cho A thắng ≥ {rule.MinApiWinPercentSubject}%");
        }

        if (rule.RequireApiUnderOverAgree && rule.Market == Market.OverUnder)
        {
            list.Add("API dự đoán tài/xỉu cùng hướng");
        }

        return list;
    }

    private static string Thresholds(decimal? line, decimal? price)
    {
        var parts = new List<string>();
        if (line.HasValue)
        {
            parts.Add($"dịch ≥ {LineText.Number(line.Value)}");
        }

        if (price.HasValue)
        {
            parts.Add($"tiền giảm ≥ {LineText.Odds(price.Value)}");
        }

        return parts.Count == 0 ? "" : $" ({string.Join(", ", parts)})";
    }

    private static string Range(decimal? min, decimal? max) => (min, max) switch
    {
        ({ } a, { } b) => $"từ {LineText.Number(a)} đến {LineText.Number(b)}",
        ({ } a, null) => $"≥ {LineText.Number(a)}",
        (null, { } b) => $"≤ {LineText.Number(b)}",
        _ => "",
    };

    private static string SignedRange(decimal? min, decimal? max) => (min, max) switch
    {
        ({ } a, { } b) => $"từ {LineText.Signed(a)} đến {LineText.Signed(b)}",
        ({ } a, null) => $"≥ {LineText.Signed(a)}",
        (null, { } b) => $"≤ {LineText.Signed(b)}",
        _ => "",
    };
}
