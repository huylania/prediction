using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prediction.Core.Entities;
using Prediction.Web.Data;

namespace Prediction.Web.Services;

/// <summary>Tài khoản quản trị duy nhất của website.</summary>
public sealed class AdminAccountService(IDbContextFactory<AppDbContext> dbFactory)
{
    private static readonly PasswordHasher<AdminUser> Hasher = new();

    public async Task<bool> HasAdminAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.AdminUsers.AnyAsync(ct);
    }

    /// <summary>Tạo tài khoản đầu tiên. Trả về lỗi nếu đã có tài khoản hoặc dữ liệu không hợp lệ.</summary>
    public async Task<string?> CreateFirstAsync(string username, string password, CancellationToken ct = default)
    {
        username = username.Trim();
        if (username.Length < 3)
        {
            return "Tên đăng nhập cần ít nhất 3 ký tự.";
        }

        if (password.Length < 8)
        {
            return "Mật khẩu cần ít nhất 8 ký tự.";
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.AdminUsers.AnyAsync(ct))
        {
            return "Đã có tài khoản quản trị.";
        }

        var user = new AdminUser { Username = username };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<AdminUser?> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username.Trim(), ct);
        if (user is null)
        {
            return null;
        }

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = Hasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct);
        }

        return result == PasswordVerificationResult.Failed ? null : user;
    }

    public async Task<string?> ChangePasswordAsync(string username, string current, string next, CancellationToken ct = default)
    {
        if (next.Length < 8)
        {
            return "Mật khẩu mới cần ít nhất 8 ký tự.";
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, current) == PasswordVerificationResult.Failed)
        {
            return "Mật khẩu hiện tại không đúng.";
        }

        user.PasswordHash = Hasher.HashPassword(user, next);
        await db.SaveChangesAsync(ct);
        return null;
    }
}
