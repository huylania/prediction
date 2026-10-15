using Prediction.Core.Entities;

namespace Prediction.Core.Engine.Analysis;

public enum InPlaySeverity
{
    Info = 0,

    /// <summary>Cơ hội vào lệnh (khi kèo live đạt điều kiện).</summary>
    Opportunity = 1,

    /// <summary>Cảnh báo tránh một cửa.</summary>
    Warning = 2,
}

/// <summary>Cảnh báo In-play theo một trong 5 quy tắc mục 7 của file kỹ năng.</summary>
public sealed record InPlayAlert(int Rule, InPlaySeverity Severity, string Title, string Detail);

/// <summary>Dữ liệu một trận đang đá: tỉ số, phút, thống kê trực tiếp và bối cảnh trước trận.</summary>
public sealed class InPlayInput
{
    public required Fixture Fixture { get; init; }

    public FixtureStats? Stats { get; init; }

    public string HomeName { get; init; } = "Đội nhà";

    public string AwayName { get; init; } = "Đội khách";

    public DnaTag LeagueTags { get; init; }

    public DnaTag HomeTags { get; init; }

    public DnaTag AwayTags { get; init; }

    /// <summary>Kèo chấp đóng (mức của đội nhà).</summary>
    public decimal? PreMatchHandicap { get; init; }

    /// <summary>Mốc tài xỉu đóng.</summary>
    public decimal? PreMatchTotal { get; init; }

    public RegimeStatus? HomeRegime { get; init; }

    public RegimeStatus? AwayRegime { get; init; }
}

/// <summary>
/// 5 quy tắc In-play: bàn siêu sớm, hạ nhịp săn Tài ở trận DNA Tài, kiểm chứng HLV/tân binh mới,
/// bế tắc hiệp 1, bàn muộn ở giải đuối sức cuối trận. Không dùng kèo live: điều kiện mốc kèo ghi trong cảnh báo để tự đối chiếu.
/// </summary>
public static class InPlayEngine
{
    public static IReadOnlyList<InPlayAlert> Evaluate(InPlayInput input)
    {
        var f = input.Fixture;
        var alerts = new List<InPlayAlert>();
        if (!f.IsLive)
        {
            return alerts;
        }

        var minute = f.Status == "HT" ? 45 : f.Elapsed ?? 0;
        var secondHalf = f.Status is "2H" or "ET" or "BT" or "P" || minute > 45 && f.Status != "HT";
        var hg = f.HomeGoals ?? 0;
        var ag = f.AwayGoals ?? 0;
        var s = input.Stats;
        var overDna = input.LeagueTags.HasFlag(DnaTag.OverGoals) || input.HomeTags.HasFlag(DnaTag.OverGoals) || input.AwayTags.HasFlag(DnaTag.OverGoals);

        // 1. Bàn thắng siêu sớm (≤ 15').
        var firstGoal = s?.FirstGoalMinute;
        if (firstGoal is null && hg + ag > 0 && minute <= 15)
        {
            firstGoal = minute;
        }

        if (firstGoal is <= 15)
        {
            alerts.Add(new InPlayAlert(1, InPlaySeverity.Opportunity, $"Bàn siêu sớm (phút {firstGoal})",
                "Hủy mọi nhận định Xỉu trước trận; chuyển sang pha rượt đuổi/phản công, săn Tài bàn thắng tiếp theo"
                + (overDna ? " (trận có DNA Tài: độ tin cậy cao)." : ".")));
        }

        // 2. Hạ nhịp săn Tài ở trận DNA Tài với mốc trước trận cao.
        if (overDna && input.PreMatchTotal >= 3.25m && hg + ag == 0 && minute is >= 15 and <= 30 && !secondHalf && s is not null)
        {
            var shots = (s.HomeShots ?? 0) + (s.AwayShots ?? 0);
            var onTarget = (s.HomeShotsOnTarget ?? 0) + (s.AwayShotsOnTarget ?? 0);
            if (shots >= minute / 2.5 || onTarget >= 3)
            {
                alerts.Add(new InPlayAlert(2, InPlaySeverity.Opportunity, "Hạ nhịp săn Tài",
                    $"Trận DNA Tài, mốc trước trận {LineText.Number(input.PreMatchTotal!.Value)} nhưng 0-0 ở phút {minute} trong khi đã có {shots} cú sút ({onTarget} trúng đích) → vào Tài bàn live khi mốc về 2.5–2.75 (hoặc Tài 0.5 hiệp 1)."));
            }
        }

        // 3. Kiểm chứng HLV mới / tân binh trong 20–25' đầu.
        if (minute is >= 20 and <= 30 && !secondHalf && s is not null)
        {
            foreach (var (name, regime, own, opp, ownOnTarget, oppOnTarget) in new[]
            {
                (input.HomeName, input.HomeRegime, s.HomeShots ?? 0, s.AwayShots ?? 0, s.HomeShotsOnTarget ?? 0, s.AwayShotsOnTarget ?? 0),
                (input.AwayName, input.AwayRegime, s.AwayShots ?? 0, s.HomeShots ?? 0, s.AwayShotsOnTarget ?? 0, s.HomeShotsOnTarget ?? 0),
            })
            {
                if (regime is not { InBuffer: true })
                {
                    continue;
                }

                var dominant = own >= 4 && own >= 2 * Math.Max(opp, 1) && ownOnTarget >= oppOnTarget;
                alerts.Add(dominant
                    ? new InPlayAlert(3, InPlaySeverity.Opportunity, $"{name}: triết lý mới vận hành tốt",
                        $"{regime.KindLabel} {regime.PersonName} (trận thứ {regime.UpcomingMatchNumber}): sút {own}–{opp}, trúng đích {ownOnTarget}–{oppOnTarget} → có thể mở vị thế In-play theo hướng {name}.")
                    : new InPlayAlert(3, InPlaySeverity.Info, $"{name}: chưa thuyết phục",
                        $"{regime.KindLabel} {regime.PersonName} (trận thứ {regime.UpcomingMatchNumber}): sút {own}–{opp}, trúng đích {ownOnTarget}–{oppOnTarget} → chưa đủ áp đảo, tiếp tục đứng ngoài."));
            }
        }

        // 4. Bế tắc hiệp 1.
        if (hg == 0 && ag == 0 && (f.Status == "HT" || secondHalf && minute <= 60) && s is not null)
        {
            var onTarget = (s.HomeShotsOnTarget ?? 0) + (s.AwayShotsOnTarget ?? 0);
            var favourite = input.PreMatchHandicap switch
            {
                < 0 => input.HomeName,
                > 0 => input.AwayName,
                _ => null,
            };
            var fragileAwayFavourite = input.PreMatchHandicap > 0 && input.AwayTags.HasFlag(DnaTag.AwayFragile);
            if (onTarget <= 1 || fragileAwayFavourite)
            {
                var cornersHint = favourite is not null && FavouriteCorners(input) is { } c && c >= 5
                    ? $" Cửa trên đã có {c} quả góc: cân nhắc chuyển sang kèo góc nếu họ chỉ biết tạt cánh."
                    : "";
                alerts.Add(new InPlayAlert(4, InPlaySeverity.Warning, "Bế tắc tiêu cực",
                    (onTarget <= 1 ? $"Hiệp 1 hòa 0-0, tổng sút trúng đích chỉ {onTarget}" : $"{input.AwayName} (yếu sân khách) đang bế tắc trên sân đối phương")
                    + $" → không nhồi {favourite ?? "cửa trên"} ở hiệp 2; ưu tiên cửa dưới hoặc Xỉu in-play.{cornersHint}"));
            }
        }

        // 5. Thể lực hiệp 2: giải hay có bàn muộn.
        if (input.LeagueTags.HasFlag(DnaTag.LateGoals) && secondHalf && minute is >= 60 and <= 85)
        {
            alerts.Add(new InPlayAlert(5, InPlaySeverity.Opportunity, "Bàn muộn (đuối sức cuối trận)",
                "Hàng thủ thường sụp thể lực từ phút 65–70 → săn bàn thắng muộn từ phút 70"
                + (input.LeagueTags.HasFlag(DnaTag.HomeFortress) ? ", ưu tiên đội chủ nhà." : ".")));
        }

        return alerts;
    }

    private static int? FavouriteCorners(InPlayInput input) => input.PreMatchHandicap switch
    {
        < 0 => input.Stats?.HomeCorners,
        > 0 => input.Stats?.AwayCorners,
        _ => null,
    };
}
