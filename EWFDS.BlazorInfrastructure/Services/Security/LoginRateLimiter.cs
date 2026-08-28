using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EWFDS.BlazorInfrastructure.Services.Security
{
    /// <summary>
    /// Rate limiter for login attempts to prevent brute-force attacks.
    /// Uses sliding window approach: tracks attempts within a time window.
    /// </summary>
    public interface ILoginRateLimiter
    {
        /// <summary>
        /// Checks if a login attempt should be allowed and records the attempt.
        /// </summary>
        /// <param name="identifier">Username or IP address to track</param>
        /// <returns>True if allowed, false if rate limited</returns>
        bool IsAllowed(string identifier);

        /// <summary>
        /// Records a failed login attempt (increases penalty).
        /// </summary>
        void RecordFailedAttempt(string identifier);

        /// <summary>
        /// Clears attempts for an identifier after successful login.
        /// </summary>
        void ClearAttempts(string identifier);

        /// <summary>
        /// Gets remaining lockout time for a rate-limited identifier.
        /// </summary>
        TimeSpan? GetLockoutRemaining(string identifier);
    }

    public class LoginRateLimiter : ILoginRateLimiter
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<LoginRateLimiter> _logger;

        // Configuration - could be moved to appsettings.json
        private const int MaxAttempts = 5;                          // Max attempts before lockout
        private static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(15);  // Tracking window
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15); // Lockout duration after max attempts

        public LoginRateLimiter(IMemoryCache cache, ILogger<LoginRateLimiter> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsAllowed(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return true;

            var key = GetCacheKey(identifier);

            if (_cache.TryGetValue(key, out LoginAttemptInfo? info) && info != null)
            {
                // Check if currently locked out
                if (info.LockedUntil.HasValue && info.LockedUntil > DateTime.UtcNow)
                {
                    _logger.LogWarning(
                        "Login blocked for {Identifier}: locked out until {LockedUntil}", 
                        identifier, 
                        info.LockedUntil);
                    return false;
                }

                // Check if max attempts exceeded within window
                if (info.AttemptCount >= MaxAttempts)
                {
                    // Apply lockout
                    info.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                    _cache.Set(key, info, LockoutDuration);

                    _logger.LogWarning(
                        "Login locked for {Identifier}: {AttemptCount} failed attempts. Locked for {Duration} minutes",
                        identifier,
                        info.AttemptCount,
                        LockoutDuration.TotalMinutes);
                    return false;
                }
            }

            return true;
        }

        public void RecordFailedAttempt(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return;

            var key = GetCacheKey(identifier);

            var info = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = WindowDuration;
                return new LoginAttemptInfo();
            })!;

            info.AttemptCount++;
            info.LastAttempt = DateTime.UtcNow;

            // Extend the cache entry
            _cache.Set(key, info, WindowDuration);

            _logger.LogInformation(
                "Failed login attempt {AttemptCount}/{MaxAttempts} for {Identifier}",
                info.AttemptCount,
                MaxAttempts,
                identifier);
        }

        public void ClearAttempts(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return;

            var key = GetCacheKey(identifier);
            _cache.Remove(key);

            _logger.LogDebug("Login attempts cleared for {Identifier}", identifier);
        }

        public TimeSpan? GetLockoutRemaining(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return null;

            var key = GetCacheKey(identifier);

            if (_cache.TryGetValue(key, out LoginAttemptInfo? info) && 
                info?.LockedUntil.HasValue == true && 
                info.LockedUntil > DateTime.UtcNow)
            {
                return info.LockedUntil.Value - DateTime.UtcNow;
            }

            return null;
        }

        private static string GetCacheKey(string identifier) => $"LoginAttempt:{identifier.ToLowerInvariant()}";

        private class LoginAttemptInfo
        {
            public int AttemptCount { get; set; }
            public DateTime LastAttempt { get; set; }
            public DateTime? LockedUntil { get; set; }
        }
    }
}
