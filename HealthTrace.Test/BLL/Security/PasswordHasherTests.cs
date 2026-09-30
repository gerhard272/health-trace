using BCrypt.Net;
using HealthTrace.BLL.Security;

namespace HealthTrace.Test.BLL.Security
{
    /// <summary>
    /// Tests for PasswordHasher, the only IPasswordHasher implementation: the place where
    /// the application produces and verifies user secrets.
    /// No mocks, because the class has no dependencies and delegates to BCrypt: it is also the only place
    /// in the suite where BCrypt actually runs, since in the UserService tests the hasher
    /// is mocked. The values used are made up and implausible as real credentials.
    /// Mind the cost: work factor 11, about 100 ms per operation. Do not lower it to
    /// speed up the suite, otherwise the tests would not protect the production algorithm.
    /// Mind the argument order: VerifyPassword takes the hash first and the plain-text
    /// password second, the opposite of BCrypt.Verify(text, hash).
    /// </summary>
    public class PasswordHasherTests
    {
        private const string TestPassword = "Test-password-1!";

        private const string BcryptPrefix = "$2a$";

        // The work factor is written out here and not read from BCrypt.DefaultRounds: a
        // downgrade of the algorithm must break the suite, not pass silently.
        private const int BcryptWorkFactor = 11;
        private const int BcryptHashLength = 60;

        // 72 bytes is the maximum BCrypt considers: it matches the PasswordMaxLength of the
        // registration validator, so a password at the boundary reaches the hasher in full.
        private const int BcryptMaxPasswordBytes = 72;

        private readonly PasswordHasher _hasher = new();

        // --- HashPassword ---

        [Fact]
        public void HashPassword_ReturnsBcryptHashAndNotThePassword()
        {
            // The result must be the bcrypt hash, not the password: if the work factor
            // dropped, this test would signal that the protection has weakened.
            var hash = _hasher.HashPassword(TestPassword);

            Assert.NotEqual(TestPassword, hash);
            Assert.StartsWith($"{BcryptPrefix}{BcryptWorkFactor}$", hash);
            Assert.Equal(BcryptHashLength, hash.Length);
        }

        [Fact]
        public void HashPassword_TwiceOnTheSamePassword_ReturnsDifferentHashes()
        {
            // The salt is new on every call: two users with the same password must not have the
            // same hash, otherwise whoever reads the table can tell who shares a password.
            // Both hashes stay verifiable, because the salt travels inside the hash.
            var first = _hasher.HashPassword(TestPassword);
            var second = _hasher.HashPassword(TestPassword);

            Assert.NotEqual(first, second);
            Assert.True(_hasher.VerifyPassword(first, TestPassword));
            Assert.True(_hasher.VerifyPassword(second, TestPassword));
        }

        [Fact]
        public void HashPassword_ThenVerify_RoundTripAsUserServiceDoes_ReturnsTrue()
        {
            // The exact UserService round trip: RegisterAsync hashes the model's password and
            // LoginAsync verifies the stored hash. If this round trip breaks, registration
            // produces users who can no longer authenticate.
            var hash = _hasher.HashPassword(TestPassword);

            Assert.True(_hasher.VerifyPassword(hash, TestPassword));
        }

        [Fact]
        public void HashPassword_PasswordOf72Bytes_IsHashedAndVerifiable()
        {
            // The BCrypt limit and the validator limit match, so the longest allowed password
            // reaches the hasher in full. Repeated value, so it is not plausible as a
            // real credential.
            var password = new string('x', BcryptMaxPasswordBytes);

            var hash = _hasher.HashPassword(password);

            Assert.True(_hasher.VerifyPassword(hash, password));
        }

        // --- VerifyPassword ---

        [Theory]
        [InlineData("test-password-1!")]   // different case
        [InlineData("Test-password-1! ")]  // trailing space
        [InlineData("Test-password-2!")]   // different character
        [InlineData("Test-password")]      // shorter
        [InlineData("Test-password-11!")]  // different length
        public void VerifyPassword_PasswordDifferentFromTheHashedOne_ReturnsFalse(string providedPassword)
        {
            // No false positives: even a similar password must never pass. false is the only
            // answer UserService can turn into 401, so a true here would open the session to
            // someone who typed the wrong password.
            var hash = _hasher.HashPassword(TestPassword);

            Assert.False(_hasher.VerifyPassword(hash, providedPassword));
        }

        [Fact]
        public void VerifyPassword_AcceptsHashCreatedWithADifferentWorkFactor()
        {
            // The work factor may change over time, for example because hardware gets
            // faster: hashes already in the table must stay verifiable, otherwise raising the
            // factor would lock out every previously registered user. Here the low
            // factor only keeps the test fast, it is not the production one.
            var hashWithLowerWorkFactor = BCrypt.Net.BCrypt.HashPassword(TestPassword, 4);

            Assert.True(_hasher.VerifyPassword(hashWithLowerWorkFactor, TestPassword));
        }

        [Fact]
        public void VerifyPassword_ArgumentsSwapped_ThrowsSaltParseException()
        {
            // The wrapper's order is the reverse of BCrypt.Verify(text, hash): passing the two
            // arguments the other way round, the plain-text password is read as the stored hash and BCrypt
            // throws an exception instead of returning false. The test makes the trap explicit:
            // a caller who gets the order wrong fails immediately instead of authenticating the
            // wrong user. In UserService the correct form is VerifyPassword(user.PasswordHash, password).
            var hash = _hasher.HashPassword(TestPassword);

            Assert.Throws<SaltParseException>(() => _hasher.VerifyPassword(TestPassword, hash));
        }

        [Fact]
        public void VerifyPassword_StoredHashNotGeneratedByBcrypt_ThrowsSaltParseException()
        {
            // A stored hash that is not a bcrypt hash (corrupted data, plain-text column by mistake,
            // format from a different version) throws instead of returning false. In LoginAsync
            // this becomes a 500 error and not a 401: the behavior is documented here, but it must be
            // kept in mind when deciding how to handle corrupted data.
            Assert.Throws<SaltParseException>(() => _hasher.VerifyPassword("not-a-bcrypt-hash", TestPassword));
        }
    }
}
