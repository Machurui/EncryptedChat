using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class PasswordHistoryService(EncryptedChatContext context, IPasswordHasher<User> hasher) : IPasswordHistoryService
{
    private readonly EncryptedChatContext _context = context;
    private readonly IPasswordHasher<User> _hasher = hasher;

    // Total distinct passwords blocked = current + (RetainCount - 1) historical.
    // 3 means the user cannot reuse their current password or either of the two
    // most recent previous ones.
    private const int RetainCount = 3;

    public async Task<bool> IsReusedAsync(User user, string candidatePlaintext)
    {
        if (string.IsNullOrEmpty(candidatePlaintext))
            return false;

        List<string> hashesToCheck = new();

        if (!string.IsNullOrEmpty(user.PasswordHash))
            hashesToCheck.Add(user.PasswordHash);

        List<string> historical = await _context.PasswordHistory
            .AsNoTracking()
            .Where(p => p.UserId == user.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Take(RetainCount - 1)
            .Select(p => p.PasswordHash)
            .ToListAsync();

        hashesToCheck.AddRange(historical);

        foreach (string oldHash in hashesToCheck)
        {
            // Identity hashes are non-deterministic (per-call salt), so we must
            // verify via the hasher rather than comparing strings.
            PasswordVerificationResult verifyResult = _hasher.VerifyHashedPassword(user, oldHash, candidatePlaintext);
            if (verifyResult != PasswordVerificationResult.Failed)
                return true;
        }

        return false;
    }

    public async Task RecordAsync(string userId, string hashToRecord)
    {
        if (string.IsNullOrEmpty(hashToRecord))
            return;

        _context.PasswordHistory.Add(new PasswordHistoryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PasswordHash = hashToRecord,
            CreatedAt = DateTime.UtcNow
        });

        // Prune anything beyond the last (RetainCount - 1) historical entries —
        // the current user.PasswordHash counts as the Nth, so history only needs
        // to remember the two preceding ones.
        List<PasswordHistoryEntry> allHistorical = await _context.PasswordHistory
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        int keep = RetainCount - 1;
        // The new entry we just queued via Add() is not yet in `allHistorical`
        // (queries see the DB state before SaveChanges). So we keep `keep - 1`
        // of the loaded entries; once the queued add flushes, total = keep.
        int dropFromIndex = keep - 1;
        if (allHistorical.Count > dropFromIndex)
        {
            _context.PasswordHistory.RemoveRange(allHistorical.Skip(dropFromIndex));
        }

        await _context.SaveChangesAsync();
    }
}
